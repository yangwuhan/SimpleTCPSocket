using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tz.SimpleTCPSocket.Server;
using Tz.SimpleTCPSocket.Common;

namespace DemoServer
{
    class MyServer : BaseServer<MyConfig, MySession>
    {
        public MyServer(string local_ip, int local_port)
            : base(local_ip, local_port)
        {

        }

        protected override void OnServerStart()
        {
            Console.WriteLine("MyServer - OnServerStart");
        }

        /** 服务器Stop时调用，虚函数可被重载，派生类可用于服务器对象的资源释放
         */
        protected override void OnServerStop()
        {
            Console.WriteLine("MyServer - OnServerStop");
        }
    }
}
