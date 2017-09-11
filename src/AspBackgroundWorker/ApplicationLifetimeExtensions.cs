using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Titanosoft.AspBackgroundWorker
{
    public static class ApplicationLifetimeExtensions
    {
        private static readonly IDictionary<string, JobMonitor> MontiorLookup
            = new ConcurrentDictionary<string, JobMonitor>();

        /// <summary>
        /// Add a background task that is tied to the application lifetime
        /// </summary>
        /// <param name="builder">The application builder</param>
        /// <param name="backgroundTask">The task definition to run</param>
        public static void UseBackgroundTask(this IApplicationBuilder builder, RecurringBackgroundTask backgroundTask)
        {
            var scopeFactory = builder.ApplicationServices.GetService<IServiceScopeFactory>();
            var lifetime = builder.ApplicationServices.GetService<IApplicationLifetime>();
            var logFactory = builder.ApplicationServices.GetService<ILoggerFactory>();
            var logger = logFactory?.CreateLogger<RecurringBackgroundTask>();

            MontiorLookup.Add(backgroundTask.Name, new JobMonitor());

            async void Callback(object self)
            {
                var monitor = MontiorLookup[backgroundTask.Name];

                //Do not allow a new instance to start if the last one hasn't finished
                if (monitor.Increment() != 1)
                {
                    monitor.End();
                    return;
                }

                try
                {
                    using (logger?.BeginScope("{TaskName}", backgroundTask.Name))
                    {
                        try
                        {
                            logger?.LogDebug("Beginning execution");

                            using (var scope = scopeFactory.CreateScope())
                            {
                                await backgroundTask.Delegate(scope.ServiceProvider, lifetime.ApplicationStopping);
                            }

                            logger?.LogDebug("Completed execution");
                        }
                        catch (OperationCanceledException exception)
                        {
                            logger?.LogDebug(0, exception, "Caught OperationCanceledException");
                        }
                        catch (Exception exception)
                        {
                            logger?.LogError(0, exception, "Uncaught Exception");
                        }
                    }
                }
                finally
                {
                    monitor.End();
                }
            }
            
            lifetime.ApplicationStarted.Register(() =>
            {
                var monitor = MontiorLookup[backgroundTask.Name];
                monitor.Start(Callback, backgroundTask.Interval);

                if (backgroundTask.RunImmediately)
                {
                    Task.Run(() => Callback((TimerCallback)Callback), lifetime.ApplicationStopping);
                }

                lifetime.ApplicationStopping.Register(() =>
                {
                    if (monitor == null) return;
                    var totalSleep = 0;
                    while (monitor.IsRunning)
                    {
                        Thread.Sleep(100);
                        totalSleep += 100;
                        if (totalSleep <= 5000) continue;

                        monitor.Dispose();
                        var ex = new TaskCanceledException("Cancellation event was not respected.");
                        logger.LogCritical(0, ex, "The maximum threshold was exceeded for waiting on a background task to complete");
                        throw ex;
                    }
                    monitor.Dispose();
                });
            });
        }
    }
}

