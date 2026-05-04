using System;
using System.ServiceProcess;

namespace RemoteRescueService
{
    public partial class RescueService : ServiceBase
    {
        private HttpServer _httpServer;

        public RescueService()
        {
            InitializeComponent();
            this.ServiceName = "RemoteRescueService";
            this.CanStop = true;
            this.CanPauseAndContinue = true;
            this.AutoLog = true;
        }

        protected override void OnStart(string[] args)
        {
            _httpServer = new HttpServer();
            _httpServer.Start();
        }

        protected override void OnStop()
        {
            _httpServer?.Stop();
        }
    }
}
