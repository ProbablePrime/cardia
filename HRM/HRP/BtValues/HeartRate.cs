using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MGT.HRM.HRP.BtValues
{
    public class HeartRate
    {
        public int Value;
        public bool HasExpendedEnergy;
        public int ExpendedEnergy;
        public DateTimeOffset Timestamp;
        public int MinHeartRate;
        public int MaxHeartRate;

        public int SmoothedHeartRate;
    }
}
