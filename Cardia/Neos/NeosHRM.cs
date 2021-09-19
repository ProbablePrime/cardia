using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MGT.Cardia.Neos
{
    public class NeosHRM
    {
        private int _heartRate;
        public int HeartRate
        {
            get => _heartRate;
            set
            {
                Samples.Enqueue(value);
                _heartRate = value;
                OnUpdate.Invoke(this.ToString());
            }
        }
        public int HearRateVariance { get; private set; }
        public int BatteryLevel;
        public bool Connected;

        public Action<string> OnUpdate;

        public Queue<int> Samples = new Queue<int>(20);

        public override string ToString()
        {
            return String.Format("{0},{1},{2},{3}", HeartRate, HearRateVariance, BatteryLevel, Connected ? 1 : 0);
        }


    }
}
