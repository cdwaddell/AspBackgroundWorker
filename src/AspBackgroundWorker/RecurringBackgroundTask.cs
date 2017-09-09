using System;
using System.Threading;
using System.Threading.Tasks;

namespace Titanosoft.AspBackgroundWorker
{
    public struct RecurringBackgroundTask
    {
        public string Name { get; set; }

        public bool RunImmediately { get; set; }

        public TimeSpan Interval { get; set; }

        public Func<IServiceProvider, CancellationToken, Task> Delegate { get; set; }
    }
}