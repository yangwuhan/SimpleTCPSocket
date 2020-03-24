using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Tz.SimpleTCPSocket.Common;
using Tz.SimpleTCPSocket.Server;
using Tz.SimpleTCPSocket.Server.Command;

namespace DemoServer.Command
{
    public class CmdUploadFile : ICommand
    {
        const EMyCommand _cmd = EMyCommand.UPLOADFILE; //将帧处理器和帧类型值的枚举关联起来，一一对应，如果多个帧处理器定义了同一个帧类型值的枚举，系统只取第一个

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
            Console.WriteLine("客户端发过来：上传文件（"+frame.GetTotalBodySize()+"字节）【CmdUploadFile】");

            UInt16 SN = 0;

            //上传文件使用这样的嵌套的 TLV 帧结构，TLV1(V1=TLV2+TLV3)：T1=UPLOADFILE，T2=FILENAME，T3=FILEDATA
            UInt16 t_file_name = 0; //T2 - FILENAME（文件名）
            UInt16 t_file_data = 0; //T3 - FILEDATA（文件数据）

            //检查是大型帧（没有字节数组，只有流对象），还是小型帧（只有字节数组，没有流对象）
            BinaryReader br = null;
            if (frame.IsBodyHasDataInStream())
                br = frame.GetBodyStream(); //大型帧

            #region STLV2 - FILENAME（文件名）

            //帧序列号 - 子帧的序列号是0
            if (frame.IsBodyHasDataInStream())
                SN = br.ReadUInt16();
            else
                SN = BitConverter.ToUInt16(frame.GetBodyBytes(), 0);

            //帧类型值
            if (frame.IsBodyHasDataInStream())
                t_file_name = br.ReadUInt16();
            else
                t_file_name = BitConverter.ToUInt16(frame.GetBodyBytes(), 2);
            if (t_file_name != (UInt16)EMyCommand.FILENAME)
                throw new Exception("无文件名子帧");

            //帧体长度
            UInt32 L_file_name = 0;
            if (frame.IsBodyHasDataInStream())
                L_file_name = br.ReadUInt32();
            else
                L_file_name = BitConverter.ToUInt32(frame.GetBodyBytes(), 4);
            byte[] fnb = new byte[L_file_name];

            //帧体
            if (frame.IsBodyHasDataInStream())
                br.Read(fnb, 0, fnb.Length);
            else
                Array.Copy(frame.GetBodyBytes(), 8, fnb, 0, L_file_name);
            string file_name = Encoding.UTF8.GetString(fnb);
            #endregion

            #region 文件保存到当前目录
            string exe_path = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
            int pos = exe_path.LastIndexOf('\\');
            string path = exe_path.Substring(0, pos + 1);
            string file_path = path + file_name;
            #endregion

            FileStream fs = new FileStream(file_path, FileMode.Create);
            BinaryWriter bw = new BinaryWriter(fs);

            #region 文件数据

            //帧序列号 - 子帧的序列号是0
            if (frame.IsBodyHasDataInStream())
                SN = br.ReadUInt16();
            else
                SN = BitConverter.ToUInt16(frame.GetBodyBytes(), 8 + (int)L_file_name);

            //帧类型值
            if (frame.IsBodyHasDataInStream())
                t_file_data = br.ReadUInt16();
            else
                t_file_data = BitConverter.ToUInt16(frame.GetBodyBytes(), 10 + (int)L_file_name);
            if (t_file_data != (UInt16)EMyCommand.FILEDATA)
                throw new Exception("无文件数据子帧");

            //帧体
            UInt32 L_file_data = 0;
            if (frame.IsBodyHasDataInStream())
                L_file_data = br.ReadUInt32();
            else
                L_file_data = BitConverter.ToUInt32(frame.GetBodyBytes(), 8 + (int)L_file_name + 4);
            byte[] tmp = new byte[1024 * 1024]; //每次读1MBytes
            UInt32 block_count = (L_file_data / (UInt32)tmp.Length) + (L_file_data % (UInt32)tmp.Length > 0 ? (UInt32)1 : (UInt32)0);
            for (int i = 0; i < block_count; ++i)
            {
                UInt32 last_block_size = (L_file_data % (UInt32)tmp.Length > 0 ? (L_file_data % (UInt32)tmp.Length) : (UInt32)tmp.Length);
                UInt32 block_size = (i == block_count - 1 ? last_block_size : (UInt32)tmp.Length);
                if (frame.IsBodyHasDataInStream())
                    br.Read(tmp, 0, (int)block_size);
                else
                    Array.Copy(frame.GetBodyBytes(), 8 + (int)L_file_name + 8 + i * tmp.Length, tmp, 0, block_size);
                bw.Write(tmp, 0, (int)block_size);
            }
            #endregion

            bw.Close();
            bw.Dispose();
            fs.Close();
            fs.Dispose();

            byte[] body = Encoding.UTF8.GetBytes("OK");
            Frame frm_send = new Frame(frame.GetFrameSerialNumber(), GetT(), body); //要发给客户端的帧
            session.Send(frm_send);
        }
    }
}
