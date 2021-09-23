using log4net;
using MGT.HRM.HRP.BtValues;
using MGT.HRM.HRP.Characteristics;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Timers;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Devices.Enumeration.Pnp;
using Windows.Storage.Streams;

namespace MGT.HRM.HRP
{
    public class BtHrp : HeartRateMonitor
    {
        private static readonly ILog logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private DeviceInformation device;
        private String deviceContainerId;

        private GattDeviceService service;

        private PnpObjectWatcher watcher;
        private const GattClientCharacteristicConfigurationDescriptorValue CHARACTERISTIC_NOTIFICATION_TYPE = GattClientCharacteristicConfigurationDescriptorValue.Notify;

        private HeartRateCharacteristic HeartRateCharacteristic;

        private int initDelay = 500;

        public override string Name
        {
            get { return "Bluetooth Smart HRP"; }
        }

        private static TimeSpan START_TIMEOUT = new TimeSpan(0, 0, 10);
        private static TimeSpan RUN_TIMEOUT = new TimeSpan(0, 0, 30);
        private System.Timers.Timer timeoutTimer = new System.Timers.Timer(1000);
        private DateTime lastReceivedDate;

        public BtHrp()
        {
            Running = false;

            TotalPackets = 0;
            CorruptedPackets = 0;

            timeoutTimer.Elapsed += timeoutTimer_Elapsed;
        }


        public override int TotalPackets { get; protected set; }
        public override int CorruptedPackets { get; protected set; }

        public override bool Running { get; protected set; }

        public delegate void DeviceChangedEventHandler(object sender, DeviceInformation device);
        public event DeviceChangedEventHandler DeviceChanged;
        public DeviceInformation Device
        {
            get
            {
                return device;
            }
            set
            {
                DeviceInformation backup = Device;

                device = value;

                deviceContainerId = "{" + device.Properties["System.Devices.ContainerId"] + "}";

                if (backup == null && value == null)
                    return;

                if ((backup == null && value != null) || !backup.Equals(value))
                    if (DeviceChanged != null)
                        DeviceChanged(this, value);
            }
        }
        

        public override IHRMPacket LastPacket { get => HeartRateCharacteristic.LastValue; protected set => throw new NotImplementedException(); }
        public override int HeartBeats { get => HeartRateCharacteristic.Packets; protected set => throw new NotImplementedException(); }
        public override byte? MinHeartRate { get => (byte)HeartRateCharacteristic.Value.MinHeartRate; protected set => throw new NotImplementedException(); }
        public override byte? MaxHeartRate { get => (byte)HeartRateCharacteristic.Value.MaxHeartRate; protected set => throw new NotImplementedException(); }

        // TODO
        private int smooth;
        public override int HeartRateSmoothingFactor { get => 1; set => smooth = value; }
        public override double SmoothedHeartRate { get => HeartRateCharacteristic.Value.SmoothedHeartRate; protected set => throw new NotImplementedException(); }

        public override async void Start()
        {
            if (Running)
                return;

            logger.Debug("Starting HRP");

            lastReceivedDate = DateTime.Now;

            timeoutTimer.Start();
            Running = true;

            await SetupCharacteristicsAsync();
        }

        private async Task LogAllCharacteristics()
        {
            // List all the characteristics of the device
            logger.Debug("Getting all GattCharacteristic...");
            GattCharacteristicsResult allResult = await service.GetCharacteristicsAsync();
            logger.Debug($"GattCharacteristicsResult status {allResult.Status}");
            foreach (GattCharacteristic allCharacteristic in allResult.Characteristics)
            {
                logger.Debug($"GattCharacteristic {allCharacteristic.Uuid}: " +
                 $"description = {allCharacteristic.UserDescription}, " +
                 $"protection level = {allCharacteristic.ProtectionLevel}");
            }
        }

        private async Task SetupCharacteristicsAsync()
        {          
            try
            {
                logger.Debug($"Getting GattDeviceService {device.Name} with id {device.Id}");

                if (initDelay > 0)
                    await Task.Delay(initDelay);

                service = await GattDeviceService.FromIdAsync(device.Id);

                if (service == null)
                {
                    logger.Debug($"Failed to instantiate GattDeviceService");
                    return;
                }
                logger.Debug($"GattDeviceService instatiated successfully");

                logger.Debug($"GattSession status = {service.Session.SessionStatus}, " +
                    $"mantain connection = {service.Session.MaintainConnection}, " +
                    $"can mantain connection = {service.Session.MaintainConnection}");

                HeartRateCharacteristic = new HeartRateCharacteristic(service);
                await HeartRateCharacteristic.Setup();
                HeartRateCharacteristic.ValueUpdated += ElevateProcessedPacket;
            }
            catch (Exception e)
            {
                logger.Warn("Error configuring HRP device", e);

                Stop();
                FireTimeout("Bluetooth HRP device initialization failed");
                //throw new Exception("Bluetooth HRP device initialization failed");
            }
        }

        private void ElevateProcessedPacket(HeartRateBtValue value)
        {
            logger.Debug($"Firing PacketProcessed event, packet = {value}");
            lastReceivedDate = DateTime.Now;
            PacketProcessedEventArgs args = new PacketProcessedEventArgs(value);
            base.FirePacketProcessed(args);
        }

        private void StartDeviceConnectionWatcher()
        {
            watcher = PnpObject.CreateWatcher(PnpObjectType.DeviceContainer,
                new string[] { "System.Devices.Connected" }, String.Empty);

            logger.Debug("Registering device connection watcher updated event handler");

            watcher.Updated += DeviceConnection_Updated;

            logger.Debug("Starting device connection watcher");

            watcher.Start();
        }

        private async void DeviceConnection_Updated(PnpObjectWatcher sender, PnpObjectUpdate args)
        {

            logger.Debug($"Device connection updated, args = {args}");

            var connectedProperty = args.Properties["System.Devices.Connected"];

            logger.Debug($"Connected property, args = {connectedProperty}");

            bool isConnected = false;
            if ((deviceContainerId == args.Id) && Boolean.TryParse(connectedProperty.ToString(), out isConnected) &&
                isConnected)
            {
                //var status = await actualHRCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                  //  CHARACTERISTIC_NOTIFICATION_TYPE);

               // if (status == GattCommunicationStatus.Success)
                //{
                  //  logger.Debug("Stopping device connection watcher");

                    // Once the Client Characteristic Configuration Descriptor is set, the watcher is no longer required
                    //watcher.Stop();
                    //watcher = null;

                    //logger.Debug("Configuration successfull");
               // }
            }
        }

        void timeoutTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            TimeSpan timeout;
            if (Running)
                timeout = RUN_TIMEOUT;
            else
                timeout = START_TIMEOUT;

            TimeSpan diff = DateTime.Now - lastReceivedDate;
            if (diff > timeout)
            {
                if (Running)
                    logger.Debug("Communication timeout elapsed");
                else
                    logger.Debug("Start timeout elapsed");

                Stop();
                FireTimeout("Bluetooth HRP device not transmitting");
            }
        }

        public override void Stop()
        {
            if (Running)
            {
                logger.Debug("Stopping HRP");

                Running = false;

                logger.Debug("Stopping timeout timer");

                timeoutTimer.Stop();

                // Call Stop on All Characteristics
                if (HeartRateCharacteristic != null)
                    HeartRateCharacteristic.Stop();

                if (watcher != null)
                {
                    logger.Debug("Clearing device changed watcher");

                    watcher.Stop();
                    watcher = null;
                }

                if (service != null)
                {

                    logger.Debug("Clearing GattDeviceService");

                    service.Dispose();
                    service = null;
                }
            }
        }

        public override void Reset()
        {

            logger.Debug("Resetting HRP");

            Stop();
            Start();

            //TODO: Reset Characteristics
        }

        public override void Dispose()
        {
            Stop();
        }
    }
}
