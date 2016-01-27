﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using WoWMap.Geometry;
using SharpDX;

namespace WoWMap
{
    public static class Helpers
    {
        public static Dictionary<uint, string> GetIndexedStrings(byte[] data)
        {
            var ret = new Dictionary<uint, string>();

            var sb = new StringBuilder();
            var offset = 0u;
            for (uint i = 0; i < data.Length; i++)
            {
                if (data[i] == '\0') // Terminate string
                {
                    if (sb.Length > 1)
                        ret.Add(offset, sb.ToString());
                    offset = i+1;
                    sb = new StringBuilder();

                    continue;
                }

                sb.Append((char)data[i]);
            }

            return ret;
        }

        public static string ReadCString(byte[] data)
        {
            var sb = new StringBuilder(0x100);
            for (int i = 0; i < data.Length; i++)
            {
                if (data[i] == '\0') // Terminate string
                    if (sb.Length > 1) break;

                sb.Append((char)data[i]);
            }
            return sb.ToString();
        }

        public static string ReadCString(this BinaryReader br)
        {
            var buffer = new List<byte>();
            byte b = 0;
            while ((b = br.ReadByte()) != 0)
                buffer.Add(b);

            if (buffer.Count <= 0)
                return null;

            return Encoding.ASCII.GetString(buffer.ToArray());
        }

        public static Vector3 ReadVector3(this BinaryReader br)
        {
            return new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
        }

        /* public static SharpNav.Vector3 ToV3(this SharpDX.Vector3 v)
         {
             return new SharpNav.Vector3(v.X, v.Y, v.Z);
         }*/


        public static float[] ToRecast(this float[] val)
        {
            return new[] { -val[1], val[2], -val[0] };
        }

        public static float[] ToWoW(this float[] val)
        {
            return new[] { -val[2], -val[0], val[1] };
        }


        public static float[] ToFloatArray(this Vector3 v)
        {
            return new[] { v.X, v.Y, v.Z };
        }

        public static Vector3 ToWoW(this Vector3 v)
        {
            return new Vector3(-v.Z, -v.X, v.Y);
        }

        public static Vector3 ToRecast(this Vector3 v)
        {
            return new Vector3(-v.Y, v.Z, -v.X);
        }

        public static float ToRadians(this float angle)
        {
            return (float)(Math.PI / 180) * angle;
        }

        public static float[] Origin = new[] { -17066.666f, 0, -17066.666f };

        public static float TileSize
        {
            get { return 533.33333f; }
        }

    }
}
