using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace MGT.Cardia.Network
{
    class NeosHRMWebsocketService : WebSocketBehavior
    {
        public void BroadcastHRM(string hrm)
        {
            Sessions.Broadcast(hrm);
        }
    }
}
