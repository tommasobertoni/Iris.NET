using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace Iris.NET.Collections
{
    /// <summary>
    /// This queue uses a Semaphore to block the dequeue method when the queue is empty,
    /// until a new item is added to the queue.
    /// (inspired by Stephen Toub at https://blogs.msdn.microsoft.com/toub/2006/04/12/blocking-queues/)
    /// </summary>
    public class BlockingQueue<T> : IDisposable
    {
        private Queue<T> _queue = new Queue<T>();
        private Semaphore _semaphore = new Semaphore(0, int.MaxValue);

        /// <summary>
        /// Adds the item to the queue.
        /// </summary>
        /// <param name="item">A new item.</param>
        public void Enqueue(T item)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            lock (_queue)
                _queue.Enqueue(item);

            _semaphore.Release();
        }

        /// <summary>
        /// If the queue is empty, waits for a new item to be added to the queue, else returns the dequeued item.
        /// </summary>
        /// <returns>The item at the beginning of the queue.</returns>
        public T Dequeue()
        {
            _semaphore.WaitOne();

            lock (_queue)
            {
                if (_queue.Any())
                    return _queue.Dequeue();
                else // The queue has been disposed and the queue is empty
                    return default(T);
            }
        }

        /// <summary>
        /// Clear the semaphore and the queue.
        /// </summary>
        public void Dispose()
        {
            _queue.Clear();
            _semaphore.Release(int.MaxValue); // Release all the blocked threads
            _semaphore.Dispose();
        }
    }
}
