using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Threading;

using System.Net;

using System.Xml;

using IthacaKeyServer.RequestProcessing;

namespace IthacaKeyServer
{
    public struct Key
    {
        public string KeyData;
    }


    public class KeyAuthRequest : IRequestObject
    {

        public Key[] Keys
        {
            get
            {
                return m_keys;
            }
        }

        private Key[] m_keys;
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
            //ParseRequest();
        }

        private void ParseRequest()
        {
            if (!m_context.Request.HasEntityBody)
                throw new Exception("Error: Request has no body.", new Exception("XML Body required for GET method in this server."));
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
