using SharpDX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WoWMapPather
{
    public enum ConnectionType
    {
        Waypoint,
        Flightmaster,
    }

    public class WoWMapConnection
    {
        public ConnectionType Type { get; set; }
        public Vector3 Location { get; set; }

        /// <summary>
        /// Only valid for hops with Flightmaster type
        /// </summary>
        public string FlightTarget { get; set; }
    }
}
