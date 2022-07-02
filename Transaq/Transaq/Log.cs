using System;
using System.IO;
using System.Linq;
using System.Text;

namespace Transaq
{
    static class log
    {
        static private bool LogFlag = false; // флаг записи лог-файла
        static private StreamWriter LogFile; // переменная лог-файла


        //================================================================================
        public static void WriteLog(string log_str)
        {
            if (LogFlag)
            {
                LogFile.WriteLine(DateTime.Now.ToString("HH:mm:ss.fff") + " " + log_str);
                LogFile.Flush();
            }
        }

        public static void StartLogging(string path)
        {
            if (!File.Exists(path)) File.Create(path).Close();
            LogFile = File.AppendText(path);
            LogFlag = true;
            WriteLog("START LOGGING");
        }

        public static void StopLogging()
        {
            if (LogFlag)
            {
                WriteLog("STOP LOGGING");
                LogFile.Close();
            }
            LogFlag = false;
        }

    }
}
