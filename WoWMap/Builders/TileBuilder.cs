using WoWMap.Layers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DetourLayer;
using RecastLayer;

using WoWMap.Geometry;
using SharpDX;

namespace WoWMap.Builders
{
    public class TileBuilder
    {
        public string World { get; private set; }
        public int X { get; private set; }
        public int Y { get; private set; }
        public int MapId { get; private set; }

        public RecastConfig Config { get; private set; }
        public Geometry.Geometry Geometry { get; private set; }
        public RecastContext Context { get; private set; }
 

        private static string GetAdtPath(string world, int x, int y)
        {
            return "World\\Maps\\" + world + "\\" + world + "_" + x + "_" + y + ".adt";
        }

        public TileBuilder(string world, int x, int y)
        {
            World = world;
            X = x;
            Y = y;
            Config = RecastConfig.Default;
            //  MapId = PhaseHelper.GetMapIdByName(World);
        }

        private void CalculateTileBounds(out Vector3 bmin, out Vector3 bmax)
        {
            var origin = new[] { -WoWMap.Constants.MaxXY, 0, -WoWMap.Constants.MaxXY };
            bmin = new Vector3();

            bmax = new Vector3();

            bmin.X = origin[0] + (WoWMap.Constants.TileSize * X);
            bmin.Z = origin[2] + (WoWMap.Constants.TileSize * Y);
            bmax.X = origin[0] + (WoWMap.Constants.TileSize * (X + 1));
            bmax.Z = origin[2] + (WoWMap.Constants.TileSize * (Y + 1));
        }

        private void CalculateTileBounds(out float[] bmin, out float[] bmax)
        {
            var origin = new[] { -WoWMap.Constants.MaxXY, 0, -WoWMap.Constants.MaxXY };
            bmin = new float[3];
            bmax = new float[3];
            bmin[0] = origin[0] + (Constants.TileSize * X);
            bmin[2] = origin[2] + (Constants.TileSize * Y);
            bmax[0] = origin[0] + (Constants.TileSize * (X + 1));
            bmax[2] = origin[2] + (Constants.TileSize * (Y + 1));
          /*  bmax = bmax.ToRecast();
            bmin = bmin.ToRecast();*/
        }

        public byte[] Build()
        {
            Geometry = new Geometry.Geometry {Transform = true };
            {
                var main = new ADT(World, X, Y);
                main.Read();
               // main.Generate();
                Geometry.AddADT(main);
            }

            if (Geometry.Vertices.Count == 0 && Geometry.Indices.Count == 0)
                throw new InvalidOperationException("Can't build tile with empty geometry");

            float[] bbMin, bbMax;
            CalculateTileBounds(out bbMin, out bbMax);

            Geometry.CalculateMinMaxHeight(out bbMin[1], out bbMax[1]);


            // again, we load everything - wasteful but who cares
            /* for (int ty = Y - 1; ty <= Y + 1; ty++)
             {
                 for (int tx = X - 1; tx <= X + 1; tx++)
                 {
                     try
                     {
                         // don't load main tile again
                         if (tx == X && ty == Y)
                             continue;

                         var adt = new ADT(World, tx, ty);
                         adt.Read();
                         Geometry.AddADT(adt);
                     }
                     catch (FileNotFoundException)
                     {
                         // don't care - no file means no geometry
                     }
                 }
             }*/

            Context = new RecastContext();
          //  Context.SetContextHandler(Log);

            // get raw geometry - lots of slowness here
            float[] vertices;
            int[] triangles;
            byte[] areas;
            Geometry.GetRawData(out vertices, out triangles, out areas);
            Geometry.SaveWavefrontObject($"{World}_{X}_{Y}.obj");
            Geometry.Indices.Clear();
            Geometry.Vertices.Clear();

            // add border
            bbMin[0] -= Config.BorderSize * Config.CellSize;
            bbMin[2] -= Config.BorderSize * Config.CellSize;
            bbMax[0] += Config.BorderSize * Config.CellSize;
            bbMax[2] += Config.BorderSize * Config.CellSize;

            Heightfield hf;
            int width = Config.TileWidth + (Config.BorderSize * 2);
            if (!Context.CreateHeightfield(out hf, width, width, bbMin, bbMax, Config.CellSize, Config.CellHeight))
                throw new OutOfMemoryException("CreateHeightfield ran out of memory");
            Context.MarkWalkableTriangles(Config.WalkableSlopeAngle, ref vertices, ref triangles,out areas);
          //  Context.ClearUnwalkableTriangles(Config.WalkableSlopeAngle, ref vertices, ref triangles, areas);
            Context.RasterizeTriangles(ref vertices, ref triangles, ref areas, hf, Config.WalkableClimb);

            // Once all geometry is rasterized, we do initial pass of filtering to
            // remove unwanted overhangs caused by the conservative rasterization
            // as well as filter spans where the character cannot possibly stand.
            Context.FilterLowHangingWalkableObstacles(Config.WalkableClimb, hf);
            Context.FilterLedgeSpans(Config.WalkableHeight, Config.WalkableClimb, hf);
            Context.FilterWalkableLowHeightSpans(Config.WalkableHeight, hf);

            // Compact the heightfield so that it is faster to handle from now on.
            // This will result in more cache coherent data as well as the neighbours
            // between walkable cells will be calculated.
            CompactHeightfield chf;
            if (!Context.BuildCompactHeightfield(Config.WalkableHeight, Config.WalkableClimb, hf, out chf))
                throw new OutOfMemoryException("BuildCompactHeightfield ran out of memory");

            hf.Delete();

            // Erode the walkable area by agent radius.
            if (!Context.ErodeWalkableArea(Config.WalkableRadius, chf))
                throw new OutOfMemoryException("ErodeWalkableArea ran out of memory");

            // Prepare for region partitioning, by calculating distance field along the walkable surface.
            if (!Context.BuildDistanceField(chf))
                throw new OutOfMemoryException("BuildDistanceField ran out of memory");

            // Partition the walkable surface into simple regions without holes.
            if (!Context.BuildRegions(chf, Config.BorderSize, Config.MinRegionArea, Config.MergeRegionArea))
                throw new OutOfMemoryException("BuildRegionsMonotone ran out of memory");

            // Create contours.
            ContourSet cset;
            if (!Context.BuildContours(chf, Config.MaxSimplificationError, Config.MaxEdgeLength, out cset))
                throw new OutOfMemoryException("BuildContours ran out of memory");

            // Build polygon navmesh from the contours.
            PolyMesh pmesh;
            if (!Context.BuildPolyMesh(cset, Config.MaxVertsPerPoly, out pmesh))
                throw new OutOfMemoryException("BuildPolyMesh ran out of memory");

            // Build detail mesh.
            PolyMeshDetail dmesh;
            if (
                !Context.BuildPolyMeshDetail(pmesh, chf, Config.DetailSampleDistance, Config.DetailSampleMaxError,
                                             out dmesh))
                throw new OutOfMemoryException("BuildPolyMeshDetail ran out of memory");

            chf.Delete();
            cset.Delete();

            // Remove padding from the polymesh data. (Remove this odditity)
            pmesh.RemovePadding(Config.BorderSize);

            // Set flags according to area types (e.g. Swim for Water)
            pmesh.MarkAll();

            // get original bounds
            float[] tilebMin, tilebMax;
            CalculateTileBounds(out tilebMin, out tilebMax);
            tilebMin[1] = bbMin[1];
            tilebMax[1] = bbMax[1];

            // build off mesh connections for flightmasters
            // bMax and bMin are switched here because of the coordinate system transformation

            var connections = new List<OffMeshConnection>();


            byte[] tileData;
            if (!Detour.CreateNavMeshData(out tileData, pmesh, dmesh,
                                          X, Y, tilebMin, tilebMax,
                                          Config.WorldWalkableHeight, Config.WorldWalkableRadius,
                                          Config.WorldWalkableClimb, Config.CellSize,
                                          Config.CellHeight, Config.TileWidth,
                                          connections.ToArray()))
            {
                pmesh.Delete();
                dmesh.Delete();
                return null;
            }

            pmesh.Delete();
            dmesh.Delete();
            GC.Collect();
            return tileData;
        }
    }



}

