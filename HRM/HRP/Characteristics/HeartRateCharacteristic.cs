using MGT.HRM.HRP.BtValues;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;

namespace MGT.HRM.HRP.Characteristics
{
    public class HeartRateCharacteristic : Characteristic<HeartRateBtValue>
    {
        private const byte HEART_RATE_VALUE_FORMAT = 0x01;
        private const byte ENERGY_EXPANDED_STATUS = 0x08;

        private Queue<int> SmoothingData = new Queue<int>(5);

        public HeartRateCharacteristic(GattDeviceService service) : base(service)
        {

        }

        protected override void GattCharacteristicValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            byte[] data = new byte[args.CharacteristicValue.Length];

            DataReader.FromBuffer(args.CharacteristicValue).ReadBytes(data);

            //logger.Debug($"Processing HRP payload, data = {data}");

            byte currentOffset = 0;
            byte flags = data[currentOffset];
            bool isHeartRateValueSizeLong = ((flags & HEART_RATE_VALUE_FORMAT) != 0);
            bool hasEnergyExpended = ((flags & ENERGY_EXPANDED_STATUS) != 0);

            currentOffset++;

            ushort heartRateMeasurementValue = 0;

            if (isHeartRateValueSizeLong)
            {
                heartRateMeasurementValue = (ushort)((data[currentOffset + 1] << 8) + data[currentOffset]);
                currentOffset += 2;
            }
            else
            {
                heartRateMeasurementValue = data[currentOffset];
                currentOffset++;
            }

            // TODO: What is this? 
            ushort expendedEnergyValue = 0;

            if (hasEnergyExpended)
            {
                expendedEnergyValue = (ushort)((data[currentOffset + 1] << 8) + data[currentOffset]);
                currentOffset += 2;
            }

            SmoothingData.Enqueue(heartRateMeasurementValue);


            var hr = new HeartRateBtValue
            {
                HeartRate = heartRateMeasurementValue,
                HasExpendedEnergy = hasEnergyExpended,
                ExpendedEnergy = expendedEnergyValue,
                Timestamp = args.Timestamp,
                MinHeartRate = Math.Min(LastValue.HeartRate, heartRateMeasurementValue),
                MaxHeartRate = Math.Max(LastValue.HeartRate, heartRateMeasurementValue),
                SmoothedHeartRate = (int)SmoothingData.Average()
            };
            LastValue = Value;
            Value = hr;
            this.ValueUpdated.Invoke(Value);
            // logger.Debug($"Constructed HRP packet = {btHrpPacket}");
        }

        
    }
}
