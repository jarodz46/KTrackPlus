using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KTrackPlus.Helpers
{
    public class SimpleLocation
    {
        public uint tt { get; set; }
        public float lat { get; set; }
        public float lng { get; set; }
        public float alt { get; set; }

        public SimpleLocation(float lat, float lng, uint tt, float alt)
        {
            this.lat = lat;
            this.lng = lng;
            this.tt = tt;
            this.alt = alt;
        }

        public byte[] ToByeArray()
        {
            byte[] ttBytes = BitConverter.GetBytes(tt);
            byte[] latBytes = BitConverter.GetBytes(lat);
            byte[] longBytes = BitConverter.GetBytes(lng);
            byte[] altBytes = BitConverter.GetBytes(alt);
            return ttBytes.Concat(latBytes).Concat(longBytes).Concat(altBytes).ToArray();
        }

        public double DistanceTo(SimpleLocation loc)
        {
            return Common.CalculateDistance(this, loc);
        }
    }
}
