using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MGT.HRM.HRP.BtValues;
using MGT.Utilities.EventHandlers;

namespace MGT.HRM.HRP
{
    public abstract class BtHrpLogger : IHRMLogger
    {
        public event GenericEventHandler<bool> LoggerStatusChanged;

        protected bool running = false;
        public bool Running
        {
            get
            {
                return running;
            }
        }

        protected BtHrp btHrp;

        public void Start(HeartRateMonitor hrm)
        {
            if (hrm is BtHrp)
                btHrp = (BtHrp)hrm;
            else
                throw new Exception("Invalid HRM, BtHrp expected");

            BtHrpStart();

            running = true;

            if (LoggerStatusChanged != null)
                LoggerStatusChanged(this, running);
        }

        protected abstract void BtHrpStart();

        public void Stop()
        {
            if (!running)
                return;
            
            running = false;

            BtHrpStop();

            if (LoggerStatusChanged != null)
                LoggerStatusChanged(this, running);
        }

        protected abstract void BtHrpStop();

        public void Log(IHRMPacket hrmPacket)
        {
            HeartRateBtValue btHrpPacket;
            if (hrmPacket is HeartRateBtValue)
                btHrpPacket = (HeartRateBtValue)hrmPacket;
            else
                throw new Exception("Invalid IHRMPacket, BtHrpPacket expected");

            BtHrpLog(btHrpPacket);
        }

        public virtual void ResetSubscriptions()
        {
            LoggerStatusChanged = null;
        }

        protected abstract void BtHrpLog(HeartRateBtValue btHrpPacket);

        public abstract void Dispose();
    }
}
