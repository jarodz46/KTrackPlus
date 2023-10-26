using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KTrackPlus.Helpers
{
    internal class Stats
    {
        public float ascend { get; set; } = 0;
        public float distance { get; set; } = 0;
        public uint rideTime { get; set; } = 0;
        public float avgSpeed { get; set; } = 0;


        public bool updated { get; set; } = false;

        int count = 0;
        float? lastAlt = null;
        public void Push(float? currentAlt, float? distance, uint? rideTime, float? speed)
        {
            if (currentAlt != null)
            {
                if (lastAlt == null)
                {
                    lastAlt = (float)currentAlt;
                }
                else
                {
                    var diff = (float)currentAlt - (float)lastAlt;
                    if (diff > 0)
                        ascend += diff;
                    lastAlt = (float)currentAlt;
                }
            }

            if (distance != null)
                this.distance = (float)distance;
            if (rideTime != null)
                this.rideTime = (uint)rideTime;

            if (speed != null)
            {
                avgSpeed = (avgSpeed * count + (float)speed) / (count + 1);
                count++;
            }

        }

        SimpleLocation? lastLoc;
        public void Push(SimpleLocation newLoc)
        {
            if (lastLoc == null)
            {
                lastLoc = newLoc;
            }
            else
            {
                var timeDiff = newLoc.tt - lastLoc.tt;
                rideTime += timeDiff;
                var dist = (float)newLoc.DistanceTo(lastLoc);
                distance += dist;
                var diffAlt = newLoc.alt - lastLoc.alt;
                if (diffAlt > 0 )
                    ascend += diffAlt;
                var speed = dist / timeDiff;
                avgSpeed = (avgSpeed * count + (float)speed) / (count + 1);
                count++;
            }
        }

        public void Reset()
        {
            ascend = 0;
            distance = 0;
            avgSpeed = 0;
            rideTime = 0;
        }


        public byte[] ToBytesArray()
        {
            var bRideTime = BitConverter.GetBytes(rideTime);
            var bAscend = BitConverter.GetBytes(ascend);
            var bDistance = BitConverter.GetBytes(distance);
            var bAvgSpeed = BitConverter.GetBytes(avgSpeed);
            return bRideTime.Concat(bAscend).Concat(bDistance).Concat(bAvgSpeed).ToArray();
        }

        public override string ToString()
        {
            return rideTime + " dist : " + distance + " ascend : " + ascend + " avgspeed : " + avgSpeed;
        }
    }
}
