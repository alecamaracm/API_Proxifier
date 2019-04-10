using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace APIProxifierBaseServer
{
    public class Logger
    {
        Dictionary<string, LoggerInstance> loggers = new Dictionary<string, LoggerInstance>();


        public void TryCreate(string name,string folderPath="/",TextBox textBox=null)
        {
            if (loggers.ContainsKey(name)) return;

            LoggerInstance instance = new LoggerInstance();
            instance.name = name;
            instance.folderPath = folderPath;
            instance.textBox = textBox;
            instance.day = DateTime.Now;
            instance.writer = generateWriter(instance);

            loggers.Add(name, instance);
        }

        public void Log(string endpointName, string logReason, string iP, string errorString,DateTime time,int finishingTime)
        {
            LoggerInstance instance = loggers[endpointName];
            lock(instance)
            {
                try
                {
                    if (DateTime.Now.Day != instance.day.Day)
                    {
                        instance.day = DateTime.Now;
                        instance.writer.Close();
                        instance.writer = generateWriter(instance);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("An error has ocurred when reopening a log writer for " + endpointName + ": " + ex.Message);
                }

                try
                {
                    string str = String.Format("[{0}] [{1}] [{5}]-[{2}] ({4}ms) - {3}", time.ToShortTimeString(), iP, logReason, errorString,finishingTime,endpointName);

                    if (instance.textBox != null)
                    {
                        instance.textBox.Invoke((MethodInvoker)delegate () {
                            instance.textBox.AppendText(str+Environment.NewLine);
                        });
                    }
                    instance.writer.WriteLine(str);
                    instance.writer.Flush();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("An error has ocurred when logging for " + endpointName + ": " + ex.Message);
                }
            }
           
        }

        StreamWriter generateWriter(LoggerInstance instance)
        {
            string path = Environment.CurrentDirectory + "/Logs/" + instance.day.Year + "-" + instance.day.Month.ToString("00") + "-" + instance.day.Day.ToString("00") + "/" + instance.folderPath + "/";
            if (Directory.Exists(path) == false) Directory.CreateDirectory(path);
            return new StreamWriter(path + instance.name + ".txt",true);
        }
    }

    public class LoggerInstance
    {
        public StreamWriter writer;
        public TextBox textBox;
        public string name;
        public string folderPath;
        public DateTime day;
    }
}
