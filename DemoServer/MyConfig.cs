using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tz.SimpleTCPSocket.Server;
using Tz.SimpleTCPSocket.Common;

namespace DemoServer
{
    class MyConfig : BaseConfig
    {
        /** 一个服务器对象支持的最大TCP连接数目
         */ 
        public override int MaxConnectionCount { get { return 10000; } }

        /** 一个服务器的每个客户端IP最大可以有几个连接
         */ 
        public override int MaxConnectionCountPerIP { get { return 10; } }

        /** 帧体的最大字节数（注意：服务器会预先将帧完整接收再交给应用程序处理，因此，帧体越大，则越影响服务器的性能）
         */ 
        public override UInt32 MaxFrameBodySize { get { return 20 * 1024 * 1024; } }

        /** 超过多少秒未发送或接收数据的Session会被服务器关闭
         */
        public override UInt32 MaxKeepLiveSecond { get { return 60; } }
    }
}
