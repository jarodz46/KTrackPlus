using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MessagePack;

namespace KTrackPlus.Helpers
{
    [MessagePackObject]
    public class SimpleLocation
    {
        [Key(0)]
        public uint tt { get; set; }
        [Key(1)]
        public float lat { get; set; }
        [Key(2)]
        public float lng { get; set; }
        [Key(3)]
        public float alt { get; set; }

        public SimpleLocation(uint tt, float lat, float lng, float alt)
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

    [MessagePackObject]
    public class LocationsPack
    {
        [Key(0)]
        public List<SimpleLocation> locs { get; set; }

        public LocationsPack(List<SimpleLocation> locs)
        {
            this.locs = locs;
        }
    }
}
