using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DemoServer.Command
{
    /** 为帧类型值定义易于记忆的枚举
     */ 
    enum EMyCommand
    {
        UNKONWT = 0x7fff,
        EXCEPT = 0x7ffe,
        HELLO = 1,      
        BYE = 2,
        HEARTBEART = 3,
        UPLOADFILE = 4,
        FILENAME = 5,
        FILEDATA = 6,
        SERVER_PUSH = 7
    }
}
