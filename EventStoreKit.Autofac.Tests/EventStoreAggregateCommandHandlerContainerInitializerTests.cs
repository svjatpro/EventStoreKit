using System;
using Autofac;
using CommonDomain.Core;
using EventStoreKit.Handler;
using EventStoreKit.Messages;
using FluentAssertions;
using NUnit.Framework;

namespace EventStoreKit.Tests
{
    [TestFixture]
    public class EventStoreAggregateCommandHandlerContainerInitializerTests : BasicContainerInitializerTests
    {
        #region private members

        private static DomainCommand LastSentCommand;

        private class Command1 : DomainCommand { }
        private class Command2 : DomainCommand { }

        // ReSharper disable ClassNeverInstantiated.Local
        private class Aggregate1 : AggregateBase,
            ICommandHandler<Command1>,
            ICommandHandler<Command2>
        {
            public Aggregate1( Guid id ) {  }
            public void Handle( Command1 cmd ) { LastSentCommand = cmd; }
            public void Handle( Command2 cmd ) { LastSentCommand = cmd; }
        }
        // ReSharper restore ClassNeverInstantiated.Local

        private void ValidateCommandHandler( DomainCommand command )
        {
            LastSentCommand = null;
            Service.SendCommand( command );
            LastSentCommand.Should().Be( command );
        }

        #endregion
        
        [Test]
        public void HandlerSetByContainerShouldBeAvailableThroughTheService()
        {
            Builder.RegisterType<Aggregate1>();
            InitializeContainer();

            ValidateCommandHandler( new Command1() );
            ValidateCommandHandler( new Command2() );
        }

        [Test]
        public void HandlerMethodSetByContainerShouldBeAvailableThroughTheService()
        {
            Builder.RegisterType<Aggregate1>().As<ICommandHandler<Command1>>();
            InitializeContainer();

            ValidateCommandHandler( new Command1() );
            ValidateCommandHandler( new Command2() );
        }

        [Test]
        public void HandlerMethodsSetByContainerShouldBeAvailableThroughTheService()
        {
            Builder.RegisterType<Aggregate1>().AsImplementedInterfaces();
            InitializeContainer();

            ValidateCommandHandler( new Command1() );
            ValidateCommandHandler( new Command2() );
        }

        [Test]
        public void HandlerInstanceSetByContainerShouldBeAvailableThroughTheService()
        {
            Builder.RegisterType<Aggregate1>().SingleInstance(); // instance should be ignored, aggregate will be created in regular way
            InitializeContainer();

            ValidateCommandHandler( new Command1() );
            ValidateCommandHandler( new Command1() );
        }
    }
}
