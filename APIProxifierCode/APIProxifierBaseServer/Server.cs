using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace APIProxifierBaseServer
{
    public  abstract class APIBaseServer
    {
        TcpListener listener;
        List<ServerConnection> clients = new List<ServerConnection>();

        bool mustStop = false;

        public delegate bool ActionHandler(ref string[] data,out string[] returnData,ref string errorString,ServerConnection connection);
        public delegate void LogHandler(ServerConnection connection);
        public Dictionary<byte, ActionHandler> handlers = new Dictionary<byte, ActionHandler>();
        public Dictionary<byte, LogHandler> logHandler = new Dictionary<byte, LogHandler>();

        

        X509Certificate cert;

        public void Start(int portNumber)
        {
            cert = X509Certificate.CreateFromSignedFile(Environment.CurrentDirectory + "\\Certs\\alecalolPK.pfx");

            clientCreatorThread = new Thread(clientCreator);
            clientCreatorThread.IsBackground = true;
            clientCreatorThread.SetApartmentState(ApartmentState.STA);
            clientCreatorThread.Start(portNumber);

            clientRemoverThread=new Thread(clientRemover);
            clientRemoverThread.IsBackground = true;
            clientRemoverThread.SetApartmentState(ApartmentState.STA);
            clientRemoverThread.Start();
        }

        public void AddHandler(byte endpointID, EndpointHandler handler)
        {
            if(handlers.ContainsKey(endpointID))
            {
                throw new ArgumentException("A handler with the same ID already exists!");
            }

            if(handler==null)
            {
                throw new ArgumentException("Null handler");
            }

            handlers.Add(endpointID, handler.Handle);
            logHandler.Add(endpointID, handler.HandleLog);
        }

        void clientCreator(object objPort)
        {
            listener = new TcpListener(IPAddress.Any,(int) objPort);
            listener.Start();
            while (mustStop == false)
            {
                TcpClient client = listener.AcceptTcpClient();
           
                ServerConnection connection = new ServerConnection(client,this,cert);
     
                connection.DoWork();
         
                clients.Add(connection);
            }

        }

        void clientRemover()
        {
            while(mustStop==false)
            {
                for(int i=0;i<clients.Count;i++)
                {
                    if (i >= clients.Count) break;
                    if(clients[i].status==ServerConnectionStatus.Done)
                    {
                        clients.RemoveAt(i);
                        i--;
                    }
                }
                Thread.Sleep(300);
            }
        }

        Thread clientCreatorThread;
        Thread clientRemoverThread;

        public string test()
        {
            return "YEPU!";
        }

        public int getNumberOfClients()
        {
            if (clients == null) return 0;
            return clients.Count;
        }
    }
}
