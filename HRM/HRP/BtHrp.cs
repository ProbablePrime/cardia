using log4net;
using System;
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
        private GattCharacteristic actualHRCharacteristic;
        private PnpObjectWatcher watcher;
        private const GattClientCharacteristicConfigurationDescriptorValue CHARACTERISTIC_NOTIFICATION_TYPE = GattClientCharacteristicConfigurationDescriptorValue.Notify;

        // Heart Rate devices typically have only one Heart Rate Measurement characteristic.
        // Make sure to check your device's documentation to find out how many characteristics your specific device has.
        private int characteristicIndex = 0;

        public delegate void CharacteristicIndexChangedEventHandler(object sender, int characteristicIndex);
        public event CharacteristicIndexChangedEventHandler CharacteristicIndexChanged;

        public int CharacteristicIndex
        {
            get
            {
                return characteristicIndex;
            }
            set
            {
                if (Running)
                    throw new Exception();

                if (characteristicIndex != value)
                {
                    characteristicIndex = value;
                    if (CharacteristicIndexChanged != null)
                        CharacteristicIndexChanged(this, value);
                }   
            }
        }

        private int initDelay = 500;

        public delegate void InitDelayChangedEventHandler(object sender, int delay);
        public event InitDelayChangedEventHandler InitDelayChanged;

        public int InitDelay
        {
            get
            {
                return initDelay;
            }
            set
            {
                if (Running)
                    throw new Exception();

                if (initDelay != value)
                {
                    initDelay = value;
                    if (InitDelayChanged != null)
                        InitDelayChanged(this, value);
                }
            }
        }

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

        public delegate void DeviceChangedEventHandler(object sender, DeviceInformation device);
        public event DeviceChangedEventHandler DeviceChanged;

        public override async void Start()
        {
            if (Running)
                return;

            logger.Debug("Starting HRP");

            lastReceivedDate = DateTime.Now;

            timeoutTimer.Start();
            Running = true;

            //TODO: Write Characteristic listener
            await ConfigureServiceForNotificationsAsync();
        }

        private async Task ConfigureServiceForNotificationsAsync()
        {          
            try
            {
                logger.Debug($"Getting GattDeviceService {device.Name} with id {device.Id}");

                service = await GattDeviceService.FromIdAsync(device.Id);
                if (initDelay > 0)
                    await Task.Delay(initDelay);

                if (service != null)
                {
                    logger.Debug($"GattDeviceService instatiated successfully");

                    logger.Debug($"GattSession status = {service.Session.SessionStatus}, " +
                    $"mantain connection = {service.Session.MaintainConnection}, " +
                    $"can mantain connection = {service.Session.MaintainConnection}");
                }
                else
                {
                    logger.Debug($"Failed to instantiate GattDeviceService");
                }

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
            
                //TODO
                var currentDescriptorValue = await actualHRCharacteristic.ReadClientCharacteristicConfigurationDescriptorAsync();

                if ((currentDescriptorValue.Status != GattCommunicationStatus.Success) ||
                    (currentDescriptorValue.ClientCharacteristicConfigurationDescriptor != CHARACTERISTIC_NOTIFICATION_TYPE))
                {
                    // Set the Client Characteristic Configuration Descriptor to enable the device to send notifications
                    // when the Characteristic value changes

                    logger.Debug("Setting GattCharacteristic configuration descriptor to enable notifications");

                    GattCommunicationStatus status =
                        await actualHRCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                        CHARACTERISTIC_NOTIFICATION_TYPE);

                    if (status == GattCommunicationStatus.Unreachable)
                    {

                        logger.Debug("Device unreachable");

                        // Register a PnpObjectWatcher to detect when a connection to the device is established,
                        // such that the application can retry device configuration.
                        StartDeviceConnectionWatcher();
                    }
                }
                else
                {
                    logger.Debug("Configuration successfull");
                }
            }
            catch (Exception e)
            {
                logger.Warn("Error configuring HRP device", e);

                Stop();
                FireTimeout("Bluetooth HRP device initialization failed");
                //throw new Exception("Bluetooth HRP device initialization failed");
            }
        }

        private void ProcessPackets()
        {
            logger.Debug($"Firing PacketProcessed event, packet = {LastPacket}");
            PacketProcessedEventArgs args = new PacketProcessedEventArgs(LastPacket);
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
                var status = await actualHRCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                    CHARACTERISTIC_NOTIFICATION_TYPE);

                if (status == GattCommunicationStatus.Success)
                {
                    logger.Debug("Stopping device connection watcher");

                    // Once the Client Characteristic Configuration Descriptor is set, the watcher is no longer required
                    watcher.Stop();
                    watcher = null;

                    logger.Debug("Configuration successfull");
                }
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

                logger.Debug("Resetting counters");

                DoReset();
            }
        }

        private void DoReset()
        {
            TotalPackets = 0;
        }

        public override void Reset()
        {

            logger.Debug("Resetting HRP");

            Stop();
            Start();
        }

        public override void Dispose()
        {
            if (watcher != null)
            {
                watcher.Stop();
                watcher = null;
            }

            if (service != null)
            {
                service.Dispose();
                service = null;
            }
        }
    }
}
