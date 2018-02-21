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
    public class EventStoreCommandHandlerContainerInitializerTests : BasicContainerInitializerTests
    {
        #region private members

        private static DomainCommand LastSentCommand;

        private class Command1 : DomainCommand { }
        private class Command2 : DomainCommand { }

        // ReSharper disable ClassNeverInstantiated.Local
        private class Aggregate1 : AggregateBase { public Aggregate1( Guid id ){} }
        private class CommandHandler1 :
            ICommandHandler<Command1, Aggregate1>,
            ICommandHandler<Command2, Aggregate1>
        {
            public void Handle( Command1 cmd, CommandHandlerContext<Aggregate1> context ) { LastSentCommand = cmd; }
            public void Handle( Command2 cmd, CommandHandlerContext<Aggregate1> context ) { LastSentCommand = cmd; }
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
            Builder.RegisterType<CommandHandler1>();
            InitializeContainer();

            ValidateCommandHandler( new Command1() );
            ValidateCommandHandler( new Command2() );
        }

        [Test]
        public void HandlerMethodSetByContainerShouldBeAvailableThroughTheService()
        {
            Builder.RegisterType<CommandHandler1>().As<ICommandHandler<Command1,Aggregate1>>();
            InitializeContainer();

            ValidateCommandHandler( new Command1() );
            ValidateCommandHandler( new Command2() );
        }

        [Test]
        public void HandlerMethodsSetByContainerShouldBeAvailableThroughTheService()
        {
            Builder.RegisterType<CommandHandler1>().AsImplementedInterfaces();
            InitializeContainer();

            ValidateCommandHandler( new Command1() );
            ValidateCommandHandler( new Command2() );
        }


        [Test]
        public void HandlerInstanceSetByContainerShouldBeAvailableThroughTheService()
        {
            Builder.RegisterType<CommandHandler1>().SingleInstance();
            InitializeContainer();

            ValidateCommandHandler( new Command1() );
            ValidateCommandHandler( new Command1() );
        }
    }
}
