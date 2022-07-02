using System;

namespace Transaq
{
    class Program
    {
        static void Main(string[] args)
        {
            Ports.commandListenerPort = Int32.Parse(args[0]);
            Ports.PingPort = Int32.Parse(args[1]);
            Ports.newsListenerPort = Int32.Parse(args[2]);
            Ports.subscribeDataListenerPort = Int32.Parse(args[3]);
            Ports.dataListenerPort = Int32.Parse(args[4]);
            Ports.accountListenerPort = Int32.Parse(args[5]);

            Server server = new Server();
            server.Run();
        }
    }
}
