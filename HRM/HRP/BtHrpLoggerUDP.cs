﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net.Sockets;
using MGT.HRM.HRP.BtValues;

namespace MGT.HRM.HRP
{
    public class BtHrpLoggerUDP : BtHrpNetLogger
    {
        private string delimiter = ";";
        public string Delimiter
        {
            get
            {
                return delimiter;
            }
            set
            {
                if (running)
                    throw new Exception("Delimiter cannot be changed when the logger is running");

                delimiter = value;
            }
        }

        private long packetIndex;
        private UdpClient udpClient;

        // 1 - Timestamp
        // 2 - Heart beat
        // 3 - Heart rate
        // 4 - Min heart rate
        // 5 - Max heart rate

        protected override void BtHrpStart()
        {
            packetIndex = 0;
            udpClient = new UdpClient(0);
        }

        protected override void BtHrpStop()
        {
            Dispose();
        }

        protected override void BtHrpLog(HeartRateBtValue btHrpPacket)
        {
            DateTime timestamp = DateTime.Now;

            if (!running)
                return;

            write(btHrpPacket, timestamp);
        }

        private void write(HeartRateBtValue btHrpPacket, DateTime timestamp)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append((packetIndex++).ToString());
            sb.Append(delimiter);
            sb.Append(timestamp.ToString());
            sb.Append(delimiter);
            sb.Append(btHrp.HeartBeats.ToString());
            sb.Append(delimiter);
            sb.Append(btHrpPacket.HeartRate.ToString());
            sb.Append(delimiter);
            sb.Append(btHrp.MinHeartRate.ToString());
            sb.Append(delimiter);
            sb.Append(btHrp.MaxHeartRate.ToString());
            sb.Append(delimiter);
            sb.Append(Environment.NewLine);

            byte[] payload = Encoding.UTF8.GetBytes(sb.ToString());

            udpClient.Send(payload, payload.Length, this.ipAddress, this.destinationPort);
        }

        public override void Dispose()
        {
            udpClient.Close();
        }
    }
}
