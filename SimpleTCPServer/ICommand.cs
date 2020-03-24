using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tz.SimpleTCPSocket.Server.Command
{
    public interface ICommand
    {
        /** 获取该Command处理的“服务器收到的帧类型值T”
         */
        UInt16 GetT();

        /** 处理服务器收到的帧
         */
        void Execute(BaseSession session, Common.Frame frame);
    }
}
