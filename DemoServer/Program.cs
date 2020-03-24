using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

using Tz.SimpleTCPSocket.Common;

namespace DemoServer
{
    class Program
    {
        static MyServer server;

        static void Main(string[] args)
        {
            int type = 2;

            if(type == 1)
            {
                #region 第一种类型的TCP服务器：应答式服务器（客户端发送一帧，服务器回应一帧）

                LauchServerType1();

                #endregion
            }
            else if (type == 2)
            {
                #region 第二种类型的TCP服务器：支持服务器主动推送的应答式服务器（除了客户端发送一帧，服务器回应一帧，服务器还可以随时向客户端推送帧）

                LauchServerType2();

                #endregion
            }

            while (true) System.Threading.Thread.Sleep(1000);
        }

        static void LauchServerType1()
        {
            /** 
             * Command目录实现了4个帧类型值：Hello、Bye、Heartbeat和UploadFile
             * CmdHello : 服务器回应Hello字符串
             * CmdBye : 服务器回应Bye字符串，并且关闭Session
             * CmdHeartbeart : 服务器收到什么就回什么
             * CmdUploadFile : 上传文件，该帧是嵌套帧，即CmdUploadFile帧体包含2个TLV帧，第一个TLV帧是文件名，第二个TLV帧是文件的二进制数据
             */
            try
            {
                server = new MyServer("127.0.0.1", 2020);

                Console.WriteLine("Server UUID : " + server.GetUUID());
                Console.WriteLine("Server Status : " + server.GetStatus().ToString());

                server.Start();
            }
            catch (System.Exception ex)
            {
                server = null;
                Console.WriteLine("创建和启动服务器发生异常：" + ex.Message);
            }

            Console.WriteLine("如果要停止服务器，请输入quit，然后回车：");
            while (true)
            {
                string str = Console.ReadLine();
                if (str == "quit")
                {
                    if (server != null)
                    {
                        try
                        {
                            server.Stop();
                            server = null;
                        }
                        catch (System.Exception ex)
                        {
                            server = null;
                            Console.WriteLine("停止服务器发生异常：" + ex.Message);
                        }
                    }
                    break;
                }
                else
                {
                    Console.WriteLine("如果要停止服务器，请输入quit，然后回车：");
                }
            }
        }


        static CancellationTokenSource cts = new CancellationTokenSource();
        static void TaskTimer()
        {
            byte[] body = Encoding.UTF8.GetBytes("系统通知：这是一个测试！");

            UInt16 sn = 0;
            Frame frame = new Frame(Frame.MakeSerialNumber(true, sn), ((UInt16)Command.EMyCommand.SERVER_PUSH), body);

            while (!cts.IsCancellationRequested)
            {
                Thread.Sleep(1000);

                if (sn >= (UInt16)0x7fff)
                    sn = 0;
                else
                    ++sn;
                frame.UpdateFrameSerialNumber(Frame.MakeSerialNumber(true, sn));

                List<string> session_uuids = DemoRegisterSession.GetAll();
                foreach (string session_uuid in session_uuids)
                {
                    server.PushToClient(session_uuid, frame, (su, frm, rlt) => {
                        Console.WriteLine("服务器主动推送结果：" + rlt.ToString());
                    });
                }
            }
        }
        static void LauchServerType2()
        {
            /** 
             * LauchServerType2跟LauchServerType1相比，其他不变，只是服务器会定时（1000毫秒）向客户端发送ServerPush帧
             */            
            
            Task task_timer = new Task(TaskTimer, cts.Token);
            try
            {
                server = new MyServer("127.0.0.1", 2020);

                Console.WriteLine("Server UUID : " + server.GetUUID());
                Console.WriteLine("Server Status : " + server.GetStatus().ToString());

                server.Start();

                task_timer.Start();
            }
            catch (System.Exception ex)
            {
                server = null;
                Console.WriteLine("创建和启动服务器发生异常：" + ex.Message);
            }

            Console.WriteLine("如果要停止服务器，请输入quit，然后回车：");
            while (true)
            {
                string str = Console.ReadLine();
                if (str == "quit")
                {
                    if (server != null)
                    {
                        try
                        {
                            cts.Cancel();
                            task_timer.Wait();

                            server.Stop();
                            server = null;
                        }
                        catch (System.Exception ex)
                        {
                            server = null;
                            Console.WriteLine("停止服务器发生异常：" + ex.Message);
                        }
                    }
                    break;
                }
                else
                {
                    Console.WriteLine("如果要停止服务器，请输入quit，然后回车：");
                }
            }
        }
    }
}
