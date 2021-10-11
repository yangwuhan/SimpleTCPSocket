using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Tz.SimpleTCPSocket.Common;

namespace Tz.SimpleTCPSocket.Server
{
    struct MyTask
    {
        public Task _task;
        public CancellationTokenSource _token_source;
    }

    public abstract class BaseServer<TConfig, TSession>
        where TConfig : BaseConfig
        where TSession : BaseSession 
    {
        /** 服务器对象的配置
         */
        protected TConfig _config; 

        /** 服务器对象的ID
         */
        protected string _uuid;

        /** 服务器对象的状态
         */
        protected EServerStatus _status; 

        /** 本机IP地址
         */
        protected IPAddress _local_ip;

        /** 本机Bind端口
         */
        protected int _local_port;

        /** 错误信息
         */
        protected string _error;
        protected object _error_lock;

        /** 服务器的所有帧处理对象（ICommand派生类对象）
         */
        protected Dictionary<UInt32, Command.ICommand> _dic_commands;

        /** TCPListener对象
         */
        protected TcpListener _tcp_listener;

        /** ClientSocket数组，以session的uuid为key
         *  等待推送给客户端的帧
         */
        protected Dictionary<string, TSession> _dic_sessions_idel;      //未被工作线程处理的Session
        protected Dictionary<string, TSession> _dic_sessions_dealing;   //正在被工作线程处理的Session
        protected Dictionary<string, List<dynamic>> _dic_wait_to_push;  //等待推送给客户端的帧
        protected object _dic_sessions_lock;

        /** 异步工作任务组
         */
        private MyTask[] __work_tasks;

        /** 构造函数
         */
        public BaseServer(string local_ip, int local_port)
        {
            _config = Activator.CreateInstance(typeof(TConfig)) as TConfig;

            _uuid = Guid.NewGuid().ToString("N").ToUpper();

            if (string.IsNullOrEmpty(local_ip) || string.IsNullOrEmpty(local_ip.Trim()))
                _local_ip = IPAddress.Any;
            else
                _local_ip = IPAddress.Parse(local_ip.Trim());

            if (local_port <= 0)
                throw new Exception("端口必须为正整数");
            _local_port = local_port;

            _error = "";
            _error_lock = new object();

            _GetAllCommands(out _dic_commands);

            _dic_sessions_idel = new Dictionary<string, TSession>();
            _dic_sessions_dealing = new Dictionary<string, TSession>();
            _dic_wait_to_push = new Dictionary<string, List<dynamic>>();
            _dic_sessions_lock = new object();

        }

        /** 获取UUID
         */
        public string GetUUID()
        {
            return _uuid;
        }

        /** 获取状态
         */
        public EServerStatus GetStatus()
        {
            return _status;
        }

        /** 启动服务器
         */ 
        public void Start()
        {
            if (_status != EServerStatus.CREATED && _status != EServerStatus.STOPPED)
                throw new Exception("状态不正确");

            OnServerStart();

            #region 工作异步任务
            __work_tasks = new MyTask[Environment.ProcessorCount * 2];
            for(int i = 0; i < __work_tasks.Length; ++i)
            {
                MyTask my_task = new MyTask();
                __work_tasks[i] = my_task;
                my_task._token_source = new CancellationTokenSource();
                my_task._task = Task.Factory.StartNew(new Action<object>(__WorkFunc), my_task._token_source as object);
            }

            #endregion

            #region 侦听

            try {
                _tcp_listener = new TcpListener(_local_ip, _local_port);
                _tcp_listener.Start();
            }
            catch (System.Exception ex)
            {
                _tcp_listener = null;
                throw ex;
            }

            try
            {
                _tcp_listener.BeginAcceptSocket(new AsyncCallback(_DoAcceptSocket), _tcp_listener);
            }
            catch(System.Exception ex)
            {
                _tcp_listener.Stop();
                _tcp_listener = null;
                throw ex;
            }

            #endregion

            _status = EServerStatus.STARTED;
        }

        /** 从一个Session数组获取IP地址的已连接数
         */ 
        private int __GetConnectionCountForIPFromSessionArray(string client_ip, Dictionary<string, TSession> dic_sessions)
        {
            int same_ip_count = 0;
            if (dic_sessions.Count > 0)
            {
                string[] keys = dic_sessions.Keys.ToArray();
                for (int i = 0; i < keys.Length; ++i)
                {
                    string key = keys[i];
                    var session = dic_sessions[key];
                    if (!session.GetIsConnected())
                        continue;
                    if (session.GetClientIP() == client_ip)
                        ++same_ip_count;
                }
            }
            return same_ip_count;
        }

        /** Accept New Client Socket
         */ 
        protected void _DoAcceptSocket(IAsyncResult ar)
        {
            TcpListener tcp_listener = ar.AsyncState as TcpListener;

            //获取客户端Socket
            Socket sk = null;
            try
            {
                sk = tcp_listener.EndAcceptSocket(ar);                
            }
            catch(System.Exception ex)
            {
                string info = "EndAcceptSocket发生异常，" + ex.Message;
                _SetError(info);
                return; //EndAcceptSocket发生异常，直接返回
            }

            //最大连接数限制
            int current_connection_count = 0;
            lock (_dic_sessions_lock)
            {
                current_connection_count = _dic_sessions_idel.Count;
                current_connection_count += _dic_sessions_dealing.Count;
            }
            
            if(current_connection_count < _config.MaxConnectionCount)
            {
                //同一个IP地址限制最大连接数
                int same_ip_count = 0;
                try
                {
                    int max_connection_per_ip = _config.MaxConnectionCountPerIP;
                    string client_ip = ((IPEndPoint)sk.RemoteEndPoint).Address.ToString();
                    lock (_dic_sessions_lock)
                    {
                        max_connection_per_ip = __GetConnectionCountForIPFromSessionArray(client_ip, _dic_sessions_idel);
                        max_connection_per_ip += __GetConnectionCountForIPFromSessionArray(client_ip, _dic_sessions_dealing);
                    }
                }
                catch (System.Exception ex)
                {
                    string info = "查询同一IP地址连接数发生异常，" + ex.Message;
                    _SetError(info); //查询同一IP地址连接数发生异常，这种情况跟TcpListener无关，不直接返回
                }

                //添加到Session
                if (same_ip_count < _config.MaxConnectionCountPerIP)
                {
                    try
                    {
                        sk.SendTimeout = 2000;
                        sk.ReceiveTimeout = 2000;
                        sk.NoDelay = true;
                        TSession session = Activator.CreateInstance(typeof(TSession), new object[2] { sk, DateTime.Now }) as TSession;
                        session.OnSessionStart();

                        lock (_dic_sessions_lock)
                        {
                            _dic_sessions_idel.Add(session.GetUUID(), session);
                        }
                    }
                    catch (System.Exception ex)
                    {
                        string info = "添加到Session发生异常，" + ex.Message;
                        _SetError(info); //添加到Session发生异常，这种情况跟TcpListener无关，不直接返回
                    }
                }
            }            
            
            //再次BeginAcceptSocket
            try
            {
                tcp_listener.BeginAcceptSocket(new AsyncCallback(_DoAcceptSocket), tcp_listener);
            }
            catch (System.Exception ex)
            {
                string info = "BeginAcceptSocket发生异常，" + ex.Message;
                _SetError(info);
                return; //BeginAcceptSocket发生异常，直接返回
            }
        }

        /** 停止服务器
         */
        public void Stop()  
        {
            if (_status != EServerStatus.STARTED)
                throw new Exception("状态不正确");

            #region 停止工作线程
            Task[] tasks = new Task[__work_tasks.Length];
            for(int i = 0; i < __work_tasks.Length; ++i)
            {
                MyTask my_task = __work_tasks[i];
                tasks[i] = my_task._task;
                my_task._token_source.Cancel();
            }
            Task.WaitAll(tasks);
            __work_tasks = null;
            #endregion

            #region 停止侦听
            try
            {
                _tcp_listener.Stop();
            }
            catch { }
            _tcp_listener = null;
            #endregion

            OnServerStop();

            _status = EServerStatus.STOPPED;
        }

        /** 获取错误信息
         */ 
        public string GetError()
        {
            string err = "";
            lock (_error_lock)
            {
                err = _error;
            }
            return err;
        }

        /** 设置错误信息
         */
        protected void _SetError(string error)
        {
            lock (_error_lock)
            {
                _error = error;
            }
        }

        /** 通过反射找到所有帧处理器（ICommand派生类对象）
         */
        protected void _GetAllCommands(out Dictionary<UInt32, Command.ICommand> dic_commands)
        {
            dic_commands = new Dictionary<uint, Command.ICommand>();
            try
            {
                var assembly = Assembly.GetEntryAssembly();
                var modules = assembly.GetModules();
                foreach (var module in modules)
                {
                    var types = module.GetTypes();
                    if (types == null || types.Length == 0)
                        continue;
                    foreach (var type in types)
                    {
                        if (!type.IsClass)
                            continue;
                        var ti = type.GetInterface("Tz.SimpleTCPSocket.Server.Command.ICommand");
                        if (ti == null)
                            continue;
                        var cmd = assembly.CreateInstance(type.FullName) as Command.ICommand;
                        UInt32 ui_cmd = cmd.GetT();
                        if (!dic_commands.ContainsKey(ui_cmd))
                            dic_commands.Add(ui_cmd, cmd);
                    }
                }
            }
            catch(System.Exception ex)
            {
                string info = "获取命令列表发生异常，" + ex.Message;
                _SetError(info);
            }
        }

        /** 服务器Start时调用，虚函数可被重载，派生类可用于服务器对象的初始化
         */
        protected virtual void OnServerStart()
        {
            Console.WriteLine("BaseServer - OnServerStart");
        }

        /** 服务器Stop时调用，虚函数可被重载，派生类可用于服务器对象的资源释放
         */
        protected virtual void OnServerStop()
        {
            Console.WriteLine("BaseServer - OnServerStop");
        }

        /** 将帧推送到客户端
         *  session_uuid : Session的uuid，可以在Session.OnSessionStart中通过this.GetUUID()得到
         *  frame : 要发送的帧
         *  on_push_result : 发送完毕后，服务器通知应用（参数1同session_uuid；参数2同frame；参数3表示推送结果：成功为true，失败为false）
         *  注意：frame不能使用流对象作为帧体（流对象一般用于大型数据帧，推送的帧一般较小，用不上大型数据帧），否则，返回false 
         */
        public bool PushToClient(string session_uuid, Frame frame, Action<string, Frame, bool> on_push_result)
        {
            if (string.IsNullOrEmpty(session_uuid) || string.IsNullOrEmpty(session_uuid.Trim()) || frame == null || frame.IsBodyHasDataInStream())
                return false;
            lock(_dic_sessions_lock)
            {
                List<dynamic> the_lst = null;
                if (_dic_wait_to_push.ContainsKey(session_uuid))
                    the_lst = _dic_wait_to_push[session_uuid];
                else
                {
                    the_lst = new List<dynamic>();
                    _dic_wait_to_push.Add(session_uuid, the_lst);
                }
                the_lst.Add(new
                {
                    Frame = frame,
                    OnPushResult = on_push_result
                });
            }
            return true;
        }

        /** 异步工作任务函数
         */ 
        private void __WorkFunc(object param)
        {
            CancellationTokenSource cts = param as CancellationTokenSource;
            while (!cts.IsCancellationRequested)
            {
                #region 检查是否有可读的要处理
                List<TSession> session_to_deal = new List<TSession>();
                lock (_dic_sessions_lock)
                {
                    if(_dic_sessions_idel.Count > 0)
                    {
                        string[] keys = _dic_sessions_idel.Keys.ToArray();
                        foreach (var key in keys)
                        {
                            TSession sessin = _dic_sessions_idel[key];
                            if (!sessin.GetIsConnected() || !sessin.GetIsReadable())
                                continue;
                            _dic_sessions_idel.Remove(key);
                            _dic_sessions_dealing.Add(key, sessin);
                            session_to_deal.Add(sessin);
                        }
                    }
                }
                #endregion

                #region 处理可读的
                if(session_to_deal.Count > 0)
                {
                    foreach(var session in session_to_deal)
                    {
                        //接收完整一帧
                        Frame frame = session.Recv();
                        if(frame == null)
                            session.SetOffline();
                        else
                        {
                            //根据T找到帧处理器
                            Command.ICommand cmd = null;
                            if (!_dic_commands.ContainsKey(frame.GetFrameType()))
                            {
                                #region 未找到帧处理器，则调用Session的OnSessionUnkownT
                                try
                                {
                                    session.OnSessionUnkownT(frame);
                                }
                                catch (System.Exception ex)
                                {
                                    session.SetOffline();
                                    Console.WriteLine("Session.OnSessionUnkownT抛出异常，" + ex.Message);
                                }
                                #endregion
                            }
                            else
                            {
                                cmd = _dic_commands[frame.GetFrameType()];
                                #region  //使用帧处理器处理帧
                                try
                                {
                                    cmd.Execute(session, frame);
                                }
                                catch (System.Exception ex) //帧处理器处理，发生异常则调用Session的OnSessionExcept
                                {
                                    try
                                    {
                                        session.OnSessionExcept(frame, ex);
                                    }
                                    catch (System.Exception ex2)
                                    {
                                        session.SetOffline();
                                        Console.WriteLine("Session.OnSessionExcept抛出异常，" + ex2.Message);
                                    }
                                }
                                #endregion
                            }
                            #region 帧处理完后，释放帧所占用的流资源
                            if (frame.IsBodyHasDataInStream())
                            {
                                try
                                {
                                    var br = frame.GetBodyStream();
                                    br.Close();
                                    br.Dispose();
                                    br = null;
                                }
                                catch (System.Exception ex)
                                {
                                    Console.WriteLine("释放帧的流帧体发生异常，" + ex.Message);
                                }
                            }
                            #endregion
                            frame = null;
                        }                        
                    }
                    //将Session归还到数组
                    lock(_dic_sessions_lock)
                    {
                        foreach (var session in session_to_deal)
                        {
                            _dic_sessions_dealing.Remove(session.GetUUID());
                            _dic_sessions_idel.Add(session.GetUUID(), session);
                        }
                        session_to_deal.Clear();
                    }
                }
                #endregion

                #region 检查是否有要主动推送的要处理
                Dictionary<string, List<dynamic>> dic_push = new Dictionary<string, List<dynamic>>();
                Dictionary<string, TSession> dic_session = new Dictionary<string, TSession>();
                lock(_dic_sessions_lock)
                {
                    if (_dic_wait_to_push.Count > 0)
                    {
                        var keys = _dic_wait_to_push.Keys.ToArray();
                        foreach(var key in keys)
                        {
                            string session_uuid = key;
                            if(_dic_sessions_idel.ContainsKey(session_uuid))
                            {
                                _dic_sessions_dealing.Add(session_uuid, _dic_sessions_idel[session_uuid]);

                                dic_push.Add(session_uuid, _dic_wait_to_push[session_uuid]);
                                dic_session.Add(session_uuid, _dic_sessions_idel[session_uuid]);

                                _dic_sessions_idel.Remove(session_uuid);

                                _dic_wait_to_push.Remove(session_uuid);
                            }
                        }
                    }
                }
                #endregion

                #region 处理主动推送
                if(dic_push.Count > 0)
                {
                    foreach(var push_session in dic_push)
                    {
                        string session_uuid = push_session.Key;
                        TSession session = dic_session[session_uuid];
                        List < dynamic> lst = push_session.Value;
                        foreach (var push in lst)
                        {
                            Frame frame = push.Frame;
                            Action<string, Frame, bool> on_push_result = push.OnPushResult;
                            on_push_result(session_uuid, frame, session.Send(frame));
                        }                        
                    }
                    //将Session归还到数组
                    lock (_dic_sessions_lock)
                    {
                        foreach (var dic in dic_session)
                        {
                            string session_uuid = dic.Key;
                            _dic_sessions_dealing.Remove(session_uuid);
                            _dic_sessions_idel.Add(session_uuid, dic.Value);
                        }
                        dic_session.Clear();
                        dic_push.Clear();
                    }
                }
                #endregion

                #region 检查并设置超时未发送或接收数据的Session
                lock(_dic_sessions_lock)
                {
                    if(_dic_sessions_idel.Count > 0)
                    {
                        foreach(var dic in _dic_sessions_idel)
                        {
                            TSession sessioin = dic.Value;
                            DateTime accept_time = sessioin.GetAcceptTime();
                            DateTime? last_communication_time = sessioin.GetLastCommunicationTime();
                            DateTime now = DateTime.Now;
                            TimeSpan ts;
                            if (last_communication_time == null)
                                ts = now - accept_time;
                            else
                                ts = now - last_communication_time.Value;
                            if (ts.TotalSeconds > _config.MaxKeepLiveSecond)
                                sessioin.SetOffline();
                        }
                    }
                }
                #endregion

                #region 检查是否有连接断开的session要处理
                List<TSession> lst_offline = new List<TSession>();
                lock(_dic_sessions_lock)
                {
                    if (_dic_sessions_idel.Count > 0)
                    {
                        string[] keys = _dic_sessions_idel.Keys.ToArray();
                        foreach (var key in keys)
                        {
                            TSession sessin = _dic_sessions_idel[key];
                            if (sessin.GetIsConnected())
                                continue;
                            _dic_sessions_idel.Remove(key);
                            lst_offline.Add(sessin);
                        }
                    }
                }
                #endregion

                #region 处理断开的Session
                if(lst_offline.Count > 0)
                {
                    foreach(var session in lst_offline)
                    {
                        session.OnSessionStop();
                    }
                    lst_offline.Clear();
                }
                #endregion

                Thread.Sleep(15);
            }
        }
    }
}