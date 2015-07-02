using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Threading;

namespace IthacaKeyServer
{
    public class ThreadSafeQueue<T>
    {

        Queue<T> m_queue;

        public int Count
        {
            get
            {
                lock (m_queue)
                {
                    return m_queue.Count;
                }
            }
        }

        public ThreadSafeQueue()
        {
            m_queue = new Queue<T>();
        }

        // This method blocks the thread until the object can be added safely.
        public void Enqueue(T obj)
        {
            lock (m_queue)
            {
                m_queue.Enqueue(obj);
            }
        }

        // Here, instead of locking, we check if the lock is immediately available.
        // Returns a bool indicating whether or not the operation succeeded
        public bool TryEnqueue(T obj)
        {
            if (Monitor.TryEnter(m_queue))
            {
                try
                {
                    m_queue.Enqueue(obj);
                }
                finally
                {
                    Monitor.Exit(obj);
                }
                return true;
            }
            else
            {
                return false;
            }
        }

        // Specify a waitTime in milliseconds
        public bool TryEnqueue(T obj, int waitTime)
        {
            if (Monitor.TryEnter(m_queue, waitTime))
            {
                try
                {
                    m_queue.Enqueue(obj);
                }
                finally
                {
                    Monitor.Exit(obj);
                }
                return true;
            }
            else
            {
                return false;
            }
        }

        public T Dequeue()
        {
            T retval;

            lock (m_queue)
            {
                retval = m_queue.Dequeue();
            }

            return retval;
        }


        public T[] ToArray()
        {
            lock (m_queue)
            {
                return m_queue.ToArray();
            }
        }

        public void Clear()
        {
            lock (m_queue)
            {
                m_queue.Clear();
            }
        }

    }
}
