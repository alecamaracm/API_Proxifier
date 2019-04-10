using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace APIProxifierBaseServer
{
    public class ServerConnection
    {

        const int MAX_REQUEST_LENGHT= 3 * 1000; //Bytes
        const int MS_FOR_CLIENT_TIMEOUT = 5 * 1000; //ms

        byte[] dataReceived;
        byte[] returnData;

        byte endpoint;

        TcpClient client;

        NetworkStream stream;

        public string remoteIP;

        X509Certificate cert;

        int dataCountToReceive = 0;

        public bool encrypted = false;
        SslStream sslStream;

        public Stopwatch totalTime = new Stopwatch();
        public Stopwatch workEndedTime = new Stopwatch();
        public Stopwatch precesingTime = new Stopwatch();

        DateTime connectionStartTime;

        public bool errorOcurred = false;
        public string error = "";

        APIBaseServer server;

        public ServerConnectionStatus status = ServerConnectionStatus.Default;

        public ServerReceivingStatus receivingStatus;

        public ServerConnection(TcpClient _client,APIBaseServer _server,X509Certificate certificate)
        {
            totalTime.Start();
            workEndedTime.Start();
            server = _server;
            client = _client;
            connectionStartTime = DateTime.Now;

            cert = certificate;
          

        }

        public void DoWork()
        {
            Task.Factory.StartNew(()=> { Worker(); });
        }

        private void Worker()
        {
            status = ServerConnectionStatus.Receiving;
            remoteIP = (client.Client.RemoteEndPoint as IPEndPoint).Address + ":" + (client.Client.RemoteEndPoint as IPEndPoint).Port;
            stream = client.GetStream();

            stream.ReadTimeout = MS_FOR_CLIENT_TIMEOUT;

            try
            {
                int mustBeEncrypted = stream.ReadByte();
                //int mustBeEncrypted = 1;

                if (mustBeEncrypted==0)
                {
                    encrypted = false;
                }else if(mustBeEncrypted==1)
                {
                    encrypted = true;
                }else
                {
                    error = "Wrong encryption flag.";
                    errorOcurred = true;
                    finish();
                }              
            }
            catch
            {
                error = "Couldn´t receive encryption flag.";
                errorOcurred = true;
                finish();
            }

            if(errorOcurred==false)
            {
                if (encrypted)
                { 
                    sslStream = new SslStream(stream,false);
                    sslStream.ReadTimeout = MS_FOR_CLIENT_TIMEOUT;
                    sslStream.AuthenticateAsServer(cert);
                }               
            }


            if (errorOcurred == false)
            {
                try
                {                  
                    if (encrypted)
                    {
                        endpoint = (byte)sslStream.ReadByte();

                    }
                    else
                    {
                        endpoint = (byte)stream.ReadByte();
                     
                    }
                    if (server.handlers.ContainsKey(endpoint)==false)
                    {
                        error = "Uknown endpoint ID";
                        errorOcurred = true;

                        finish();
                    }
                    }
                catch
                {
                    error = "Couldn´t receive endpoint ID.";
                    errorOcurred = true;

                    finish();
                  
                }
            }

            if (errorOcurred == false)
            {
                try
                {
                    byte[] buff = new byte[4];
                    if(encrypted)
                    {
                        sslStream.Read(buff, 0, 4);
                    }
                    else
                    {
                        stream.Read(buff, 0, 4);
                    }
                    dataCountToReceive = BitConverter.ToInt32(buff, 0);
                }
                catch
                {
                    error = "Couldn´t receive request data lenght.";
                    errorOcurred = true;
                    finish();
             
                }
            }

            if(errorOcurred==false)
            {
                if (dataCountToReceive > MAX_REQUEST_LENGHT)
                {
                    error = "Max request lenght exceded.";
                    errorOcurred = true;
                    finish();
                }
                else if (dataCountToReceive < 0)
                {
                    error = "Invalid request lenght.";
                    errorOcurred = true;
                    finish();
                }
                else
                {
                    receivingStatus = ServerReceivingStatus.Data;
                }
            }

            if (errorOcurred==false&& dataCountToReceive>0)
            {
                try
                {
                    dataReceived = new byte[dataCountToReceive];
                    if (encrypted)
                    {
                        sslStream.Read(dataReceived, 0, dataCountToReceive);
                    }
                    else
                    {
                        stream.Read(dataReceived, 0, dataCountToReceive);
                    }
                }catch
                {
                    error = "Couldn´t receive request data.";
                    errorOcurred = true;
                    finish();
                  
                }

            }

            status = ServerConnectionStatus.Working;
            precesingTime.Start();
            if(errorOcurred==false)
            {  //Everything is fine, execute request
                try
                {
                    errorOcurred=processMessage(dataReceived,out returnData,ref error);
                }catch (Exception ex)
                {
                    error = "A general error has ocurred when executing the request: " + ex.Message;
                    errorOcurred = true;
                    finish();
                }
            }
            precesingTime.Stop();
            status = ServerConnectionStatus.Sending;

            try
            {
                if(errorOcurred)
                {
                    returnData= Encoding.UTF8.GetBytes(error);
                }

                if(encrypted)
                {
                    sslStream.WriteByte(errorOcurred ? (byte)1 : (byte)0); //Successful or error;
                    sslStream.Write(BitConverter.GetBytes(returnData.Length));
                    sslStream.Write(returnData, 0, returnData.Length);

                    sslStream.Flush();
                }
                else
                {
                    stream.WriteByte(errorOcurred ? (byte)1 : (byte)0); //Successful;
                    byte[] lenBuff = BitConverter.GetBytes(returnData.Length);
                    stream.Write(lenBuff,0,lenBuff.Length);
                    stream.Write(returnData, 0, returnData.Length);
                }

                stream.Flush();
                stream.Close();
                               
              
            }
            catch(Exception ex)
            {
                error = "An error has ocurred when sending the response: " + ex.Message;
                errorOcurred = true;

            }finally
            {
                finish();
            }


           

            //Finish the connection
        }

        private bool processMessage(byte[] dataReceived, out byte[] returnData,ref string errorString)  //Returns true if there is an error. If so, errorString can be modified
        {
            string data="";
            if(dataReceived!=null)data= UTF8Encoding.UTF8.GetString(dataReceived);
            returnData = null;
            
            if(server.handlers.ContainsKey(endpoint))
            {
                string[] desdata = null;
                try
                {
                    if(dataReceived!=null)
                    {
                        desdata = UTF8Encoding.UTF8.GetString(dataReceived).Split(':');
                        for (int i = 0; i < desdata.Length; i++)
                        {
                            desdata[i] = desdata[i].Replace("&,.,&", ":");
                        }
                    }
                 
                }catch
                {
                    errorString = "Can not deserialize data";
                    return true;
                }
                string[] returnArray;

                bool result=server.handlers[endpoint](ref desdata, out returnArray,ref errorString,this);

                if (result == false)
                {
                    for(int i=0;i<returnArray.Length;i++)
                    {
                        returnArray[i] = returnArray[i].Replace(":", "&,.,&");
                    }
                    returnData = UTF8Encoding.UTF8.GetBytes(String.Join(":",returnArray));
                }

                return result;
            }else
            {
                errorString = "Uknown endpoint ID";
                return true;
            }

        }

        public void finish()
        {
            workEndedTime.Stop();
            try
            {
                if(server.logHandler.ContainsKey(endpoint))  server.logHandler[endpoint](this);
            }
            catch
            {
            }
          
            status = ServerConnectionStatus.Done;
        }
    }

    public enum ServerConnectionStatus
    {
        Default,
        Receiving,
        Working,
        Sending,
        Done,
    }

    public enum ServerReceivingStatus
    {
        DataSize,
        Data,
        Done,
    }
}