using System;
using System.Threading;
using System.Threading.Tasks;

namespace Titanosoft.AspBackgroundWorker
{
    public class RecurringBackgroundTask
    {
        public readonly Func<IServiceProvider, CancellationToken, Task> Delegate;
        public readonly string Name;
        public readonly TimeSpan Interval;

        public RecurringBackgroundTask(string name, TimeSpan interval, Func<IServiceProvider, CancellationToken, Task> task)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Delegate = task ?? throw new ArgumentNullException(nameof(task));

            if(interval.TotalMilliseconds < 250)
                throw new ArgumentOutOfRangeException(nameof(interval), "Interval is too small");

            Interval = interval;
        }

        public bool RunImmediately { get; set; }
        
    }
}