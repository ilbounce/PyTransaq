using System;
using System.Threading;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Collections.Generic;
using System.Text.Json;
using System.Runtime.InteropServices;

namespace Transaq
{
    static class MarshalUTF8
    {
        private static UTF8Encoding _utf8;

        //--------------------------------------------------------------------------------
        static MarshalUTF8()
        {
            _utf8 = new UTF8Encoding();
        }

        //--------------------------------------------------------------------------------
        public static IntPtr StringToHGlobalUTF8(String data)
        {
            Byte[] dataEncoded = _utf8.GetBytes(data + "\0");

            int size = Marshal.SizeOf(dataEncoded[0]) * dataEncoded.Length;

            IntPtr pData = Marshal.AllocHGlobal(size);

            Marshal.Copy(dataEncoded, 0, pData, dataEncoded.Length);

            return pData;
        }

        //--------------------------------------------------------------------------------
        public static String PtrToStringUTF8(IntPtr pData)
        {
            // this is just to get buffer length in bytes
            String errStr = Marshal.PtrToStringAnsi(pData);
            int length = errStr.Length;

            Byte[] data = new byte[length];
            Marshal.Copy(pData, data, 0, length);

            return _utf8.GetString(data);
        }
        //--------------------------------------------------------------------------------
    }
    static class TXmlConnector
    {
        const String EX_SETTING_CALLBACK = "Не смог установить функцию обратного вызова";


        delegate bool CallBackDelegate(IntPtr pData);

        static readonly CallBackDelegate myCallbackDelegate = new CallBackDelegate(myCallBack);
        static readonly GCHandle callbackHandle = GCHandle.Alloc(myCallbackDelegate);

        public static bool bConnected; // флаг наличия подключения к серверу

        public static AutoResetEvent statusDisconnected = new AutoResetEvent(true);
        public static bool bDisconnected;
        public static int statusTimeout;


        public static void ConnectorSetCallback()
        {


            if (!SetCallback(myCallbackDelegate))
            {
                throw (new Exception(EX_SETTING_CALLBACK));
            }

        }

        //--------------------------------------------------------------------------------
        static bool myCallBack(IntPtr pData)
        {
            string res;
            String data = MarshalUTF8.PtrToStringUTF8(pData);
            FreeMemory(pData);

            res = Transaq_HandleData(data);
            if (res == "server_status") New_Status();
            return true;
        }


        static void New_Status()
        {
            if (bConnected)
            {
                statusDisconnected.Reset();
            }
            else
            {
                bConnected = true;
                statusDisconnected.Set();
                bDisconnected = false;
            }
        }



        //--------------------------------------------------------------------------------
        public static String ConnectorSendCommand(String command)
        {

            IntPtr pData = MarshalUTF8.StringToHGlobalUTF8(command);
            IntPtr pResult = SendCommand(pData);

            String result = MarshalUTF8.PtrToStringUTF8(pResult);



            Marshal.FreeHGlobal(pData);
            FreeMemory(pResult);

            return result;

        }


        public static bool ConnectorInitialize(String Path, Int16 LogLevel)
        {

            IntPtr pPath = MarshalUTF8.StringToHGlobalUTF8(Path);
            IntPtr pResult = Initialize(pPath, LogLevel);

            if (!pResult.Equals(IntPtr.Zero))
            {
                String result = MarshalUTF8.PtrToStringUTF8(pResult);
                Marshal.FreeHGlobal(pPath);
                FreeMemory(pResult);
                log.WriteLog("INITIALIZE: " + result);
                return false;
            }
            else
            {
                Marshal.FreeHGlobal(pPath);
                log.WriteLog("Initialize() OK");
                return true;
            }

        }


        public static void ConnectorUnInitialize()
        {

            if (statusDisconnected.WaitOne(statusTimeout))
            {
                IntPtr pResult = UnInitialize();

                if (!pResult.Equals(IntPtr.Zero))
                {
                    String result = MarshalUTF8.PtrToStringUTF8(pResult);
                    FreeMemory(pResult);
                    log.WriteLog(result);
                }
                else
                {
                    log.WriteLog("UnInitialize() OK");
                }
            }
            else
            {
                log.WriteLog("WARNING! Не дождались статуса disconnected. UnInitialize() не выполнено.");
            }

        }


        //================================================================================

        public static string Transaq_HandleData(string data)
        {
            XmlReaderSettings xs = new XmlReaderSettings();
            xs.IgnoreWhitespace = true;
            xs.ConformanceLevel = ConformanceLevel.Fragment;
            xs.ProhibitDtd = false;
            XmlReader xr = XmlReader.Create(new System.IO.StringReader(data), xs);

            string section = "";
            string ename = "";
            string evalue = "";

            Dictionary<string, string> curQuotation = new Dictionary<string, string>();

            try
            {
                while (xr.Read())
                {
                    switch (xr.NodeType)
                    {
                        case XmlNodeType.Element:
                        case XmlNodeType.EndElement:
                            ename = xr.Name; break;
                        case XmlNodeType.Text:
                            evalue = xr.Value; break;
                        case XmlNodeType.CDATA:
                        case XmlNodeType.Comment:
                        case XmlNodeType.XmlDeclaration:
                            evalue = xr.Value; break;
                        case XmlNodeType.DocumentType:
                            ename = xr.Name; evalue = xr.Value; break;
                        default: break;
                    }

                    if (xr.Depth == 0)
                    {
                        if (xr.NodeType == XmlNodeType.Element)
                        {
                            section = ename;
                            byte[] byteData = Encoding.UTF8.GetBytes(data);
                            string bufLength = byteData.Length.ToString();
                            string secMsg = section + ":" + bufLength;
                            if (section == "news_header" || section == "news_body")
                            {
                                while (Server.newsListener.handler is null) continue;
                                Server.newsListener.Send(secMsg + "\0");
                                Server.newsListener.Send(data + "\0");

                            }

                            else if (section == "quotations" || section == "quotes" || section == "alltrades" || section == "ticks")
                            {
                                while (Server.subscribeDataListener.handler is null) continue;
                                Server.subscribeDataListener.Send(secMsg + "\0");
                                Server.subscribeDataListener.Send(data + "\0");
                            }

                            else if (section == "orders" || section == "trades")
                            {
                                while (Server.accountListener.handler is null) continue;
                                Server.accountListener.Send(secMsg + "\0");
                                Server.accountListener.Send(data + "\0");
                            }

                            else
                            {
                                Server.dataListener.Send(secMsg + "\0");
                                Server.dataListener.Send(data + "\0");

                                if (section == "server_status")
                                {
                                    if (xr.GetAttribute("connected") == "false")
                                    {
                                        bDisconnected = true;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            
            catch (System.Xml.XmlException)
            {
                section = "Undefined";
                byte[] byteData = Encoding.UTF8.GetBytes(data);
                string bufLength = byteData.Length.ToString();
                string secMsg = section + ":" + bufLength;

                Server.dataListener.Send(secMsg + "\0");
                Server.dataListener.Send(data + "\0");
            }

            return section;
        }

        //--------------------------------------------------------------------------------
        // файл библиотеки TXmlConnector.dll должен находиться в одной папке с программой

        [DllImport("txmlconnector.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern bool SetCallback(CallBackDelegate pCallback);

        //[DllImport("txmlconnector.dll", CallingConvention = CallingConvention.StdCall)]
        //private static extern bool SetCallbackEx(CallBackExDelegate pCallbackEx, IntPtr userData);

        [DllImport("txmlconnector.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern IntPtr SendCommand(IntPtr pData);

        [DllImport("txmlconnector.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern bool FreeMemory(IntPtr pData);

        [DllImport("TXmlConnector.dll", CallingConvention = CallingConvention.Winapi)]
        private static extern IntPtr Initialize(IntPtr pPath, Int32 logLevel);

        [DllImport("TXmlConnector.dll", CallingConvention = CallingConvention.Winapi)]
        private static extern IntPtr UnInitialize();

        [DllImport("TXmlConnector.dll", CallingConvention = CallingConvention.Winapi)]
        private static extern IntPtr SetLogLevel(Int32 logLevel);
        //--------------------------------------------------------------------------------
    }
}
