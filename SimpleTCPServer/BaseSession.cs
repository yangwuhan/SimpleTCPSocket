using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.IO;
using Tz.SimpleTCPSocket.Common;

namespace Tz.SimpleTCPSocket.Server
{
    public abstract class BaseSession
    {
        /** UUID
         */
        private string __uuid;

        /** 会话（连接）是否有效，socket.IsConnected不可靠，通常只能通过send和receive结果来置该属性
         */
        private bool __is_connected;

        /** 客户端Socket
         */
        private Socket __client_socket;

        /** Accept该Socket的时间
         */
        private DateTime __accept_time;

        /** 上次成功Recv或Send的时间
         */ 
        private DateTime? __last_communication_time;

        /** 发送缓冲区最大字节数
         */
        public static int MAX_BUFFER_SEND_SIZE = 1024 * 1024;

        /** 接收缓冲区最大字节数
         */
        public static int MAX_BUFFER_RECV_SIZE = 1024 * 1024;

        /** 发送缓冲区
         */
        private byte[] __buffer_send;

        /** 接收缓冲区
         */
        private byte[] __buffer_recv;

        /** 构造函数
         */
        public BaseSession(Socket sk, DateTime accept_time)
        {
            if (sk == null)
                throw new Exception("无效参数");
            __buffer_send = new byte[MAX_BUFFER_SEND_SIZE];
            __buffer_recv = new byte[MAX_BUFFER_RECV_SIZE];
            __uuid = Guid.NewGuid().ToString("N").ToUpper();
            __client_socket = sk;
            __accept_time = accept_time;
            __last_communication_time = null;
            __is_connected = true;
        }

        /** 获取UUID
         */
        public string GetUUID()
        {
            return __uuid;
        }

        /** 获取链接是否有效
         */ 
        public bool GetIsConnected()
        {
            return __is_connected;
        }

        /** 获取链接是否有数据可读
         */
        public bool GetIsReadable()
        {
            if (!__is_connected)
                return false;
            if (__client_socket.Available > 0)
                return true;
            else
                return false;
        }

        /** 获取Socket被Accept的时间
         */
        public DateTime GetAcceptTime()
        {
            return __accept_time;
        }

        /** 上次成功Send或Recv的时间
         */
        public DateTime? GetLastCommunicationTime()
        {
            return __last_communication_time;
        }

        /** 获取客户端IP地址
         */
        public string GetClientIP()
        {
            if (!__is_connected)
                return null;
            return ((IPEndPoint)__client_socket.RemoteEndPoint).Address.ToString();
        }

        /** 置Session对象为断开状态
         */ 
        public void SetOffline()
        {
            if (!__is_connected)
                return;
            try
            {
                __client_socket.Close();
                __is_connected = false;
            }
            catch { }
        }

        /** 发送字节数组
         */
        private void __Send(byte[] data, int offset = 0, int len = 0)
        {
            if (!__is_connected)
                throw new Exception("连接已经关闭");
            if (data == null || data.Length == 0)
                throw new Exception("无效参数");
            int to_send = (len <= 0 ? (data.Length - offset) : len);
            if(offset + to_send > data.Length)
                throw new Exception("非法参数");
            int sended = 0;
            while(sended < to_send)
            {
                int sed = __client_socket.Send(data, offset + sended, to_send - sended, SocketFlags.None);
                if (sed <= 0)
                    throw new Exception("发送错误");
                sended += sed;
            }
        }
        
        /** 发送帧
         */
        public bool Send(Frame frame)
        {
            if (frame == null)
                return false;
            if (!__is_connected)
                return false;
            try
            {
                //发送帧头
                byte[] head = frame.GetHeadBytes();
                __Send(head);

                //发送帧体 - 字节数组部分
                byte[] body_bytes = frame.GetBodyBytes();
                if(body_bytes != null && body_bytes.Length > 0)
                    __Send(body_bytes);

                //发送帧体 - 流对象部分
                if(frame.IsBodyHasDataInStream())
                {
                    var stm = frame.GetBodyStream();
                    UInt32 stm_size = frame.GetBodySteamSize();
                    UInt32 block_count = (stm_size / (UInt32)__buffer_send.Length) + (stm_size % (UInt32)__buffer_send.Length > 0 ? (UInt32)1 : (UInt32)0);
                    for (UInt32 i = 0; i < block_count; ++i)
                    {
                        UInt32 lft = stm_size % (UInt32)__buffer_send.Length;
                        UInt32 to_copy = (i == block_count - 1 ? (lft > 0 ? lft : (UInt32)__buffer_send.Length) : (UInt32)__buffer_send.Length);
                        stm.Read(__buffer_send, 0, (int)to_copy);
                        __Send(__buffer_send, 0, (int)to_copy);
                    }
                }               
                __last_communication_time = DateTime.Now;
                return true;
            }
            catch (System.Exception ex)
            {
                __client_socket.Close();
                __is_connected = false;
                Console.WriteLine("Session.Send发生异常，" + ex.Message);
                return false;
            }
        }

        /** 接收帧体
         */
        private void __Recv(UInt32 L, out byte[] frame_body, out BinaryReader stream, bool use_stream = false)
        {
            frame_body = null;
            stream = null;
            string file_path = string.Empty;
            FileStream fs = null;
            BinaryWriter bw = null;
            if (use_stream == false)
                frame_body = new byte[L];
            else
            {
                string temp = Environment.GetEnvironmentVariable("TEMP");
                DirectoryInfo di = new DirectoryInfo(temp);
                string directory = di.FullName;
                if (string.IsNullOrEmpty(directory))
                    throw new Exception("临时目录为空");
                if (directory[directory.Length - 1] != '\\')
                    directory += @"\";
                file_path = directory + "SimpleTCPServer." + Guid.NewGuid().ToString("N") + ".fb";
                fs = new FileStream(file_path, FileMode.Create);
                bw = new BinaryWriter(fs);
            }
            UInt32 block_count = 1;
            if (L > __buffer_recv.Length)
                block_count = (L / (UInt32)__buffer_recv.Length) + (L % (UInt32)__buffer_recv.Length > 0 ? (UInt32)1 : (UInt32)0);
            UInt32 last_block_size = (L % (UInt32)__buffer_recv.Length);
            if (last_block_size == 0)
                last_block_size = (UInt32)__buffer_recv.Length;
            for(UInt32 i = 0; i < block_count; ++i)
            {
                UInt32 block_size = (i == block_count - 1 ? last_block_size : (UInt32)__buffer_recv.Length);
                UInt32 reced = 0;
                while(reced < block_size)
                {
                    int red = __client_socket.Receive(__buffer_recv, (int)reced, (int)(block_size - reced), SocketFlags.None);
                    if (red <= 0)
                        throw new Exception("接收错误");
                    reced += (UInt32)red;
                }
                if(use_stream == false)
                    Array.Copy(__buffer_recv, 0, frame_body, i * __buffer_recv.Length, block_size);
                else
                {
                    bw.Write(__buffer_recv, 0, (int)block_size);
                }
            }
            if (use_stream == false)
                return;
            else
            {
                bw.Close();
                bw.Dispose();
                bw = null;
                fs.Close();
                fs.Dispose();
                fs = null;
                fs = new FileStream(file_path, FileMode.Open);
                stream = new BinaryReader(fs);
                return;
            }
        }

        /** 接收帧
         */
        public Frame Recv()
        {
            if (!__is_connected)
                return null;
            try
            {
                #region 接收帧头
                int reced = 0;
                while(reced < 8)
                {
                    int red = __client_socket.Receive(__buffer_recv, reced, 8 - reced, SocketFlags.None);
                    if (red <= 0)
                        throw new Exception("接收数据发生错误");
                    reced += red;
                }
                #endregion

                UInt16 S = BitConverter.ToUInt16(__buffer_recv, 0);
                UInt16 T = BitConverter.ToUInt16(__buffer_recv, 2);
                UInt32 L = BitConverter.ToUInt32(__buffer_recv, 4);

                Frame frame = null;                
                if (L > Frame.MAX_FRAME_BODY_BYTE_SIZE) //超过最大限制，字节数组用null，全部用流对象，这样方便后续对body部分进行解析（嵌套Frame）
                {
                    byte[] tmp = null;
                    BinaryReader br = null;
                    __Recv(L, out tmp, out br, true);
                    frame = new Frame(S, T, null, true, br, L);
                }
                else
                {
                    if(L == 0)
                        frame = new Frame(S, T, null);
                    else //未超过最大限制，用字节数组的方式
                    {
                        byte[] frame_body = null;
                        BinaryReader tmp = null;
                        __Recv(L, out frame_body, out tmp);
                        frame = new Frame(S, T, frame_body);
                    }
                }
                __last_communication_time = DateTime.Now;
                return frame;
            }
            catch (System.Exception ex)
            {
                __client_socket.Close();
                __is_connected = false;
                Console.WriteLine("Session.Recv发生异常，" + ex.Message);
                return null;
            }
        }

        /** 刚建立Socket连接时被调用
         */
        public virtual void OnSessionStart()
        {
            Console.WriteLine("Session - OnSessionStart");
        }

        /** 服务器收到未知的T时被调用
         */
        public virtual void OnSessionUnkownT(Frame frame)
        {
            Console.WriteLine("Session - OnSessionUnkownT");
        }

        /** 服务器端调用ICommand派生类的Exec发生异常时被调用
         */
        public virtual void OnSessionExcept(Frame frame, Exception ex)
        {
            Console.WriteLine("Session - OnSessionExcept");
        }

        /** 连接断开后被调用，注意，连接断开后，不可再调用Send函数（调用Send函数会发生异常）
         */
        public virtual void OnSessionStop()
        {
            Console.WriteLine("Session - OnSessionStop");
        }
    }
}
