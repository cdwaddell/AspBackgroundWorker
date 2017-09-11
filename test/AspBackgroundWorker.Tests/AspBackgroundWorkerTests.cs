using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Titanosoft.AspBackgroundWorker;
using Xunit;

namespace AspBackgroundWorker.Tests
{
    public class AspBackgroundWorkerTests
    {
        [Fact]
        [Trait("Category", "Unit")]
        public void TestMultipleCalls()
        {
            var counter = 0;

            Task Callback(IServiceProvider provider, CancellationToken token)
                => Task.FromResult(counter++);
            
            using (var server = new TestServer(new WebHostBuilder()
                .Configure(builder =>
                {
                    builder.UseBackgroundTask(new RecurringBackgroundTask(
                        "TestMultipleCalls",
                        TimeSpan.FromMilliseconds(250),
                        Callback
                    )
                    {
                        RunImmediately = true
                    });
                })
                .UseEnvironment("IntegrationTest")))
            {

                server.CreateClient();

                Thread.Sleep(1500);

                Assert.True(counter > 1);
            }
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void TestSingleCall()
        {
            var counter = 0;

            Task Callback(IServiceProvider provider, CancellationToken token)
            {
                counter++;
                Thread.Sleep(1000);
                return Task.CompletedTask;
            }

            using (var server = new TestServer(new WebHostBuilder()
                .Configure(builder =>
                {
                    builder.UseBackgroundTask(new RecurringBackgroundTask(
                        "TestSingleCall",
                        TimeSpan.FromMilliseconds(250),
                        Callback
                    )
                    {
                        RunImmediately = true
                    });
                })
                .UseEnvironment("IntegrationTest")))
            {

                server.CreateClient();

                Thread.Sleep(750);

                Assert.Equal(1, counter);
            }
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void TestDispose()
        {
            Task Callback(IServiceProvider provider, CancellationToken token)
            {
                while (!token.IsCancellationRequested)
                {
                    Thread.Sleep(100);
                }
                token.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            }
            var loggerMock = new Mock<ILogger>();
            using (var server = new TestServer(new WebHostBuilder()
                .ConfigureServices(collection =>
                {
                    var loggerService = new Mock<ILoggerFactory>();
                    loggerService.Setup(c => c.CreateLogger(It.IsAny<string>()))
                        .Returns(loggerMock.Object);

                    collection.AddSingleton(loggerService.Object);
                })
                .Configure(builder =>
                {
                    builder.UseBackgroundTask(new RecurringBackgroundTask(
                        "TestDispose",
                        TimeSpan.FromMilliseconds(250),
                        Callback
                    )
                    {
                        RunImmediately = true
                    });
                })
                .UseEnvironment("IntegrationTest")))
            {

                server.CreateClient();
            }
            loggerMock.Verify(x => x.Log(It.Is<LogLevel>(l => l == LogLevel.Debug), It.IsAny<EventId>(),
                    It.IsAny<object>(), It.Is<Exception>(e => e != null && e.GetType() == typeof(OperationCanceledException)), It.IsAny<Func<object, Exception, string>>()),
                Times.Once());
        }
    }
}
