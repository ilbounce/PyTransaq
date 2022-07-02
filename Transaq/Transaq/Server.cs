using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Transaq
{
    public class StateObject
    {  
        public const int BufferSize = 1024;
        public byte[] buffer = new byte[BufferSize];
        public StringBuilder sb = new StringBuilder();
        public Socket workSocket = null;

        public void ClearBuffer()
        {
            buffer = new byte[BufferSize];
            sb = new StringBuilder();
        }
    }

    public class AsynchronousSocketListener
    { 
        public ManualResetEvent allDone = new ManualResetEvent(false);
        public Socket handler;
        public string[] request = new string[2];
        StateObject state = new StateObject();
        int port;

        public AsynchronousSocketListener(int port)
        {
            this.port = port;
        }

        public string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            throw new Exception("No network adapters with an IPv4 address in the system!");
        }

        public bool StartListening()
        {
            IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
            IPAddress ipAddress = ipHostInfo.AddressList[1];
            IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), this.port);  
            Socket listener = new Socket(ipAddress.AddressFamily,
                SocketType.Stream, ProtocolType.Tcp);

            try
            {
                listener.Bind(localEndPoint);
                listener.Listen(100);
  
                allDone.Reset();
  
                log.WriteLog(String.Format("Waiting for a connection at port {0}...", this.port));
                listener.BeginAccept(
                    new AsyncCallback(AcceptCallback),
                    listener);

                allDone.WaitOne();

                log.WriteLog("Client is connected at port " + this.port);

                return true;

            }
            catch (Exception e)
            {
                log.WriteLog(e.ToString());
                return false;
            }

        }

        public void AcceptCallback(IAsyncResult ar)
        {
            allDone.Set();  
            Socket listener = (Socket)ar.AsyncState;
            handler = listener.EndAccept(ar);
            
            state.workSocket = handler;
        }

        public void Read()
        {
            Socket handler = state.workSocket;
            state.ClearBuffer();
            handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                new AsyncCallback(ReadCallback), state);
        }

        public void ReadCallback(IAsyncResult ar)
        {
            String content = String.Empty;

            StateObject state = (StateObject)ar.AsyncState;
            Socket handler = state.workSocket;
            int bytesRead = 0;

            if (handler.Connected)
            {

                try
                {
                    bytesRead = handler.EndReceive(ar);
                }

                catch (SocketException)
                {
                    request = new string[] { "stop", null };
                }

                if (bytesRead > 0)
                {
                    state.sb.Append(Encoding.UTF8.GetString(
                        state.buffer, 0, bytesRead));  
                    content = state.sb.ToString();
                    if (content.IndexOf("\0") > -1)
                    {
                        content = content.Replace("\0", "");
                        request = content.Split(":");

                    }
                    else
                    {  
                        handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                        new AsyncCallback(ReadCallback), state);
                    }
                }
            }

        }

        public void Send(String data)
        {

            Socket handler = state.workSocket;
 
            byte[] byteData = Encoding.UTF8.GetBytes(data);
            if (handler.Connected)
            {
                try
                {
                    handler.BeginSend(byteData, 0, byteData.Length, 0,
                        new AsyncCallback(SendCallback), handler);
                }
                catch (SocketException)
                {
                    request = new string[] { "stop", null };
                }
            }
        }

        private void SendCallback(IAsyncResult ar)
        {
            try
            {
   
                Socket handler = (Socket)ar.AsyncState;
  
                int bytesSent = handler.EndSend(ar);
            }
            catch (Exception e)
            {
                log.WriteLog(e.ToString());
            }
        }

        public void StopListening()
        {
            if (!(handler is null))
            {
                log.WriteLog(String.Format("Client at port {0} closed connection", this.port));
                handler.Shutdown(SocketShutdown.Both);
                handler.Close();
            }
        }
    }

    public class Ports
    {
        public static int commandListenerPort;
        public static int PingPort;
        public static int newsListenerPort;
        public static int subscribeDataListenerPort;
        public static int dataListenerPort;
        public static int accountListenerPort;
    }
    public class Server
    {

        public static readonly AsynchronousSocketListener commandListener = 
            new AsynchronousSocketListener(Ports.commandListenerPort);
        public static readonly AsynchronousSocketListener Ping = 
            new AsynchronousSocketListener(Ports.PingPort);
        public static readonly AsynchronousSocketListener newsListener = 
            new AsynchronousSocketListener(Ports.newsListenerPort);
        public static readonly AsynchronousSocketListener subscribeDataListener = 
            new AsynchronousSocketListener(Ports.subscribeDataListenerPort);
        public static readonly AsynchronousSocketListener dataListener = 
            new AsynchronousSocketListener(Ports.dataListenerPort);
        public static readonly AsynchronousSocketListener accountListener = 
            new AsynchronousSocketListener(Ports.accountListenerPort);

        int session_timeout = 25;
        int request_timeout = 10;

        public static string TransaqIP = "tr1.finam.ru";
        public static string TransaqPort = "3900";

        string[] request = new string[2];
        string response;

        Thread pingThread = new Thread(new ThreadStart(CheckConnection));

        public static void CheckConnection()
        {
            while (true)
            {
                Ping.Send("ping");
                if (!Ping.handler.Connected) break;
                Thread.Sleep(1000);
            }

            commandListener.request = Ping.request;
            Ping.StopListening();
            newsListener.StopListening();
            subscribeDataListener.StopListening();
            dataListener.StopListening();
            accountListener.StopListening();
        }

        public void Run()
        {
            if (commandListener.StartListening() 
                    && Ping.StartListening() 
                    && dataListener.StartListening() ){
                commandListener.Send("Connection to server status: " + commandListener.handler.Connected.ToString() + "\0");
                pingThread.Start();
            }

            while (true)
            {
                if (request == commandListener.request) continue;
                request = commandListener.request;

                if (request[0] == "auth")
                {
                    response = Methods.Authorize(request[1], session_timeout, request_timeout);
                    commandListener.Send(response + "\0");
                }

                else if (request[0] == "start_acc")
                {
                    newsListener.StartListening();
                    accountListener.StartListening();
                }

                else if ((request[0] == "quotations") || (request[0] == "alltrades") || (request[0] == "quotes"))
                {
                    response = Methods.Subscribe(request[0], request[1]);
                    commandListener.Send(response + "\0");

                    if (response == "<result success=\"true\"/>")
                    {
                        if (subscribeDataListener.handler is null) subscribeDataListener.StartListening();
                    }
                }

                else if (request[0] == "ticks")
                {
                    response = Methods.SubscribeTicks(request[1]);
                    commandListener.Send(response + "\0");

                    if (response == "<result success=\"true\"/>")
                    {
                        if (subscribeDataListener.handler is null) subscribeDataListener.StartListening();
                    }
                }

                else if ((request[0] == "-quotations") || (request[0] == "-alltrades") || (request[0] == "-quotes"))
                {
                    response = Methods.UnSubscribe(request[0].Replace("-", ""), request[1]);
                    commandListener.Send(response + "\0");
                }

                else if (request[0] == "-ticks")
                {
                    response = Methods.UnSubscribeTicks();
                    commandListener.Send(response + "\0");
                }

                else if (request[0] == "history_data")
                {
                    response = Methods.GetHistoryData(request[1]);
                    commandListener.Send(response + "\0");
                }

                else if (request[0] == "old_news")
                {
                    response = Methods.GetOldNews(request[1]);
                    commandListener.Send(response + "\0");
                }

                else if (request[0] == "get_news")
                {
                    response = Methods.GetNewsById(request[1]);
                    commandListener.Send(response + "\0");
                }

                else if (request[0] == "status")
                {
                    response = Methods.GetServerStatus();
                    commandListener.Send(response + "\0");
                    
                }

                else if (request[0] == "forts_position")
                {
                    response = Methods.GetFortsPosition(request[1]);
                    commandListener.Send(response + "\0");
                }

                else if (request[0] == "markets")
                {
                    response = Methods.GetMarkets();
                    commandListener.Send(response + "\0");
                }

                else if (request[0] == "client_limits")
                {
                    response = Methods.GetClientLimits(request[1]);
                    commandListener.Send(response + "\0");
                }

                else if (request[0] == "servtime_dif")
                {
                    response = Methods.GetServtimeDifference();
                    commandListener.Send(response + "\0");
                }

                else if (request[0] == "change_pass")
                {
                    response = Methods.ChangePass(request[1]);
                    commandListener.Send(response + "\0");
                }

                else if (request[0] == "get_version")
                {
                    response = Methods.GetConnectorVersion();
                    commandListener.Send(response + "\0");
                }

                else if (request[0] == "get_sid")
                {
                    response = Methods.GetServerId();
                    commandListener.Send(response + "\0");
                }

                else if (request[0] == "neworder")
                {
                    response = Methods.NewOrder(request[1]);
                    commandListener.Send(response + "\0");
                }

                else if (request[0] == "newcondorder")
                {
                    response = Methods.NewCondOrder(request[1]);
                    commandListener.Send(response + "\0");
                }

                else if (request[0] == "newstoporder")
                {
                    response = Methods.NewStopOrder(request[1]);
                    commandListener.Send(response + "\0");
                }

                else if (request[0] == "cancelorder")
                {
                    response = Methods.CancelOrder(request[1]);
                    commandListener.Send(response + "\0");
                }

                else if (request[0] == "cancelstoporder")
                {
                    response = Methods.CancelStopOrder(request[1]);
                    commandListener.Send(response + "\0");
                }

                else if (request[0] == "moveorder")
                {
                    response = Methods.MoveOrder(request[1]);
                    commandListener.Send(response + "\0");
                }

                else if (request[0] == "sec_info")
                {
                    response = Methods.GetSecInfo(request[1]);
                    commandListener.Send(response + "\0");
                }

                else if (request[0] == "united_equity")
                {
                    response = Methods.GetUnitedEquity(request[1]);
                    commandListener.Send(response + "\0");
                }

                else if (request[0] == "united_go")
                {
                    response = Methods.GetUnitedGo(request[1]);
                    commandListener.Send(response + "\0");
                }

                else if (request[0] == "mc_portfolio")
                {
                    response = Methods.GetMcPortfolio(request[1]);
                    commandListener.Send(response + "\0");
                }

                else if (request[0] == "max_buy_sell")
                {
                    response = Methods.GetMaxBuySell(request[1]);
                    commandListener.Send(response + "\0");
                }

                else if (request[0] == "cln_sec_permissions")
                {
                    response = Methods.GetClnSecPermissions(request[1]);
                    commandListener.Send(response + "\0");
                }

                else if (request[0] == "disconnect")
                {
                    response = Methods.Disconnect();
                    commandListener.Send(response + "\0");
                }

                else if (request[0] == "stop")
                {
                    commandListener.StopListening();
                    break;
                }

                commandListener.Read();
            }
        }
    }
}
