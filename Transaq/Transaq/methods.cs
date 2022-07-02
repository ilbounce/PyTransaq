using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace Transaq
{
    class Methods
    {
        private static string FormatOrderCmd(string method, string orderData)
        {
            string[] data = orderData.Split(";");
            string cmd = String.Format("<command id=\"{0}\">", method);
            string board = data[0];
            string seccode = data[1];

            Dictionary<string, string> cmdData = new Dictionary<string, string>();
            Dictionary<string, string> stopLossData = new Dictionary<string, string>();
            Dictionary<string, string> takeProfitData = new Dictionary<string, string>();

            cmdData.Add("client", data[2]);
            cmdData.Add("union", data[3]);

            if (method != "newstoporder")
            {
                cmdData.Add("price", data[4]);
                cmdData.Add("hidden", data[5]);
                cmdData.Add("quantity", data[6]);
                cmdData.Add("buysell", data[7]);
                cmdData.Add("bymarket", data[8]);
                cmdData.Add("brokerref", data[9]);
                if (method == "neworder")
                {
                    cmdData.Add("unfilled", data[10]);
                    cmdData.Add("usecredit", data[11]);
                    cmdData.Add("nosplit", data[12]);
                    cmdData.Add("expdate", data[13]);
                }

                else if (method == "newcondorder")
                {
                    cmdData.Add("cond_type", data[10]);
                    cmdData.Add("cond_value", data[11]);
                    cmdData.Add("valid_after", data[12]);
                    cmdData.Add("valid_before", data[13]);
                    cmdData.Add("usecredit", data[14]);
                    cmdData.Add("within_pos", data[15]);
                    cmdData.Add("nosplit", data[16]);
                    cmdData.Add("expdate", data[17]);

                }
            }

            else
            {
                cmdData.Add("buysell", data[4]);
                cmdData.Add("linkedorderno", data[5]);
                cmdData.Add("validfor", data[6]);
                cmdData.Add("expdate", data[7]);

                stopLossData.Add("activationprice", data[8]);
                stopLossData.Add("orderprice", data[9]);
                stopLossData.Add("bymarket", data[10]);
                stopLossData.Add("quantity", data[11]);
                stopLossData.Add("usecredit", data[12]);
                stopLossData.Add("brokerref", data[14]);

                takeProfitData.Add("activationprice", data[15]);
                takeProfitData.Add("quantity", data[16]);
                takeProfitData.Add("usecredit", data[17]);
                takeProfitData.Add("guardtime", data[18]);
                takeProfitData.Add("brokerref", data[19]);
                takeProfitData.Add("correction", data[20]);
                takeProfitData.Add("spread", data[21]);
                takeProfitData.Add("bymarket", data[22]);

            }

            cmd += "<security><board>" + board + "</board><seccode>" + seccode + "</seccode></security>";

            foreach (KeyValuePair<string, string> kvp in cmdData)
            {
                if (kvp.Value.Length != 0)
                {
                    cmd += String.Format("<{0}>{1}</{0}>", kvp.Key, kvp.Value);
                }
            }

            if (method == "newstoporder")
            {
                cmd += "<stoploss>";
                foreach (KeyValuePair<string, string> kvp in stopLossData)
                {
                    cmd += String.Format("<{0}>{1}</{0}>", kvp.Key, kvp.Value);
                }
                cmd += "</stoploss><takeprofit>";
                foreach (KeyValuePair<string, string> kvp in takeProfitData)
                {
                    cmd += String.Format("<{0}>{1}</{0}>", kvp.Key, kvp.Value);
                }
            }

            cmd += "</command>";

            return cmd;
        }
        public static bool Initialize(int session_timeout)
        {
            string path = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase);
            string AppDir = path.Substring(path.IndexOf('\\') + 1, path.Length - path.IndexOf('\\') - 1);

            log.StartLogging(AppDir + "\\log" + DateTime.Now.ToString("yyMMdd") + ".txt");
            TXmlConnector.statusTimeout = session_timeout * 1000;
            TXmlConnector.ConnectorSetCallback();

            string LogPath = AppDir + "\\\0";

            if (TXmlConnector.ConnectorInitialize(LogPath, 2))
            {
                TXmlConnector.statusDisconnected.Set();
                return true;
            }

            else return false;
        }

        public static void UnInitialize()
        {
            TXmlConnector.ConnectorUnInitialize();
            log.StopLogging();
        }
        public static string Authorize(string data, int session_timeout, int request_timeout)
        {
            if (!Initialize(session_timeout))
            {
                log.WriteLog("Unsuccessfull initialization");
                return "Unsuccessfull initialization";
            }
            else
            {
                string[] authData = data.Split(";");
                string cmd = "<command id=\"connect\">";
                cmd = cmd + "<login>" + authData[0] + "</login>";
                cmd = cmd + "<password>" + authData[1] + "</password>";
                cmd = cmd + "<host>" + Server.TransaqIP + "</host>";
                cmd = cmd + "<port>" + Server.TransaqPort + "</port>";
                cmd = cmd + "<rqdelay>100</rqdelay>";
                cmd = cmd + "<session_timeout>" + session_timeout.ToString() + "</session_timeout> ";
                cmd = cmd + "<request_timeout>" + request_timeout.ToString() + "</request_timeout>";
                cmd = cmd + "</command>";

                log.WriteLog("Connecting " + authData[0]);
                string res = TXmlConnector.ConnectorSendCommand(cmd);

                return res;
            }
        }

        public static string GetOldNews(string count)
        {
            string cmd = String.Format("<command id=\"get_old_news\" count=\"{0}\"/>", count);
            string res = TXmlConnector.ConnectorSendCommand(cmd);

            return res;
        }

        public static string GetNewsById(string news_id)
        {
            string cmd = String.Format("<command id=\"get_news_body\" news_id={0}/>", news_id);
            string res = TXmlConnector.ConnectorSendCommand(cmd);

            return res;
        }

        public static string Subscribe(string method, string data)
        {
            string[] securities = data.Split(";");
            string[] seccodes = securities[0].Split(",");
            string[] boards = securities[1].Split(",");


            string cmd = "<command id=\"subscribe\">";
            cmd = cmd + "<" + method + ">";

            for (int i = 0; i < seccodes.Length; i++)
            {
                cmd += "<security><board>" + boards[i] + "</board><seccode>" + seccodes[i] + "</seccode></security>";
            }

            cmd += "</" + method + "></command>";

            string res = TXmlConnector.ConnectorSendCommand(cmd);

            return res;
        }

        public static string SubscribeTicks(string data)
        {
            string[] pars = data.Split(",");
            string seccode = pars[0];
            string board = pars[1];
            string tradeno = pars[2];
            string filter = pars[3];

            string cmd = "<command id=\"subscribe_ticks\">";

            cmd += "<security><board>" + board + "</board><seccode>" + seccode + "</seccode><tradeno>" 
                + tradeno + "</tradeno></security>";
            cmd += "<filter>" + filter + "</filter>";
            cmd += "</command>";

            string res = TXmlConnector.ConnectorSendCommand(cmd);

            return res;

        }

        public static string UnSubscribe(string method, string data)
        {
            string[] securities = data.Split(";");
            string[] seccodes = securities[0].Split(",");
            string[] boards = securities[1].Split(",");

            string cmd = "<command id=\"unsubscribe\">";
            cmd = cmd + "<" + method + ">";

            for (int i = 0; i < seccodes.Length; i++)
            {
                cmd += "<security><board>" + boards[i] + "</board><seccode>" + seccodes[i] + "</seccode></security>";
            }

            cmd += "</" + method + "></command>";

            string res = TXmlConnector.ConnectorSendCommand(cmd);

            return res;
        }

        public static string UnSubscribeTicks()
        {
            string cmd = "<command id=\"subscribe_ticks\">";
            cmd += "</command>";

            string res = TXmlConnector.ConnectorSendCommand(cmd);

            return res;
        }
        public static string GetHistoryData(string data)
        {
            string[] parseData = data.Split(";");
            string seccode = parseData[0];
            string board = parseData[1];
            string period = parseData[2];
            string count = parseData[3];
            string reset = parseData[4];

            string cmd = "<command id=\"gethistorydata\">";
            cmd += "<security><board>" + board + "</board><seccode>" + seccode + "</seccode></security>";
            cmd += "<period>" + period + "</period>";
            cmd += "<count>" + count + "</count>";
            cmd += "<reset>" + reset + "</reset>";
            cmd += "</command>";

            string res = TXmlConnector.ConnectorSendCommand(cmd);

            return res;
        }

        public static string GetServerStatus()
        {
            string cmd = "<command id=\"server_status\"/>";
            string res = TXmlConnector.ConnectorSendCommand(cmd);

            return res;
        }

        public static string GetFortsPosition(string client)
        {
            string cmd;
            if (client.Length == 0)
            {
                cmd = "<command id=\"get_forts_positions\"/>";
            }
            else
            {
                cmd = String.Format("<command id=\"get_forts_positions\" client=\"{0}\"/>", client);
            }

            string res = TXmlConnector.ConnectorSendCommand(cmd);

            return res;

        }
        
        public static string GetClientLimits(string client)
        {
            string cmd = String.Format("<command id=\"get_client_limits\" client=\"{0}\"/>", client);

            string res = TXmlConnector.ConnectorSendCommand(cmd);

            return res;
        }
        
        public static string GetMarkets()
        {
            string cmd = "<command id=\"get_markets\"/>";

            string res = TXmlConnector.ConnectorSendCommand(cmd);

            return res;
        }
        
        public static string GetServtimeDifference()
        {
            string cmd = "<command id=\"get_servtime_difference\"/>";

            string res = TXmlConnector.ConnectorSendCommand(cmd);

            return res;
        }
        public static string ChangePass(string data)
        {
            string[] pass = data.Split(",");

            string cmd = String.Format("<command id=\"change_pass\" oldpass=\"{0}\" newpass=\"{1}\"/>", pass[0], pass[1]);

            string res = TXmlConnector.ConnectorSendCommand(cmd);

            return res;
        }
        
        public static string GetConnectorVersion()
        {
            string cmd = "<command id=\"get_connector_version\"/>";

            string res = TXmlConnector.ConnectorSendCommand(cmd);

            return res;
        }
        public static string GetServerId()
        {
            string cmd = "<command id=\"get_server_id\"/>";

            string res = TXmlConnector.ConnectorSendCommand(cmd);

            return res;
        }
        public static string NewOrder(string orderData)
        {
            string cmd = FormatOrderCmd("neworder", orderData);

            string res = TXmlConnector.ConnectorSendCommand(cmd);

            return res;
        }

        public static string NewCondOrder(string orderData)
        {
            string cmd = FormatOrderCmd("newcondorder", orderData);

            string res = TXmlConnector.ConnectorSendCommand(cmd);

            return res;
        }

        public static string NewStopOrder(string orderData)
        {
            string cmd = FormatOrderCmd("newstoporder", orderData);

            string res = TXmlConnector.ConnectorSendCommand(cmd);

            return res;
        }

        public static string CancelOrder(string orderid)
        {
            string cmd = "<command id=\"cancelorder\"><transactionid>" + orderid + "</transactionid></command>";

            string res = TXmlConnector.ConnectorSendCommand(cmd);

            return res;
        }

        public static string MoveOrder(string data)
        {
            string[] pars = data.Split(",");

            string cmd = "<command id=\"moveorder\">";

            cmd += "<transactionid>" + pars[0] + "</transactionid>";
            cmd += "<price>" + pars[1] + "</price>";
            cmd += "<moveflag>" + pars[2] + "</moveflag>";
            cmd += "<quantity>" + pars[3] + "</quantity>";

            cmd += "</command>";

            string res = TXmlConnector.ConnectorSendCommand(cmd);

            return res;
        }
        public static string CancelStopOrder(string orderid)
        {
            string cmd = "<command id=\"cancelstoporder\"><transactionid>" + orderid + "</transactionid></command>";

            string res = TXmlConnector.ConnectorSendCommand(cmd);

            return res;
        }
        
        public static string GetSecInfo(string data)
        {
            string[] secData = data.Split(",");

            string cmd = String.Format("<command id=\"get_securities_info\"><security><market>{0}" +
                "</market><seccode>{1}</seccode></security></command>", secData[0], secData[1]);

            string res = TXmlConnector.ConnectorSendCommand(cmd);

            return res;
        }
        
        public static string GetUnitedEquity(string union)
        {
            string cmd = String.Format("<command id=\"get_united_equity\" union=\"{0}\" />", union);

            string res = TXmlConnector.ConnectorSendCommand(cmd);

            return res;
        }

        public static string GetUnitedGo(string union)
        {
            string cmd = String.Format("<command id=\"get_united_go\" union=\"{0}\" />", union);

            string res = TXmlConnector.ConnectorSendCommand(cmd);

            return res;
        }
        
        public static string GetMcPortfolio(string data)
        {
            string[] pars = data.Split(",");

            string cmd = String.Format("<command id=\"get_mc_portfolio\" client=\"{0}\" union=\"{1}\" " +
                "currency=\"{2}\" asset=\"{3}\" money=\"{4}\" depo=\"{5}\" registers=\"{6}\" " +
                "maxbs=\"{7}\"/>", pars[0], pars[1], pars[2], pars[3], pars[4], pars[5], pars[6], pars[7]);

            string res = TXmlConnector.ConnectorSendCommand(cmd);

            return res;
        }

        public static string GetMaxBuySell(string data)
        {
            string[] pars = data.Split(";");
            string client = pars[0];
            string union = pars[1];

            string[] markets = pars[2].Split(",");
            string[] seccodes = pars[3].Split(",");

            string cmd = String.Format("<command id=\"get_max_buy_sell\" client=\"{0}\" union=\"{1}\">", client, union);

            for (int i = 0; i < markets.Length; i++)
            {
                cmd += "<security><market>" + markets[i] + "</market><seccode>" + seccodes[i] + "</seccode></security>";
            }

            cmd += "</command>";

            string res = TXmlConnector.ConnectorSendCommand(cmd);

            return res;
        }

        public static string GetClnSecPermissions(string data)
        {
            string[] pars = data.Split(",");

            string cmd = String.Format("<command id=\"get_cln_sec_permissions\"><security><board>{0}</board><seccode>{1}</seccode>" +
                "</security><client>{2}</client><union>{3}</union></command>", pars[0], pars[1], pars[2], pars[3]);

            string res = TXmlConnector.ConnectorSendCommand(cmd);

            return res;
        }

        public static string Disconnect()
        {
            string cmd = "<command id=\"disconnect\"/>";
            log.WriteLog("SendCommand: " + cmd);

            string res = TXmlConnector.ConnectorSendCommand(cmd);

            log.WriteLog("Disconnect");

            return res;
        }
    }
}