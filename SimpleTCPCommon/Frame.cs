using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tz.SimpleTCPSocket.Common
{
    /** 帧类型
     *  帧采用STLV的结构：
     *  S - Serial Number 序列号，每个帧的唯一识别码，可以重复使用（无论是客户端还是服务器端，将接收到了的帧处理完后，就丢弃了这个帧的唯一识别码）；
     *      共2字节16个Bit，最高的bit位代表谁生成的序列号（0表示客户端生成的序列号【客户端主动发给服务器】，1表示服务器生成的序列号【服务器主动推送给客户端】），
     *                      剩余的15个Bit为序列号，取值范围0x0000~0x7fff，共32767个值，因为不可能系统同时存在32767个还未处理完的帧，因此32767个足够使用了；
     *  T - Type 帧类型，用于表示帧的内容识别码，共2字节32个Bit，取值范围0x0000~0xffff，共65535个值；
     *  L - Length 帧体的长度，用于表示帧体（V部分）的字节数目，共4字节64个Bit，因此，帧体最大可以达到4G字节；
     *  V - 帧体，用于放置帧的内容，共0字节~4G字节，可以是字节流、字符串、Json数据等等。
     */
    public class Frame
    {
        public const int MAX_FRAME_BODY_BYTE_SIZE = 10 * 1024 * 1024; //字节数组最多字节数，超过该值的部分要放到流对象里面

        /** 生成序列号
         */ 
        public static UInt16 MakeSerialNumber(bool is_server_push, UInt16 serial_number)
        {
            if (serial_number > 0x7fff)
                throw new Exception("参数错误，serial_numeber的值超过了0x7fff");
            if (is_server_push)
                return (UInt16)((UInt16)0x8000 | serial_number);
            else
                return serial_number;
        }

        /** 构造函数
         * frame_serial_number : 帧的序列号；
         * frame_type : 帧的类型值；
         * frame_body_byte : 帧的body的字节数组部分，最大不能超过MAX_FRAME_BODY_BYTE_SIZE，如果帧的Body部分超过这个最大值，则多余的要放到流里面；
         * frame_body_has_more_data_in_stream ：帧的body的除了有字节数组部分外，是否还有流对象部分；
         * frame_body_stream ：帧的body的流对象部分，frame_body_has_more_data_in_stream为true时有效；
         * frame_body_stream_size ：frame_body_stream流最大可读字节数，frame_body_has_more_data_in_stream为true时有效。
         */
        public Frame(UInt16 frame_serial_number, UInt16 frame_type, byte[] frame_body_byte, bool frame_body_has_more_data_in_stream = false, System.IO.BinaryReader frame_body_stream = null, UInt32 frame_body_stream_size = 0)
        {
            __frame_serial_number = frame_serial_number;
            __frame_type = frame_type;

            if (frame_body_byte != null && frame_body_byte.Length > MAX_FRAME_BODY_BYTE_SIZE)
                throw new Exception("参数错误");
            if (frame_body_byte == null || frame_body_byte.Length == 0)
                __frame_body_byte = null; //没有帧体（帧体长度为0），或者帧体数据都在流对象里面
            else
                __frame_body_byte = frame_body_byte;

            if (!frame_body_has_more_data_in_stream)
            {
                __frame_body_has_more_data_in_stream = false;
                __frame_body_stream = null;
                __frame_body_stream_size = 0;
            }
            else
            {
                if(frame_body_stream == null || frame_body_stream_size == 0)
                    throw new Exception("参数错误");
                __frame_body_has_more_data_in_stream = true;
                __frame_body_stream = frame_body_stream;
                __frame_body_stream_size = frame_body_stream_size;
            }            
        }

        /** 获取帧的序列号（S）
         */
        public UInt16 GetFrameSerialNumber()
        {
            return __frame_serial_number;
        }

        /** 更改帧的序列号（S）
         */
        public void UpdateFrameSerialNumber(UInt16 serial_number)
        {
            __frame_serial_number = serial_number;
        }

        /** 获取帧类型值（T）
         */
        public UInt16 GetFrameType()
        {
            return __frame_type;
        }

        /**  获取帧体的总字节数（L）
         */
        public UInt32 GetTotalBodySize()
        {
            return (__frame_body_byte == null ? (UInt32)0 : (UInt32)__frame_body_byte.Length) +
                (__frame_body_has_more_data_in_stream ? (UInt32)__frame_body_stream_size : (UInt32)0);
        }

        /**  获取帧体字节数组的字节数
         */
        public UInt32 GetBodyBytesSize()
        {
            return (__frame_body_byte == null ? (UInt32)0 : (UInt32)__frame_body_byte.Length);
        }

        /** 获取帧体是否包含流对象
         */
        public bool IsBodyHasDataInStream()
        {
            return __frame_body_has_more_data_in_stream;
        }

        /**  获取帧体的流对象的字节数
         */
        public UInt32 GetBodySteamSize()
        {
            return (__frame_body_has_more_data_in_stream ? (UInt32)__frame_body_stream_size : (UInt32)0);
        }

        /** 获取帧头的字节数组（TL => Byte Array）
         */
        public byte[] GetHeadBytes()
        {
            var S = BitConverter.GetBytes(__frame_serial_number);
            var T = BitConverter.GetBytes(__frame_type);
            var L = BitConverter.GetBytes(GetTotalBodySize());
            var ret = new byte[8];
            Array.Copy(S, 0, ret, 0, 2);
            Array.Copy(T, 0, ret, 2, 2);
            Array.Copy(L, 0, ret, 4, 4);
            return ret;
        }

        /** 获取帧体的字节数组
         */ 
        public byte[] GetBodyBytes()
        {
            return __frame_body_byte;
        }

        /** 获取帧体的流对象
         */
        public System.IO.BinaryReader GetBodyStream()
        {
            return (__frame_body_has_more_data_in_stream ? __frame_body_stream : null);
        }

        private UInt16 __frame_serial_number;
        private UInt16 __frame_type;
        private byte[] __frame_body_byte;
        private bool __frame_body_has_more_data_in_stream; 
        private System.IO.BinaryReader __frame_body_stream;
        private UInt32 __frame_body_stream_size; 
    }
}
