using System;
using System.Timers;
using System.Linq;

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
        /// <param name="max_wait_time">The max time to wait for another entity before flushing (1s = 1000ms)</param>
        public AutoFlushQueue(int max_size, int max_wait_time = 4000)
        {
            Config.Log($"[AFQ] AutoFlushQueue has been generated with max size of: {max_size} and flush time of: {max_wait_time}ms");

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

            if (pointer >= _queue.Length)
                pointer = 0;

            _queue[pointer] = element;
            elements_assigned++;
            pointer++;

            Config.Log("[AFQ] Element has been added");
        }

        private void DoFlush()
        {
            // Assign only elements that are not null
            T[] shallow_copy = _queue.Where(x => x != null).ToArray();

            Config.Log("[AFQ] DoFlush was called due to time schedule");

            OnFlushElapsed(shallow_copy);

            Array.Clear(_queue, 0, _queue.Length);

            elements_assigned = 0;
        }
    }
}
