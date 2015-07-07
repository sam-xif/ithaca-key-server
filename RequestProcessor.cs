using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Threading;

namespace IthacaKeyServer.RequestProcessing
{
    // Represents a request with a worker thread operating on it.
    // The worker thread is represented by the ThreadInfo property.

    // TODO: Rename this interface maybe to something more descriptive of its function
    public interface IRequestObject : IDisposable
    {
        ProcessorThreadInfo ThreadInfo { get; set; }
    }


    public class ProcessorThreadInfo
    {
        public Thread Thread;
        public uint ThreadID;

        // This is signaled when the thread is finished processing
        public ManualResetEvent Handle;

        public ProcessorThreadInfo() { }

        public ProcessorThreadInfo(Thread thread, uint threadID, ManualResetEvent handle)
        {
            this.Thread = thread;
            this.ThreadID = threadID;
            this.Handle = handle;
        }
    }

    public delegate void RequestCallback(object state);

    public class RequestProcessingManager : IDisposable
    {

        // Member variables:
        private ThreadSafeQueue<IRequestObject> m_requestQueue;
        private int m_capacity = 128;
        private Semaphore m_pool;
        private bool m_stopFlag = false;
        private uint m_idCounter = 0;
        private bool m_disposed = false;

        // When set to false, the thread stops.
        // When set to true by Main(), work continues because there are queued requests.
        // The purpose is to save CPU when there are no requests waiting.
        private ManualResetEvent m_continueWork;

        // May not be needed
        private int m_numWorkerThreads;

        private Thread m_dequeueThread;

        private RequestCallback m_callback;


        public RequestProcessingManager(RequestCallback callback)
        {
            this.m_callback = callback;

            m_requestQueue = new ThreadSafeQueue<IRequestObject>();
            m_pool = new Semaphore(0, m_capacity);
            m_continueWork = new ManualResetEvent(false);

            StartDequeueThread();
        }

        // Capacity: Number of requests to queue before denying additional ones.
        public RequestProcessingManager(RequestCallback callback, int capacity)
        {
            this.m_capacity = capacity;
            this.m_callback = callback;

            m_requestQueue = new ThreadSafeQueue<IRequestObject>();
            m_pool = new Semaphore(0, m_capacity);
            m_continueWork = new ManualResetEvent(false);

            StartDequeueThread();
        }

        private void StartDequeueThread()
        {
            m_dequeueThread = new Thread(ProcessQueue);
            m_dequeueThread.Start();
        }

        private void ProcessQueue()
        {
            while (!m_stopFlag)
            {
                lock (m_continueWork)
                {
                    if (m_numWorkerThreads == 0)
                    {
                        m_continueWork.Reset();
                    }

                    m_continueWork.WaitOne();
                }

                if (m_numWorkerThreads > 0)
                {
                    // Dequeues a requests, waits for it to complete, and moves on to the next one.
                    IRequestObject request = m_requestQueue.Dequeue();
                    request.ThreadInfo.Handle.WaitOne();
                    Interlocked.Decrement(ref m_numWorkerThreads);
                }
            }

            // If stop flag is set, wait on all handles:
            IRequestObject[] requests = m_requestQueue.ToArray();
            ManualResetEvent[] handles = new ManualResetEvent[requests.Length];
            for (int i = 0; i < handles.Length; i++)
            {
                handles[i] = requests[i].ThreadInfo.Handle;
            }

            WaitHandle.WaitAll(handles);
        }


        // Queues a new work item, passing a state object to the callback method
        // Returns false if the request queue is filled, meaning that the request was and will not be processed at the moment
        public bool QueueWorkItem(IRequestObject requestObject)
        {
            if (m_numWorkerThreads < m_capacity)
            {
                if (!m_stopFlag)
                {
                    Thread t = new Thread(WorkerThread);
                    
                    requestObject.ThreadInfo = new ProcessorThreadInfo();

                    requestObject.ThreadInfo.Thread = t;
                    requestObject.ThreadInfo.ThreadID = m_idCounter;

                    m_idCounter++;

                    requestObject.ThreadInfo.Handle = new ManualResetEvent(false);

                    // Passes a two-tuple of the object state and the thread info.
                    // The callback method must set the waithandle once execution has completed.
                    t.Start(requestObject);

                    m_requestQueue.Enqueue(requestObject);

                    // Give the thread some time to get started.
                    Thread.Sleep(200);
                    m_pool.Release();

                    if (m_numWorkerThreads == 0)
                        m_continueWork.Set();

                    Interlocked.Increment(ref m_numWorkerThreads);
                    return true;
                }
                return false;
            }
            return false;
        }

        // Calls the user callback method.
        private void WorkerThread(object state)
        {
            IRequestObject request = (IRequestObject)state;

            m_pool.WaitOne();

            // Call callback from worker thread
            m_callback(state);

            m_pool.Release();
            request.ThreadInfo.Handle.Set();
        }

        // Stops more requests from being queued and blocks until all the current requests are responded to.
        public void Stop()
        {
            m_stopFlag = true;
            Dispose();
        }

        // Aborts all working threads immediately.
        public void Abort()
        {
            // Abort
            Dispose();
        }

        public void Dispose()
        {
            if (!m_disposed)
            {
                
                // Dispose code here

                IRequestObject[] requests = m_requestQueue.ToArray();
                for (int i = 0; i < requests.Length; i++)
                {
                    requests[i].Dispose();
                }

                m_requestQueue.Clear();
                m_pool.Close();

                m_disposed = true;
            }
        }
    }

}
