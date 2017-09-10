using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
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
            var mockLifeTime = new Mock<IApplicationLifetime>();

            var server = new TestServer(new WebHostBuilder()
                .UseStartup<Startup>()
                .UseEnvironment("IntegrationTest"));

            var scopeFactory = server.Host.Services.GetService<IServiceScopeFactory>();
            var loggerMock = new Mock<ILogger>();
            var lifeTime = mockLifeTime.Object;

            var counter = 0;
            Task Callback(IServiceProvider provider, CancellationToken token) 
                => Task.FromResult(counter++);

            var stopSource = new CancellationTokenSource();
            var startSource = new CancellationTokenSource();
            mockLifeTime.Setup(x => x.ApplicationStopping).Returns(stopSource.Token);
            mockLifeTime.Setup(x => x.ApplicationStarted).Returns(startSource.Token);

            lifeTime.UseBackgroundTask(scopeFactory, loggerMock.Object, new RecurringBackgroundTask("NewTask", TimeSpan.FromMilliseconds(250), Callback)
            {
                RunImmediately = true
            });
            startSource.Cancel();

            Thread.Sleep(1500);

            Assert.True(counter > 1);

            stopSource.Cancel();
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void TestSingleCall()
        {
            var mockLifeTime = new Mock<IApplicationLifetime>();

            var server = new TestServer(new WebHostBuilder()
                .UseStartup<Startup>()
                .UseEnvironment("UnitTest"));

            var scopeFactory = server.Host.Services.GetService<IServiceScopeFactory>();
            var loggerMock = new Mock<ILogger>();
            var lifeTime = mockLifeTime.Object;

            var counter = 0;
            Task Callback(IServiceProvider provider, CancellationToken token)
            {
                counter++;
                Thread.Sleep(1000);
                return Task.CompletedTask;
            }

            var stopSource = new CancellationTokenSource();
            var startSource = new CancellationTokenSource();
            mockLifeTime.Setup(x => x.ApplicationStopping).Returns(stopSource.Token);
            mockLifeTime.Setup(x => x.ApplicationStarted).Returns(startSource.Token);

            lifeTime.UseBackgroundTask(scopeFactory, loggerMock.Object, new RecurringBackgroundTask("NewTask", Callback, TimeSpan.FromMilliseconds(250) )
            {
                RunImmediately = true
            });
            startSource.Cancel();

            Thread.Sleep(750);

            Assert.True(counter == 1);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void TestDispose()
        {
            var mockLifeTime = new Mock<IApplicationLifetime>();

            var server = new TestServer(new WebHostBuilder()
                .UseStartup<Startup>()
                .UseEnvironment("UnitTest"));

            var scopeFactory = server.Host.Services.GetService<IServiceScopeFactory>();
            var loggerMock = new Mock<ILogger>();
            
            var lifeTime = mockLifeTime.Object;
            
            Task Callback(IServiceProvider provider, CancellationToken token)
            {
                while (!token.IsCancellationRequested)
                {
                    Thread.Sleep(100);
                }
                token.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            }

            var stopSource = new CancellationTokenSource();
            var startSource = new CancellationTokenSource();
            mockLifeTime.Setup(x => x.ApplicationStopping).Returns(stopSource.Token);
            mockLifeTime.Setup(x => x.ApplicationStarted).Returns(startSource.Token);

            lifeTime.UseBackgroundTask(scopeFactory, loggerMock.Object, new RecurringBackgroundTask("NewTask", Callback, TimeSpan.FromMilliseconds(250) )
            {
                RunImmediately = true
            });
            startSource.Cancel();
            stopSource.Cancel();

            Thread.Sleep(100);

            loggerMock.Verify(x => x.Log(It.Is<LogLevel>(l => l == LogLevel.Debug), It.IsAny<EventId>(),
                It.IsAny<object>(), It.Is<Exception>(e => e != null && e.GetType() == typeof(OperationCanceledException)), It.IsAny<Func<object, Exception, string>>()),
                Times.Once());
        }
    }
}
