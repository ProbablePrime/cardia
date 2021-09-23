using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MGT.HRM.HRP.BtValues
{
    public class HeartRateBtValue : IHRMPacket
    {
        public int HeartRate { get; set; } = 0;
        public bool HasExpendedEnergy = false;
        public int ExpendedEnergy = 0;
        public DateTimeOffset Timestamp;
        public int MinHeartRate = 0;
        public int MaxHeartRate = 0;

        public int SmoothedHeartRate = 0;

        public override string ToString()
        {
            return base.ToString() + "[ HeartRate = " + HeartRate + " ]";
        }
    }
}
