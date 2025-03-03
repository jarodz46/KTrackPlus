﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Android.Health.Connect.DataTypes;
using MessagePack;

namespace KTrackPlus.Helpers
{
    public abstract class SendObject
    {
        public abstract byte[] ToByteArray();
    }

    [MessagePackObject]
    public class BaseLocation : SendObject
    {
        [Key(1)]
        public float lat { get; set; }
        [Key(2)]
        public float lng { get; set; }

        public BaseLocation(float lat, float lng)
        {
            this.lat = lat;
            this.lng = lng;
        }

        public override string ToString()
        {
            return lat + " - " + lng;
        }

        public override byte[] ToByteArray()
        {
            byte[] latBytes = BitConverter.GetBytes(lat);
            byte[] longBytes = BitConverter.GetBytes(lng);
            return latBytes.Concat(longBytes).ToArray();
        }

        public double DistanceTo(BaseLocation loc)
        {
            return Common.CalculateDistance(this, loc);
        }
    }

    [MessagePackObject]
    public class SimpleLocation : BaseLocation
    {
        [Key(0)]
        public uint tt { get; set; }
        [Key(3)]
        public float alt { get; set; }

        public SimpleLocation(uint tt, float lat, float lng, float alt) : base(lat, lng)
        {
            this.tt = tt;
            this.alt = alt;
        }

        public override string ToString()
        {
            return tt + ": " + lat + " - " + lng + " - " + alt;
        }

        public override byte[] ToByteArray()
        {
            byte[] ttBytes = BitConverter.GetBytes(tt);
            byte[] latBytes = BitConverter.GetBytes(lat);
            byte[] longBytes = BitConverter.GetBytes(lng);
            byte[] altBytes = BitConverter.GetBytes(alt);
            return ttBytes.Concat(latBytes).Concat(longBytes).Concat(altBytes).ToArray();
        }
    }

    [MessagePackObject]
    public class ObjectsPack<T> where T : SendObject
    {
        [Key(0)]
        public List<T> objs { get; set; }

        public ObjectsPack(List<T> locs)
        {
            this.objs = locs;
        }
    }
}
