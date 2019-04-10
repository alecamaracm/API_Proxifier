using AlecaLOLSharedLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace APIProxifierBaseClient
{
    public abstract class APIRequest
    {
        public APIBaseClient baseClient;

        public bool isEncrypted = false;

               
        public List<string> APIargs = new List<string>();

        public int timeoutMS = 15000;

        public byte endpoint;

        public DateTime startTime;

        SslStream sslStream;


        public APIRequest(APIBaseClient _baseClient)
        {
            baseClient = _baseClient;
        }

        protected async Task<RequestResult> execute()
        {

            RequestResult req = new RequestResult();

            await Task.Run(() => {
                startTime = DateTime.Now;

              
                TcpClient client = new TcpClient();
                try
                {
                    client.Connect(baseClient.ip, baseClient.port);

                }
                catch (Exception ex)
                {
                    req.errorOcurred = true;
                    req.error = "Can´t connect to the server: " + ex.Message;

                }

                NetworkStream stream = client.GetStream();

                if (req.errorOcurred == false)
                {
                    if (isEncrypted)
                    {
                        stream.WriteByte(1); //Encrypted? flag
                        stream.Flush();
                        sslStream = new SslStream(stream, false, new RemoteCertificateValidationCallback(ValidateServerCertificate), null);

                        sslStream.AuthenticateAsClient("alecalol.com");

                        while (sslStream.IsAuthenticated == false)
                        {
                            Thread.Sleep(10);
                        }
                    }
                    else
                    {
                        stream.WriteByte(0); //Encrypted? flag
                        stream.Flush();
                    }

                    if (isEncrypted)
                    {
                        sslStream.WriteByte(endpoint);
                    }
                    else
                    {
                        stream.WriteByte(endpoint);
                    }


                    for (int i = 0; i < APIargs.Count; i++)
                    {
                        if (APIargs[i] == null) APIargs[i] = "";
                        APIargs[i] = APIargs[i].Replace(":", "&,.,&");
                    }

                    byte[] toSend = UTF8Encoding.UTF8.GetBytes(String.Join(":", APIargs));

                    if (isEncrypted)
                    {
                        sslStream.Write(BitConverter.GetBytes((int)toSend.Length));
                        sslStream.Write(toSend, 0, toSend.Length);
                        sslStream.Flush();
                    }
                    else
                    {
                        byte[] lenBuff = BitConverter.GetBytes((int)toSend.Length);
                        stream.Write(lenBuff, 0, lenBuff.Length);
                        stream.Write(toSend, 0, toSend.Length);
                    }

                    stream.Flush();
                    stream.ReadTimeout = timeoutMS;  //Just the maximum time
                    if (isEncrypted) sslStream.ReadTimeout = timeoutMS;  //Just the maximum time

                }


                int errorInServer = -1;

                if (req.errorOcurred == false)
                {
                    try
                    {
                        if (isEncrypted)
                        {
                            errorInServer = sslStream.ReadByte();
                        }
                        else
                        {
                            errorInServer = stream.ReadByte();
                        }

                    }
                    catch
                    {
                        req.errorOcurred = true;
                        req.error = "Timeout when waiting for server successful flag";
                    }
                }



                int toRead = -1;

                Thread.Sleep(500);

                if (req.errorOcurred == false)
                {
                    try
                    {
                        byte[] bufff = new byte[4];
                        if (isEncrypted)
                        {
                            sslStream.Read(bufff, 0, 4);
                        }
                        else
                        {
                            stream.Read(bufff, 0, 4);
                        }
                        toRead = BitConverter.ToInt32(bufff, 0);
                    }
                    catch
                    {
                        req.errorOcurred = true;
                        req.error = "Timeout when waiting for response lenght";
                    }

                    if (toRead < 0)
                    {
                        req.errorOcurred = true;
                        req.error = "Invalid response lenght";
                    }
                }

                req.data = new string[0];

                Thread.Sleep(300);

                if (req.errorOcurred == false)
                {
                    byte[] rawData = new byte[toRead];
                    try
                    {
                        if (isEncrypted)
                        {
                            sslStream.Read(rawData, 0, toRead);
                        }
                        else
                        {
                            stream.Read(rawData, 0, toRead);
                        }

                    }
                    catch
                    {
                        req.errorOcurred = true;
                        req.error = "Timeout when reading data";
                    }

                    req.data = UTF8Encoding.UTF8.GetString(rawData).Split(':');

                    for (int i = 0; i < req.data.Length; i++)
                    {
                        req.data[i] = req.data[i].Replace("&,.,&", ":");
                    }
                }


                req.totalMS = (int)(DateTime.Now - startTime).TotalMilliseconds;

                if (errorInServer == 1)
                {
                    req.errorOcurred = true;
                    req.error = req.data[0];
                }
            });
            return req;
        }


        private static bool ValidateServerCertificate(object sender,  X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
          
            if (sslPolicyErrors == SslPolicyErrors.None)
                return true;

            Console.WriteLine("Certificate error: {0}", sslPolicyErrors);

            // Do not allow this client to communicate with unauthenticated servers.
            return false;
        }

    }

    public class RequestResult
    {
        public string[] data;
        public bool errorOcurred = false;
        public string error = "";
        public int totalMS;

        public override string ToString()
        {
            StringBuilder stringBuilder = new StringBuilder();

            stringBuilder.Append(errorOcurred ? ("Error-" + totalMS + "ms-" + error) : "Success-" + totalMS + "ms");
            
            return stringBuilder.ToString();
        }
    }

    public abstract class BaseResponse
    {
        
        public RequestResult result;
    }


}
