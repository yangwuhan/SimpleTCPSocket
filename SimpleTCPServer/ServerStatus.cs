using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tz.SimpleTCPSocket.Server
{
    public enum EServerStatus
    {
        CREATED = 0,    //对象刚创建
        STOPPED = 1,    //对象成功调用过Stop了
        STARTED = 2,    //对象成功调用过Start了
    }

    public class ServerStatus
    {
        protected EServerStatus _val;

        public ServerStatus(EServerStatus es)
        {
            _val = es;
        }

        
    }
}
