using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth.GenericAttributeProfile;

namespace MGT.HRM.HRP.Characteristics
{
    public abstract class Characteristic<T>
    {
        private static GattClientCharacteristicConfigurationDescriptorValue CHARACTERISTIC_NOTIFICATION_TYPE = GattClientCharacteristicConfigurationDescriptorValue.Notify;

        public Guid TargetGuid;
        public int Index = 0;

        private GattCharacteristic GattCharacteristic;

        public T Value;

        T lastValue;

        
        public T LastValue
        {
            get
            {
                return lastValue;
            }
            protected set
            {
                LastValue = value;
            }
        }

        public int Packets
        {
            get;
            protected set;
        }

        public Action<T> ValueUpdated;

        protected GattDeviceService Service;

        private void GattCharacteristicValueChangedWrapper(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            this.GattCharacteristicValueChanged(sender, args);
            this.Packets += 1;
        }
        protected abstract void GattCharacteristicValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args);

        public Characteristic(GattDeviceService service) {
            this.Service = service;
        }

        public override string ToString()
        {
            return string.Format("Characteristic:{0}, Index:{2}", this.TargetGuid, this.Index);
        }

        public async Task Setup()
        {
            GattCharacteristicsResult characteristics = await this.Service.GetCharacteristicsForUuidAsync(this.TargetGuid);
            if (characteristics.Status != GattCommunicationStatus.Success)
            {
                //TODO
                throw new Exception("Can't get characteristic");
            }
            if (characteristics.Characteristics.Count == 0)
            {
                throw new Exception("Can't get characteristic");
            }
            if (characteristics.Characteristics[this.Index] == null)
            {
                throw new Exception("Can't get characteristic");
            }
            this.GattCharacteristic = characteristics.Characteristics[this.Index];

            // While encryption is not required by all devices, if encryption is supported by the device,
            // it can be enabled by setting the ProtectionLevel property of the Characteristic object.
            // All subsequent operations on the characteristic will work over an encrypted link.
            //logger.Debug("Setting EncryptionRequired protection level on GattCharacteristic");

            this.GattCharacteristic.ProtectionLevel = GattProtectionLevel.EncryptionRequired;

            // Register the event handler for receiving notifications
            // if (initDelay > 0)
            // await Task.Delay(initDelay);

            //logger.Debug("Registering event handler onction level on GattCharacteristic");

            this.GattCharacteristic.ValueChanged += GattCharacteristicValueChangedWrapper;

            // In order to avoid unnecessary communication with the device, determine if the device is already 
            // correctly configured to send notifications.
            // By default ReadClientCharacteristicConfigurationDescriptorAsync will attempt to get the current
            // value from the system cache and communication with the device is not typically required.

            //logger.Debug("Reading GattCharacteristic configuration descriptor");

            var currentDescriptorValue = await this.GattCharacteristic.ReadClientCharacteristicConfigurationDescriptorAsync();

            if ((currentDescriptorValue.Status != GattCommunicationStatus.Success) ||
                (currentDescriptorValue.ClientCharacteristicConfigurationDescriptor != CHARACTERISTIC_NOTIFICATION_TYPE))
            {
                // Set the Client Characteristic Configuration Descriptor to enable the device to send notifications
                // when the Characteristic value changes

                //logger.Debug("Setting GattCharacteristic configuration descriptor to enable notifications");

                GattCommunicationStatus status =
                    await this.GattCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                    CHARACTERISTIC_NOTIFICATION_TYPE);
            }
    }
}
