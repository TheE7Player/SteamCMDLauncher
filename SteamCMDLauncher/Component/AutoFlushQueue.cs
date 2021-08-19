using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Timers;

namespace SteamCMDLauncher.Component
{
    /// <summary>
    /// A Queue which auto-flushes (handles) queued items based on queued/last added time
    /// </summary>
    public class AutoFlushQueue<T>
    {
        private T[] _queue;

        private int pointer;

        private int elements_assigned;

        private Timer _timer;

        /// <summary>
        /// The action that gets called when the flush is initiated
        /// </summary>
        public Action<T[]> OnFlushElapsed;

        /// <summary>
        /// Initializes the AFQ
        /// </summary>
        /// <param name="max_size">The max size to store, if hit max it auto-flushes</param>
        /// <param name="max_wait_time">The max time to wait for another entity before flushing</param>
        public AutoFlushQueue(int max_size, int max_wait_time = 4)
        {
            _queue = new T[max_size];
            pointer = 0; elements_assigned = 0;

            _timer = new Timer(max_wait_time);
            _timer.AutoReset = true;
            _timer.Elapsed += (_, e) =>
            {
                _timer.Stop();
                if (OnFlushElapsed is null) throw new Exception("AutoFlushQueue was not assigned a function to handle entities - Please do so.");
                DoFlush();
            };
        }

        public void Add(T element)
        {
            if (elements_assigned == _queue.Length)
                DoFlush();

            _timer.Start();

            if (pointer > _queue.Length)
                pointer = 0;

            _queue[pointer] = element;
            elements_assigned++;
            pointer++;
        }

        private void DoFlush()
        {
            T[] shallow_copy = new T[elements_assigned];

            Array.Copy(_queue, shallow_copy, elements_assigned);

            OnFlushElapsed(shallow_copy);

            Array.Clear(_queue, 0, _queue.Length);

            elements_assigned = 0;
        }
    }
}
