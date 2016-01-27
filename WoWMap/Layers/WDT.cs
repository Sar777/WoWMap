using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using WoWMap.Chunks;
using WoWMap.Geometry;
using WoWMap.Archive;

namespace WoWMap.Layers
{
    public class WDT
    {
        public WDT(string filename)
        {
            Data = new ChunkData(filename);
            Read();
            ReadGlobalModel();
        }

        public ChunkData Data { get; private set; }

        public bool IsValid { get; private set; }
        public bool[,] TileTable { get; private set; }
        public MAIN MAIN { get; private set; }

        public bool IsGlobalModel { get; private set; }
        public MWMO MWMO { get; private set; }
        public MODF MODF { get; private set; }
        public string ModelFile { get; private set; }
        public MODF.MODFEntry ModelDefinition { get; private set; }

        public bool HasTile(int x, int y)
        {
            return TileTable[x, y];
        }

        private void ReadGlobalModel()
        {
            var fileChunk = Data.GetChunkByName("MWMO");
            var defChunk = Data.GetChunkByName("MODF");
            if (fileChunk == null || defChunk == null)
                return;

            IsGlobalModel = true;
            ModelDefinition = MODF.MODFEntry.Read(new BinaryReader(defChunk.GetStream()));
            ModelFile = new BinaryReader(fileChunk.GetStream()).ReadCString();
        }

        public void Read()
        {
            foreach (var subChunk in Data.Chunks)
            {
                switch (subChunk.Name)
                {
                    case "MAIN":
                        MAIN = new MAIN(subChunk);

                        IsValid = true;

                        TileTable = new bool[64, 64];
                        for (int y = 0; y < 64; y++)
                            for (int x = 0; x < 64; x++)
                                TileTable[x, y] = MAIN.Entries[x, y].Flags.HasFlag(MAIN.MAINFlags.HasADT);                        
                        break;

                    case "MWMO":
                        MWMO = new MWMO(subChunk);    
                        break;

                    case "MODF":
                        MODF = new MODF(subChunk);
                        break;
                }
            }

            IsGlobalModel = (MODF != null && MWMO != null);
        }
    }
}
