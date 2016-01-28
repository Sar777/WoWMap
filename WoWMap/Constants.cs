using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WoWMap
{
    public static class Constants
    {
        public const float TileSize = 533.0f + (1 / (float)3);
        public const float MaxXY = 32.0f * TileSize;
        public const float ChunkSize = TileSize / 16.0f;
        public const float UnitSize = ChunkSize / 8.0f;
        //public const float ChunkRadius = (float)Math.Sqrt(2.0) * ChunkSize;
    }
}
