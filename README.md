# SimpleTCPSocket
  这是一个采用STLV帧结构的简单的TCP服务器开发框架。  
  这个框架可以简化TCP服务器的开发。  
  TCP协议是面向数据流的协议，TCP服务器和TCP客户端之间交互来往的数据也是流数据。当我们开发网络程序时，大都要对流数据进行切割，从流数据中分解出我们自己定义的数据帧，然后，进一步对数据帧进行业务处理。  
  这个框架将切割流数据的工作封装起来，让使用者直接获取到数据帧，从而达到简化开发的目的。  
  使用这个框架时，使用者不需要关心流数据怎样切割，而只需要从ICommand接口派生出帧数据处理类，将数据帧的业务处理逻辑，编写到ICommand的Exec函数即可。对于使用者从ICommand接口派生出的类，框架会自动通过反射获取，不需要使用者再另外注册一次。  
  框架使用的帧结构简称是STLV，S表示序列号（2字节）、T表示帧类型（2字节）、L表示数据帧体字节数（4字节，即帧体最大可达到4G）、V表示数据帧体（即用户数据）。  
