using MGT.HRM;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace MGT.Cardia.Neos.Views
{
    public partial class NeosForm : Form
    {
        private bool started = false;

        private Cardia cardia;
        private NetworkSampler sampler;

        // This stuff should probably be in another file to operate under separation of concerns, but I don't know what I'm doing?
        private WebSocketServer wsServer;
        private NeosHRMWebsocketService wsHRMService = new NeosHRMWebsocketService();

        public NeosForm(Cardia cardia)
        {
            this.cardia = cardia;
            this.sampler = new NetworkSampler(cardia);
            this.sampler.PacketSampled += this.wsHRMService.processSamplerPacket;
            
            InitializeComponent();
        }

        private void NeosForm_Load(object sender, EventArgs e)
        {

        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        void onPacket(object sender, IHRMPacket hrmPacket, byte? minHeartRate, byte? maxHeartRate)
        {
#if DEBUG
            System.Diagnostics.Debug.WriteLine("Sending HRM message");
#endif
            
        }

        void swapForm(bool state)
        {
            textBox1.Enabled = !state;
            Start.Text = state ? "Stop" : "Start";
        }
        private void Start_Click(object sender, EventArgs e)
        {
            
            if (!started)
            {
                this.sampler.Start();
                this.wsServer = new WebSocketServer("ws://127.0.0.1:" + int.Parse(textBox1.Text));
                this.wsServer.AddWebSocketService<NeosHRMWebsocketService>("/hrm", () => this.wsHRMService);
                this.wsServer.Start();
            } 
            else
            {
                this.sampler.Stop();
                this.wsServer.Stop();
            }
            this.started = !started;
            swapForm(started);
        }
    }
}
