using System;
using System.Threading;

namespace Titanosoft.AspBackgroundWorker
{
    public class JobMonitor : IDisposable
    {
        public JobMonitor()
        {
            _entered = 0;
        }

        public Timer Timer { get; private set; }
        private int _entered;

        public int Increment()
        {
            return Interlocked.Increment(ref _entered);
        }

        public void End()
        {
            Interlocked.Decrement(ref _entered);
        }

        private static TimeSpan TimeUntilNext(TimeSpan interval)
        {
            return new DateTime((DateTime.Now.Ticks + interval.Ticks - 1) / interval.Ticks * interval.Ticks) - DateTime.Now;
        }

        public void Start(TimerCallback callback, TimeSpan backgroundTaskInterval)
        {
            Timer = new Timer(callback, callback, TimeUntilNext(backgroundTaskInterval), backgroundTaskInterval);
        }

        public void Dispose()
        {
            Timer?.Dispose();
            Timer = null;
        }
    }
}