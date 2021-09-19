using MGT.HRM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace MGT.Cardia.Neos
{
    class NeosHRMWebsocketService : WebSocketBehavior
    {
        public void BroadcastHRM(string hrm)
        {
            if (Sessions != null && Sessions.Count > 0)
                Sessions.Broadcast(hrm);
        }

        public void processSamplerPacket(object sender, IHRMPacket hrmPacket, byte? minHeartRate, byte? maxHeartRate)
        {
            this.BroadcastHRM(String.Format("{0},{1},{2},{3}", hrmPacket.HeartRate, 0, 0, true ? 1 : 0));
        }
    }
}
