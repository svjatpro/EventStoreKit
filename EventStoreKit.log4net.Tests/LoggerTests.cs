using FluentAssertions;
using log4net.Appender;
using NUnit.Framework;

namespace EventStoreKit.Logging.Tests
{
    [TestFixture]
    [Parallelizable(ParallelScope.Fixtures)]
    public class LoggerTests
    {
        private MemoryAppender Appender;
        private ILoggerFactory Factory;

        [SetUp]
        protected void Setup()
        {
            Appender = new MemoryAppender();
            log4net.Config.BasicConfigurator.Configure( Appender );

            Factory = new LoggerFactory();
        }

        [Test]
        public void LoggerFactoryShouldCreateLog4NetLoggerInstance()
        {
            const string message = "message1";

            var log = Factory.Create();
            log.Error( message );

            Appender.GetEvents()[0].LoggerName.Should().Be( nameof( EventStoreKit ) );
            Appender.GetEvents()[0].RenderedMessage.Should().Be( message );
        }

        [Test]
        public void LoggerFactoryShouldCreateLog4NetLoggerGenericInstance()
        {
            const string message = "message1";

            var log = Factory.Create<LoggerTests>();
            log.Error( message );

            Appender.GetEvents()[0].LoggerName.Should().Be( typeof( LoggerTests ).FullName );
            Appender.GetEvents()[0].RenderedMessage.Should().Be( message );
        }

        [Test]
        public void LoggerFactoryShouldCreateLog4NetLoggerTypedInstance()
        {
            const string message = "message1";

            var log = Factory.Create( typeof( LoggerTests ) );
            log.Error( message );

            Appender.GetEvents()[0].LoggerName.Should().Be( typeof( LoggerTests ).FullName );
            Appender.GetEvents()[0].RenderedMessage.Should().Be( message );
        }

        [Test]
        public void LoggerShouldUserGlobalLog4netConfiguration()
        {
            const string message = "message1";

            var log = Factory.Create<LoggerTests>();
            log.Error( message );
            log = Factory.Create<LoggerTests>(); // second instance should write to the same appenders
            log.Error( message );

            Appender.GetEvents()[1].LoggerName.Should().Be( typeof( LoggerTests ).FullName );
            Appender.GetEvents()[1].RenderedMessage.Should().Be( message );
        }
    }
}
