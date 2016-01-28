using DetourLayer;
using SharpDX;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace WoWMapPather
{
    public class WowMapPather
    {
        private readonly NavMesh _mesh;
        private readonly NavMeshQuery _query;
        private readonly string _meshPath;
        public readonly bool IsDungeon;
        public QueryFilter Filter { get; private set; }
        public string Continent { get; private set; }
        public string fullFileName { get; private set; }


        public WowMapPather(string continent)
        {

            fullFileName = continent;
            Continent = continent.Substring(continent.LastIndexOf('\\') + 1);

            if (Directory.Exists(continent))
                _meshPath = continent;
            else
            {
                var assembly = Assembly.GetCallingAssembly().Location;
                var dir = Path.GetDirectoryName(assembly);
                if (Directory.Exists(dir + "\\Meshes"))
                    _meshPath = dir + "\\Meshes\\" + continent;
                else
                    _meshPath = dir + "\\" + continent;
            }

            if (!Directory.Exists(_meshPath))
                throw new Exception("No mesh for " + continent + " (Path: " + _meshPath + ")");

            _mesh = new NavMesh();
            DetourStatus status;

            // check if this is a dungeon and initialize our mesh accordingly
            string dungeonPath = GetDungeonPath();
            if (File.Exists(dungeonPath))
            {
                var data = File.ReadAllBytes(dungeonPath);
                status = _mesh.Initialize(data);
                IsDungeon = true;
            }
            else
                status = _mesh.Initialize(32768, 4096, Utility.Origin, Utility.TileSize, Utility.TileSize);

            if (status.HasFailed())
                throw new Exception("Failed to initialize the mesh");

            // _query = new NavMeshQuery(new PatherCallback(this));
            //TODO: Add Callback for Dynamic Tile Loading
            _query = new NavMeshQuery();
             _query.Initialize(_mesh, 65536);
            Filter = new QueryFilter { IncludeFlags = 0xFFFF, ExcludeFlags = 0x0 };
        }

        private string GetDungeonPath()
        {
            return _meshPath + "\\" + Continent + ".dmesh";
        }

        public NavMesh Mesh
        {
            get { return _mesh; }
        }

        public NavMeshQuery Query
        {
            get { return _query; }
        }

        public string GetTilePath(int x, int y)
        {
            return _meshPath + "\\" + Continent + "_" + x + "_" + y + ".tile";
        }

        public void GetTileByLocation(Vector3 loc, out int x, out int y)
        {
            CheckDungeon();

            var input = loc.ToRecast().ToFloatArray();
            float fx, fy;
            GetTileByLocation(input, out fx, out fy);
            x = (int)Math.Floor(fx);
            y = (int)Math.Floor(fy);
        }

        public static void GetTileByLocation(float[] loc, out float x, out float y)
        {
            x = (loc[0] - Utility.Origin[0]) / Utility.TileSize;
            y = (loc[2] - Utility.Origin[2]) / Utility.TileSize;
        }

        public void LoadAllTiles()
        {
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    if (!File.Exists(GetTilePath(x, y)))
                        continue;

                    LoadTile(x, y);
                }
            }
        }

        public void LoadAround(Vector3 loc, int extent)
        {
            CheckDungeon();

            int tx, ty;
            GetTileByLocation(loc, out tx, out ty);
            for (int y = ty - extent; y <= ty + extent; y++)
            {
                for (int x = tx - extent; x <= tx + extent; x++)
                {
                    LoadTile(x, y);
                }
            }
        }
        public bool LoadTile(byte[] data)
        {
            CheckDungeon();

            MeshTile tile;
            if (_mesh.AddTile(data, out tile).HasFailed())
                return false;
    
            return true;
        }
        private void CheckDungeon()
        {
            if (IsDungeon)
                throw new Exception( "Dungeon mesh doesn't support tiles");
        }

        public bool LoadTile(int x, int y)
        {
            CheckDungeon();

            if (_mesh.HasTileAt(x, y,0))
                return true;
            var path = GetTilePath(x, y);
            if (!File.Exists(path))
                return false;
            var data = File.ReadAllBytes(path);
            return LoadTile(data);
        }

        public bool RemoveTile(int x, int y, out byte[] tileData)
        {
            return _mesh.RemoveTileAt(x, y, 0,out tileData).HasSucceeded();
        }

        public bool RemoveTile(int x, int y)
        {
            return _mesh.RemoveTileAt(x, y,0).HasSucceeded();
        }
        /*for (int i = 0; i < finalPath.Length/3; i++)
            {
                Console.WriteLine("X=" + finalPath[(i * 3) + 0] +", Y=" + finalPath[(i * 3) +1] +", Z=" + finalPath[(i * 3) +2]);
            }*/
        public List<WoWMapConnection> FindPath(Vector3 startVec, Vector3 endVec)
        {
            var extents = new Vector3(2.5f, 2.5f, 2.5f).ToFloatArray();
            var start = startVec.ToRecast().ToFloatArray();
            var end = endVec.ToRecast().ToFloatArray();

            if (!IsDungeon)
            {
                LoadAround(startVec, 1);
                LoadAround(endVec, 1);
            }

            var startRef = _query.FindNearestPolygon(start, extents, Filter);
            if (startRef == 0)
                throw new Exception("No polyref found for start");

            var endRef = _query.FindNearestPolygon(end, extents, Filter);
            if (endRef == 0)
                throw new Exception( "No polyref found for end");

            ulong[] pathCorridor;
            var status = _query.FindPath(startRef, endRef, start, end, Filter, out pathCorridor);
            if (status.HasFailed() || pathCorridor == null)
                throw new Exception( "FindPath failed, start: " + startRef + " end: " + endRef);

            if (status.HasFlag(DetourStatus.PartialResult))
                Console.WriteLine("Warning, partial result: " + status);

            float[] finalPath;
            StraightPathFlag[] pathFlags;
            ulong[] pathRefs;
            status = _query.FindStraightPath(start, end, pathCorridor, out finalPath, out pathFlags, out pathRefs);
            if (status.HasFailed() || (finalPath == null || pathFlags == null || pathRefs == null))
                throw new Exception("FindStraightPath failed, refs in corridor: " + pathCorridor.Length);

            var ret = new List<WoWMapConnection>(finalPath.Length / 3);
            for (int i = 0; i < (finalPath.Length / 3); i++)
            {
                if (pathFlags[i].HasFlag(StraightPathFlag.OffMeshConnection))
                {
                    var polyRef = pathRefs[i];
                    MeshTile tile;
                    Poly poly;
                    if (_mesh.GetTileAndPolyByRef(polyRef, out tile, out poly).HasFailed() || (poly == null || tile == null))
                        throw new Exception( "FindStraightPath returned a hop with an unresolvable off-mesh connection");

                    int polyIndex = _mesh.DecodePolyIndex(polyRef);
                    int pathId = -1;
                    for (int j = 0; j < tile.Header.OffMeshConCount; j++)
                    {
                        var con = tile.GetOffMeshConnection(j);
                        if (con == null)
                            continue;

                        if (con.PolyId == polyIndex)
                        {
                            pathId = (int)con.UserID;
                            break;
                        }
                    }

                    if (pathId == -1)
                        throw new Exception("FindStraightPath returned a hop with an poly that lacks a matching off-mesh connection");
                    ret.Add(BuildFlightmasterHop(pathId));
                }
                else
                {

                    var hop = new WoWMapConnection
                    {
                        Location =
                                          new Vector3(finalPath[(i * 3) + 0], finalPath[(i * 3) + 1], finalPath[(i * 3) + 2]).
                                          ToWoW(),
                        Type = ConnectionType.Waypoint
                    };

                    ret.Add(hop);
                }
            }

            return ret;
        }
        private static WoWMapConnection BuildFlightmasterHop(int pathId)
        {
            return null;
           /* var path = TaxiHelper.GetPath(pathId);
            if (path == null)
                throw new NavMeshException(DetourStatus.Failure, "FindStraightPath returned a hop with an invalid path id");

            var from = TaxiHelper.GetNode(path.From);
            var to = TaxiHelper.GetNode(path.To);
            if (from == null || to == null)
                throw new NavMeshException(DetourStatus.Failure, "FindStraightPath returned a hop with unresolvable flight path");

            return new Hop
            {
                Location = from.Location,
                FlightTarget = to.Name,
                Type = HopType.Flightmaster
            };*/
        }


    }
}
