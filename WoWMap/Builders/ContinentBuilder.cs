
using DetourLayer;
using SharpDX;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;
using WoWMap.Layers;

namespace WoWMap.Builders
{
    public enum TileEventType
    {
        StartedBuild,
        CompletedBuild,
        FailedBuild,
    }

    public class TileEvent : EventArgs
    {
        public string Continent { get; private set; }
        public int X { get; private set; }
        public int Y { get; private set; }
        public TileEventType Type { get; private set; }

        public TileEvent(string continent, int x, int y, TileEventType type)
        {
            Continent = continent;
            X = x;
            Y = y;
            Type = type;
        }
    }

    public class ContinentBuilder
    {
        public string Continent { get; private set; }
        public WDT TileMap { get; private set; }

        public int StartX { get; private set; }
        public int StartY { get; private set; }
        public int CountX { get; private set; }
        public int CountY { get; private set; }

        public event EventHandler<TileEvent> OnTileEvent;

        public ContinentBuilder(string continent)
            : this(continent, 0, 0, 64, 64)
        {
        }

        public ContinentBuilder(string continent, int startX, int startY, int countX, int countY)
        {
            StartX = startX;
            StartY = startY;
            CountX = countX;
            CountY = countY;

            Continent = continent;
            TileMap = new WDT("World\\Maps\\" + continent + "\\" + continent + ".wdt");
        }

        private string GetTilePath(int x, int y, int phaseId)
        {
            return Continent + "\\" + Continent + "_" + x + "_" + y + "_" + phaseId + ".tile";
        }

        private string GetTilePath(int x, int y)
        {
            return Continent + "\\" + Continent + "_" + x + "_" + y + ".tile";
        }

        private void SaveTile(int x, int y, byte[] data)
        {
            File.WriteAllBytes(Continent + "\\" + Continent + "_" + x + "_" + y + ".tile", data);
        }

        private void Report(int x, int y, TileEventType type)
        {
            if (OnTileEvent != null)
                OnTileEvent(this, new TileEvent(Continent, x, y, type));
        }
        public void TestNavMesh(byte[] data)
        {

            var extents = new Vector3(2.5f, 2.5f, 2.5f).ToFloatArray();


            // var startVec = new Vector3(-9467.8f, 64.2f, 55.9f);
            //var endVec = new Vector3(-9248.9f, -93.35f, 70.3f);


            //Vector3 startVec = new Vector3(1672.2f, 1662.9f, 139.2f);
            //Vector3 startVec = new Vector3(1665.2f, 1678.2f, 120.5f);

            Vector3 startVec = new Vector3 ( -8949.95f, -132.493f, 83.5312f );
            Vector3 endVec = new Vector3 ( -9046.507f, -45.71962f, 88.33186f );

            var start = startVec.ToRecast().ToFloatArray();
            var end = endVec.ToRecast().ToFloatArray();

            NavMesh _mesh = new NavMesh();
            _mesh.Initialize(32768, 4096, Helpers.Origin, Helpers.TileSize, Helpers.TileSize);
            var meshData = data;
            MeshTile tile;
            _mesh.AddTile(data, out tile);
            NavMeshQuery _query = new NavMeshQuery();
            _query.Initialize(_mesh, 65536);
            QueryFilter Filter = new QueryFilter { IncludeFlags = 0xFFFF, ExcludeFlags = 0x0 };

            var startRef = _query.FindNearestPolygon(start, extents, Filter);
           

            var endRef = _query.FindNearestPolygon(end, extents, Filter);


            uint[] pathCorridor;
            var status = _query.FindPath(startRef, endRef, start, end, Filter, out pathCorridor);
            if (status.Equals(DetourStatus.Failure) || pathCorridor == null)
                throw new Exception("FindPath failed, start: " + startRef + " end: " + endRef);

            if (status.HasFlag(DetourStatus.PartialResult))
                Console.WriteLine("Warning, partial result: " + status);

            float[] finalPath;
            StraightPathFlag[] pathFlags;
            uint[] pathRefs;
            status = _query.FindStraightPath(start, end, pathCorridor, out finalPath, out pathFlags, out pathRefs);
            if (status.Equals(DetourStatus.Failure) || (finalPath == null || pathFlags == null || pathRefs == null))
                throw new Exception("FindStraightPath failed, refs in corridor: " + pathCorridor.Length);

            

        }


        public void Build()
        {
            if (Directory.Exists(Continent))
                Directory.Delete(Continent, true);

            Directory.CreateDirectory(Continent);

            for (int y = StartY; y < (StartY + CountY); y++)
            {
                for (int x = StartX; x < (StartX + CountX); x++)
                {
                    if (!TileMap.HasTile(x, y))
                        continue;

                    if (File.Exists(GetTilePath(x, y)))
                    {
                        Report(x, y, TileEventType.CompletedBuild);
                        continue;
                    }

                    Report(x, y, TileEventType.StartedBuild);

                    var builder = new TileBuilder(Continent, x, y);
                    byte[] data = null;


                    data = builder.Build();
                    SaveTile(x, y, data);
                    TestNavMesh(data);



                    if (data == null)
                        Report(x, y, TileEventType.FailedBuild);
                    else
                    {
                        SaveTile(x, y, data);
                        Report(x, y, TileEventType.CompletedBuild);
                    }

                    
                }
            }
        }
    }
}
