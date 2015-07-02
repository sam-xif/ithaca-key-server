using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Threading;

using System.Net;
using System.Net.Security;

using System.IO;

namespace IthacaKeyServer
{
    public class KeyAuthServer
    {

        private HttpListener m_listener;
        private Semaphore m_semaphore;

        private int m_maxRequests;
        private int m_port = 80; // Default HTTP port

        private Thread m_workThread;
        private bool m_threadStopFlag;

        private int m_numRequests;

        public KeyAuthServer()
            : this(128)
        {
        }

        public KeyAuthServer(int maxRequests)
            : this(maxRequests, 80)
        {
        }

        public KeyAuthServer(int maxRequests, int port)
        {
            this.m_maxRequests = maxRequests;
            this.m_port = port;

            m_semaphore = new Semaphore(0, m_maxRequests);
            InitHttpListener();
        }

        private void InitHttpListener()
        {
            m_listener = new HttpListener();
            m_listener.Prefixes.Add("http://+:" + m_port.ToString() + "/");
        }

        public void Start()
        {
            m_threadStopFlag = false;
            Thread t = new Thread(ListenThread);
            t.Start();
            this.m_workThread = t;
        }

        // Sends the stop signal to the thread
        // This call blocks until the listener is stopped
        public void Stop()
        {
            // This flag tells the worker thread to terminate after the next current request is processed
            m_threadStopFlag = true;
        }

        private void ListenThread()
        {

            while (m_threadStopFlag)
            {

                HttpListenerContext context = m_listener.GetContext();
                Thread t = new Thread(ProcessRequest);
                t.Start(context);
                Thread.Sleep(100);
                m_semaphore.Release();
                Interlocked.Increment(ref m_numRequests);
            }
        }

        private void ProcessRequest(object obj)
        {
            m_semaphore.WaitOne();

            HttpListenerContext context = obj as HttpListenerContext;
            KeyAuthRequest request = new KeyAuthRequest(context);

            m_semaphore.Release();
        }

    }
}
