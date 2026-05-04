using System;
using System.ServiceProcess;

namespace RemoteRescueService
{
    static class Program
    {
        static void Main()
        {
            ServiceBase[] ServicesToRun;
            ServicesToRun = new ServiceBase[]
            {
                new RescueService()
            };
            ServiceBase.Run(ServicesToRun);
        }
    }
}
