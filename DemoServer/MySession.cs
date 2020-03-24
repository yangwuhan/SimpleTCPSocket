using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using Tz.SimpleTCPSocket.Common;
using Tz.SimpleTCPSocket.Server;

namespace DemoServer
{
    class MySession : BaseSession
    {
        public MySession(Socket sk, DateTime accept_time)
            : base(sk, accept_time)
        {

        }

        public override void OnSessionStart()
        {
            Console.WriteLine("MySession - OnSessionStart");

            DemoRegisterSession.RegisterSession(this.GetUUID());
        }

        public override void OnSessionUnkownT(Frame frame)
        {
            Console.WriteLine("MySession - OnSessionUnkownT");
            byte[] body = BitConverter.GetBytes(frame.GetFrameType());
            Frame ret = new Frame(frame.GetFrameSerialNumber(), (UInt16)Command.EMyCommand.UNKONWT, body);
            this.Send(ret);
        }

        public override void OnSessionExcept(Frame frame, Exception ex)
        {
            Console.WriteLine("MySession - OnSessionExcept : " + ex.Message);
            byte[] body = Encoding.UTF8.GetBytes(ex.Message);
            Frame ret = new Frame(frame.GetFrameSerialNumber(), (UInt16)Command.EMyCommand.EXCEPT, body);
            this.Send(ret);
        }

        public override void OnSessionStop()
        {
            DemoRegisterSession.UnRegisterSession(this.GetUUID());

            Console.WriteLine("MySession - OnSessionStop");
        }

    }
}
