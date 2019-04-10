using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIProxifierBaseClient
{
    public class APIBaseClient
    {
        public string ip = "";
        public int port;

        public APIBaseClient(string _address,int _port)
        {
            ip = _address;
            port = _port;
        }
        public string test()
        {
            return "YEPA";
        }

        
    }
}
