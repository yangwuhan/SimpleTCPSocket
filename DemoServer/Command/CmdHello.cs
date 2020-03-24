using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tz.SimpleTCPSocket.Common;
using Tz.SimpleTCPSocket.Server;
using Tz.SimpleTCPSocket.Server.Command;

namespace DemoServer.Command
{
    public class CmdHello : ICommand
    {
        const EMyCommand _cmd = EMyCommand.HELLO; //将帧处理器和帧类型值的枚举关联起来，一一对应，如果多个帧处理器定义了同一个帧类型值的枚举，系统只取第一个

        /** 获取该Command处理的“服务器收到的帧类型值T”
         */
        public UInt16 GetT()
        {
            return (UInt16)_cmd; //帧类型值的枚举，其整数值就是帧类型值T
        }

        /** 处理服务器收到的帧
         */
        public void Execute(BaseSession session, Frame frame)
        {            
            if(frame.IsBodyHasDataInStream() == false && frame.GetTotalBodySize() > 0)
            {
                //客户端发过来的是UFT-8字符串
                byte[] body = frame.GetBodyBytes();
                string info = Encoding.UTF8.GetString(body, 0, body.Length);
                Console.WriteLine("客户端发过来：" + info + "【CmdHello】");
            }

            //服务器回应"Hello"字符串
            string replay = "Hello";
            byte[] replay_body = Encoding.UTF8.GetBytes(replay);
            Frame frm_send = new Frame(frame.GetFrameSerialNumber(), GetT(), replay_body); //要发给客户端的帧
            session.Send(frm_send);
        }
    }
}
