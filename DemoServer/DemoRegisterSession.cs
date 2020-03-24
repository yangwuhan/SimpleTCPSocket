using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DemoServer
{
    class DemoRegisterSession
    {
        public static void RegisterSession(string session_uuid)
        {
            lock(__sessions_lock)
            {
                string foud = __sessions.Find((x) => { return x == session_uuid; });
                if (foud != session_uuid)
                    __sessions.Add(session_uuid);
            }
        }

        public static void UnRegisterSession(string session_uuid)
        {
            lock (__sessions_lock)
            {
                string foud = __sessions.Find((x) => { return x == session_uuid; });
                if (foud == session_uuid)
                    __sessions.Remove(session_uuid);
            }
        }

        public static List<string> GetAll()
        {
            List<string> ret = new List<string>();
            lock (__sessions_lock)
            {
                foreach (string item in __sessions)
                    ret.Add(item);
            }
            return ret;
        }

        private static List<string> __sessions = new List<string>();
        private static object __sessions_lock = new object();
    }
}
