using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.IO;
using Tz.SimpleTCPSocket.Common;

namespace DemoClient
{
    class Program
    {
        static Socket socket = null;

        static void Main(string[] args)
        {
            int type = 2;
            if(type == 1)
            {
                #region 测试应答式服务器（客户端发送一帧，服务器回应一帧）

                test1();

                #endregion
            }
            else
            {
                #region 测试支持服务器主动推送的应答式服务器（除了客户端发送一帧，服务器回应一帧，服务器还可以随时向客户端推送帧）

                test2();

                #endregion

            }

            while (true) System.Threading.Thread.Sleep(1000);
        }

        static void Send(Socket socket, byte[] data, int offset = 0, int len = 0)
        {
            int to_send = (len <= 0 ? (data.Length - offset) : len);
            if (offset + to_send > data.Length)
                throw new Exception("非法参数");
            int sended = 0;
            while (sended < to_send)
            {
                int sed = socket.Send(data, offset + sended, to_send - sended, SocketFlags.None);
                if (sed <= 0)
                    throw new Exception("发送错误");
                sended += sed;
            }
        }
        static bool Send(Socket socket, Frame frame)
        {
            try
            {
                //发送帧头
                byte[] head = frame.GetHeadBytes();
                Send(socket, head);

                //发送帧体的字节数组部分
                byte[] body_bytes = frame.GetBodyBytes();
                if (body_bytes != null && body_bytes.Length > 0)
                    Send(socket, body_bytes);

                //发送帧体的流对象部分
                if (frame.IsBodyHasDataInStream())
                {
                    var stm = frame.GetBodyStream();
                    UInt32 stm_size = frame.GetBodySteamSize();
                    byte[] buffer_send = new byte[1024 * 1024]; //每次发送1MBytes
                    UInt32 block_count = (stm_size / (UInt32)buffer_send.Length) + (stm_size % (UInt32)buffer_send.Length > 0 ? (UInt32)1 : (UInt32)0);
                    for (UInt32 i = 0; i < block_count; ++i)
                    {
                        UInt32 lft = stm_size % (UInt32)buffer_send.Length;
                        UInt32 to_copy = (i == block_count - 1 ? (lft > 0 ? lft : (UInt32)buffer_send.Length) : (UInt32)buffer_send.Length);
                        stm.Read(buffer_send, 0, (int)to_copy);
                        Send(socket, buffer_send, 0, (int)to_copy);
                    }
                }
                return true;
            }
            catch (System.Exception ex)
            {
                Console.WriteLine("Send发生异常，" + ex.Message);
                return false;
            }
        }

        /** 接收帧
         */
        static Frame Recv(Socket socket)
        {
            try
            {
                var buffer_recv = new byte[1024 * 1024];
                #region 接收帧头
                int reced = 0;
                while (reced < 8)
                {
                    int red = socket.Receive(buffer_recv, reced, 8 - reced, SocketFlags.None);
                    if (red <= 0)
                    {
                        socket.Close();
                        Console.WriteLine("接收数据发生错误");
                        return null;
                    }
                    reced += red;
                }
                #endregion

                UInt16 S = BitConverter.ToUInt16(buffer_recv, 0);
                UInt16 T = BitConverter.ToUInt16(buffer_recv, 2);
                UInt32 L = BitConverter.ToUInt32(buffer_recv, 4);

                byte[] frame_body = new byte[L];
                reced = 0;
                while (reced < L)
                {
                    int red = socket.Receive(frame_body, reced, (int)L - reced, SocketFlags.None);
                    if (red <= 0)
                    {
                        socket.Close();
                        Console.WriteLine("接收数据发生错误");
                        return null;
                    }
                    reced += red;
                }
                
                var frame = new Frame(S, T, frame_body);
                return frame;
            }
            catch (System.Exception ex)
            {
                Console.WriteLine("Recv发生异常，" + ex.Message);
                return null;
            }
        }

        static void test1()
        {
            #region 连接服务器
            try
            {
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                socket.Connect("127.0.0.1", 2020);
            }
            catch (System.Exception ex)
            {
                socket = null;
                Console.WriteLine("连接服务器发生异常," + ex.Message);
            }
            #endregion

            UInt16 SN = 0;

            #region 发送Hello
            if (socket != null)
            {
                byte[] body = Encoding.UTF8.GetBytes("Hello World !");
                Frame frame_hello = new Frame(Frame.MakeSerialNumber(false, SN++), (UInt16)DemoServer.Command.EMyCommand.HELLO, body);

                Console.WriteLine("发送Hello");
                //发送Hello
                Send(socket, frame_hello);

                //接收Hello
                try
                {
                    Frame frame_ret = Recv(socket);
                    string info = Encoding.UTF8.GetString(frame_ret.GetBodyBytes());
                    Console.WriteLine("接收Hello的回应：" + info);
                }
                catch (System.Exception ex)
                {
                    socket.Close();
                    socket = null;
                    Console.WriteLine("接收Hello的回应发生异常：" + ex.Message);
                }
            }
            #endregion

            #region 发送Heartbeat
            if (socket != null)
            {
                byte[] body = Encoding.UTF8.GetBytes("Heartbeart " + DateTime.Now.ToString());
                Frame frame_hello = new Frame(Frame.MakeSerialNumber(false, SN++), (UInt16)DemoServer.Command.EMyCommand.HEARTBEART, body);

                Console.WriteLine("发送Heartbeart");
                //发送Hello
                Send(socket, frame_hello);

                //接收Hello
                try
                {
                    Frame frame_ret = Recv(socket);
                    string info = Encoding.UTF8.GetString(frame_ret.GetBodyBytes());
                    Console.WriteLine("接收Heartbeart的回应：" + info);
                }
                catch (System.Exception ex)
                {
                    socket.Close();
                    socket = null;
                    Console.WriteLine("接收Heartbeart的回应发生异常：" + ex.Message);
                }
            }
            #endregion

            #region 发送文件 - 小文件
            if (socket != null)
            {
                //文件名子帧
                byte[] body_file_name = Encoding.UTF8.GetBytes("test_small_file.data");
                int L_body_file_name = body_file_name.Length;
                Frame frame_file_name = new Frame(0, (UInt16)DemoServer.Command.EMyCommand.FILENAME, body_file_name);

                //文件数据子帧
                byte[] body_file_data = new byte[Tz.SimpleTCPSocket.Common.Frame.MAX_FRAME_BODY_BYTE_SIZE - 16 - body_file_name.Length];
                for (int i = 0; i < body_file_data.Length; ++i)
                    body_file_data[i] = 55;
                int L_body_file_data = body_file_data.Length;
                Frame frame_file_data = new Frame(0, (UInt16)DemoServer.Command.EMyCommand.FILEDATA, body_file_data);

                //要发送的帧
                byte[] data = new byte[16 + frame_file_name.GetBodyBytes().Length + frame_file_data.GetBodyBytes().Length];
                Array.Copy(frame_file_name.GetHeadBytes(), 0, data, 0, 8);
                Array.Copy(body_file_name, 0, data, 8, L_body_file_name);
                Array.Copy(frame_file_data.GetHeadBytes(), 0, data, 8 + L_body_file_name, 8);
                Array.Copy(body_file_data, 0, data, 8 + L_body_file_name + 8, L_body_file_data);
                Frame frame_small_file = new Frame(Frame.MakeSerialNumber(false, SN++), (UInt16)DemoServer.Command.EMyCommand.UPLOADFILE, data);

                Console.WriteLine("发送小文件（test_small_file.data）");
                //发送小文件
                Send(socket, frame_small_file);

                //接收小文件回应
                try
                {
                    Frame frame_ret = Recv(socket);
                    string info = Encoding.UTF8.GetString(frame_ret.GetBodyBytes());
                    Console.WriteLine("接收小文件的回应：" + info);
                }
                catch (System.Exception ex)
                {
                    socket.Close();
                    socket = null;
                    Console.WriteLine("接收小文件的回应发生异常：" + ex.Message);
                }
            }
            #endregion

            #region 发送文件 - 大文件
            if (socket != null)
            {
                //文件名子帧
                string fn = "test_big_file.mp4";
                byte[] body_file_name = Encoding.UTF8.GetBytes(fn);
                int L_body_file_name = body_file_name.Length;
                Frame frame_file_name = new Frame(0, (UInt16)DemoServer.Command.EMyCommand.FILENAME, body_file_name);

                //要发送的帧的帧体的字节数组部分（1）
                byte[] body_bytes_send = new byte[8 + frame_file_name.GetTotalBodySize() + 8];
                Array.Copy(frame_file_name.GetHeadBytes(), 0, body_bytes_send, 0, 8);
                Array.Copy(frame_file_name.GetBodyBytes(), 0, body_bytes_send, 8, frame_file_name.GetTotalBodySize());

                //打开文件数据的流对象
                string exe_path = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
                int pos = exe_path.LastIndexOf('\\');
                string path = exe_path.Substring(0, pos + 1);
                string file_path = path + fn;
                FileStream fs = new FileStream(file_path, FileMode.Open);
                UInt32 file_size = (UInt32)fs.Length;
                BinaryReader br = new BinaryReader(fs);

                //文件数据子帧
                UInt32 L_body_file_data = file_size;
                Frame frame_file_data = new Frame(0, (UInt16)DemoServer.Command.EMyCommand.FILEDATA, null, true, br, L_body_file_data);

                //要发送的帧的帧体的字节数组部分（2）
                Array.Copy(frame_file_data.GetHeadBytes(), 0, body_bytes_send, 8 + frame_file_name.GetTotalBodySize(), 8);

                //要发送的帧
                Frame frame_big_file = new Frame(Frame.MakeSerialNumber(false, SN++), (UInt16)DemoServer.Command.EMyCommand.UPLOADFILE, body_bytes_send, true, br, L_body_file_data);

                Console.WriteLine("发送大文件（" + fn + "）");
                //发送大文件
                Send(socket, frame_big_file);

                //关闭文件数据的流对象
                fs.Close();
                fs.Dispose();
                br.Close();
                br.Dispose();

                //接收大文件的回应
                try
                {
                    Frame frame_ret = Recv(socket);
                    string info = Encoding.UTF8.GetString(frame_ret.GetBodyBytes());
                    Console.WriteLine("接收大文件的回应：" + info);
                }
                catch (System.Exception ex)
                {
                    socket.Close();
                    socket = null;
                    Console.WriteLine("接收大文件的回应发生异常：" + ex.Message);
                }
            }
            #endregion

            #region 发送Bye
            if (socket != null)
            {
                byte[] body = Encoding.UTF8.GetBytes("Bye Bye !");
                Frame frame_bye = new Frame(Frame.MakeSerialNumber(false, SN++), (UInt16)DemoServer.Command.EMyCommand.BYE, body);

                Console.WriteLine("发送Bye");
                //发送Bye
                Send(socket, frame_bye);

                //接收Bye
                try
                {
                    Frame frame_ret = Recv(socket);
                    string info = Encoding.UTF8.GetString(frame_ret.GetBodyBytes());
                    Console.WriteLine("接收Bye的回应：" + info);
                }
                catch (System.Exception ex)
                {
                    socket.Close();
                    socket = null;
                    Console.WriteLine("接收Bye的回应发生异常：" + ex.Message);
                }
            }
            #endregion

            #region 发送Hello - 检测发送Bye后，Session是否被服务器关闭
            if (socket != null)
            {
                byte[] body = Encoding.UTF8.GetBytes("Hello World !");
                Frame frame_hello = new Frame(Frame.MakeSerialNumber(false, SN++), (UInt16)DemoServer.Command.EMyCommand.HELLO, body);

                Console.WriteLine("发送Hello");
                //发送Hello
                Send(socket, frame_hello);

                //接收Hello
                try
                {
                    Frame frame_ret = Recv(socket);
                    string info = Encoding.UTF8.GetString(frame_ret.GetBodyBytes());
                    Console.WriteLine("接收Hello的回应：" + info);
                }
                catch (System.Exception ex)
                {
                    socket.Close();
                    socket = null;
                    Console.WriteLine("接收Hello的回应发生异常：" + ex.Message);
                }
            }
            #endregion
        }

        static void test2()
        {
            Task task_recv = new Task(()=> {
                try
                {
                    while (true)
                    {
                        Frame frame = Recv(socket);

                        switch(frame.GetFrameType())
                        {
                            case (UInt16)DemoServer.Command.EMyCommand.HELLO:
                                {
                                    string info = Encoding.UTF8.GetString(frame.GetBodyBytes());
                                    Console.WriteLine("收到服务器："+info+"【HELLO】");
                                }
                                break;
                            case (UInt16)DemoServer.Command.EMyCommand.HEARTBEART:
                                {
                                    string info = Encoding.UTF8.GetString(frame.GetBodyBytes());
                                    Console.WriteLine("收到服务器：" + info + "【HEARTBEART】");
                                }
                                break;
                            case (UInt16)DemoServer.Command.EMyCommand.SERVER_PUSH:
                                {
                                    string info = Encoding.UTF8.GetString(frame.GetBodyBytes());
                                    Console.WriteLine("收到服务器：" + info + "【SERVER_PUSH】");
                                }
                                break;
                        }
                    }
                }
                catch { }
            });

            Task task_send = new Task(() => {
                try
                {
                    UInt16 SN = 0;

                    Frame frame_hello = new Frame(Frame.MakeSerialNumber(false, SN++), (UInt16)DemoServer.Command.EMyCommand.HELLO, Encoding.UTF8.GetBytes("Hello World !"));
                    Send(socket, frame_hello);

                    while(true)
                    {
                        System.Threading.Thread.Sleep(1000);

                        Frame frame_heartbeat = new Frame(Frame.MakeSerialNumber(false, SN++), (UInt16)DemoServer.Command.EMyCommand.HEARTBEART, Encoding.UTF8.GetBytes("心跳!"));
                        Send(socket, frame_heartbeat);

                    }
                }
                catch { }
            });

            #region 连接服务器
            try
            {
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                socket.Connect("127.0.0.1", 2020);

                task_recv.Start();
                task_send.Start();
            }
            catch (System.Exception ex)
            {
                socket = null;
                Console.WriteLine("连接服务器发生异常," + ex.Message);
            }
            #endregion
        }
    }
}
