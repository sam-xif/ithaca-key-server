using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using System.Threading;

using System.Net;

using System.Xml;

using IthacaKeyServer.RequestProcessing;

namespace IthacaKeyServer
{

    // Don't really need this structure

    /*
    public struct Key
    {
        public string KeyData;
        public string KeyDesc;

        public Key(string data, string desc)
        {
            this.KeyData = data;
            this.KeyDesc = desc;
        }

        public override int GetHashCode()
        {
            return KeyData.GetHashCode() ^ KeyDesc.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return KeyData.Equals(((Key)obj).KeyData) && KeyDesc.Equals(((Key)obj).KeyDesc);
        }
    }
    */

    // An object that holds request data
    public class KeyAuthRequest : IRequestObject
    {
        private HttpListenerContext m_context;
        private ProcessorThreadInfo m_threadInfo;
        private bool m_disposed;

        public ProcessorThreadInfo ThreadInfo
        {
            get { return m_threadInfo; }
            set { m_threadInfo = value; }
        }

        public HttpListenerContext Context
        {
            get { return m_context; }
        }


        public KeyAuthRequest(HttpListenerContext context)
        {
            this.m_context = context;
        }

        public void Close()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (!m_disposed)
            {
                m_threadInfo.Handle.Close();
                m_disposed = true;
            }
        }
    }
}
