using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace APIProxifierBaseServer
{
    public abstract class EndpointHandler
    {
        public const int LOGGING_BUFFER_SIZE = 1;
        public const float TIME_DIFFERENCE_TO_LOG = 1.05f;
        public const int COOLDOWN_AFTER_HIGH_TIME = 5; //Number of times where the average time won´t be affected after a high time

        Logger logger;

        public string dataToLog="";

        string endpointName;
        int averageFinishMS = -1;
        int highTimeModeLeft = -1;

        EndpointLoggingMode loggingMode = EndpointLoggingMode.All;

       


        public EndpointHandler(Logger _logger, string _endpointName, EndpointLoggingMode _loggingMode,string folderLogPath="/",TextBox textBox=null)
        {
            logger = _logger;
            endpointName = _endpointName;
            loggingMode = _loggingMode;
            logger.TryCreate(endpointName,folderLogPath,textBox);

        }


        //Returns true if there has been an error (errorString can be modified if so)
        public abstract bool Handle(ref string[] data, out string[] returnData, ref string errorString, ServerConnection serverConnection);

        public abstract void HandleLog(ServerConnection connection);
        

        CompletedOperation[] operationsToLog = new CompletedOperation[LOGGING_BUFFER_SIZE];
        int currentOperation = 0;

        public void AddLog(string extraData, ServerConnection serverConnection,string specialLogReason="")
        {
            string logReason = specialLogReason;

            if(logReason=="")
            {
                if (connectionMustBeLogged(serverConnection, out logReason) == false) return;
            }          

            lock (operationsToLog)
            {
                if (currentOperation >= operationsToLog.Length) //No more space, probably flush is not working
                {
                    return;
                }

                operationsToLog[currentOperation] = new CompletedOperation();
                operationsToLog[currentOperation].errorOcurred = serverConnection.errorOcurred;
                operationsToLog[currentOperation].errorString = serverConnection.error;
                operationsToLog[currentOperation].totalTime = (int)serverConnection.totalTime.ElapsedMilliseconds;
                operationsToLog[currentOperation].processingTime = (int)serverConnection.precesingTime.ElapsedMilliseconds;
                operationsToLog[currentOperation].finishTime = (int)serverConnection.workEndedTime.ElapsedMilliseconds;
                operationsToLog[currentOperation].logReason = logReason;
                operationsToLog[currentOperation].IP = serverConnection.remoteIP;
                operationsToLog[currentOperation].extraData = extraData;
                operationsToLog[currentOperation].time =DateTime.Now;

                currentOperation++;

                if (currentOperation >= operationsToLog.Length) //No more space, needs to be flushed
                {

                    if (FlushLog() == false)
                    {
                        Console.WriteLine("Couldn´t flush log for endpoint: " + endpointName);
                        return;
                    }
                }

            }
        }

        private bool connectionMustBeLogged(ServerConnection serverConnection,out string reason)
        {                       
            if (serverConnection.errorOcurred)
            {
                reason = "Error";
                return (loggingMode & EndpointLoggingMode.Errors) != 0;
            }

            if (serverConnection.workEndedTime.ElapsedMilliseconds >= 15 && averageFinishMS * TIME_DIFFERENCE_TO_LOG < serverConnection.workEndedTime.ElapsedMilliseconds)  //Allow a minimum of 15ms to be considered "normal"
            {
                highTimeModeLeft = COOLDOWN_AFTER_HIGH_TIME;
                reason = "High time";
                return true;
            }

            if(highTimeModeLeft<=-1) //If averaging is not in cooldown
            {
                if(averageFinishMS==-1) //First time is its logging
                {
                    averageFinishMS = (int)serverConnection.workEndedTime.ElapsedMilliseconds;
                }
                else
                {
                    averageFinishMS = (int)(averageFinishMS * 0.75f + serverConnection.workEndedTime.ElapsedMilliseconds * 0.25f);
                }
            }else
            {
                highTimeModeLeft--;
            }

            if((loggingMode & EndpointLoggingMode.Info)!=0)
            {
                reason = "Info";
                return true;
            }

            reason = ":(";
            return false;            
        }

        public bool FlushLog()
        {
            try
            {
                for (int i = 0; i < currentOperation; i++)
                {
                    CompletedOperation operation = operationsToLog[i];
                    if (operation.errorOcurred)
                    {
                        logger.Log(endpointName, operation.logReason, operation.IP, operation.errorString+" Extra data: "+operation.extraData, operation.time, operation.finishTime);
                    }
                    else
                    {
                        logger.Log(endpointName, operation.logReason, operation.IP, operation.extraData, operation.time, operation.finishTime);
                    }


                }

                currentOperation = 0;
            }
            catch
            {
                return false;
            }
          
            return true;
        }
    }

    public class CompletedOperation
    {
        public bool errorOcurred = false;
        public string errorString;

        public string extraData; //Any data that needs to be loged

        public int processingTime; //Data processing
        public int totalTime; //Until the conneciton is deleted form memory
        public int finishTime; //Read "work" time. Networking+processing

        public DateTime time;

        public string IP;

        public string logReason;
    }

    public enum EndpointLoggingMode
    {
        Default=0,  //Not to be used!
        Errors=1,  //Errors
        HugeDelays=2, //Opoerations that took longer than usual
        Info=4, //All operations that are not errors and take a usual amount of time
        All= EndpointLoggingMode.Errors| EndpointLoggingMode.HugeDelays | EndpointLoggingMode.Info, //Everything        
    }
}