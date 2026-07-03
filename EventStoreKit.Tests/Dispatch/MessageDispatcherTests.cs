using EventStoreKit;
using EventStoreKit.NEventStore;
using FluentAssertions;
using NUnit.Framework;

namespace EventStoreKit.Tests.Dispatch;

[TestFixture]
public class MessageDispatcherTests
{
    #region Private members

    private MessageDispatcher Dispatcher = null!;

    private record Cmd(int X);

    private static readonly Guid KnownId = Guid.NewGuid();

    [SetUp]
    public void Setup() => Dispatcher = new MessageDispatcher();

    #endregion

    [Test]
    public void SendRoutesToTheRegisteredHandler()
    {
        var seen = 0;
        Dispatcher.RegisterExclusiveHandler<Cmd>(c => seen = c.X);

        Dispatcher.Send(new Cmd(7));

        seen.Should().Be(7);
    }

    [Test]
    public void SendWithoutAHandlerThrows()
    {
        var act = () => Dispatcher.Send(new Cmd(1));
        act.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void RegisteringTwoExclusiveHandlersForOneCommandThrows()
    {
        Dispatcher.RegisterExclusiveHandler<Cmd>(_ => { });
        var act = () => Dispatcher.RegisterExclusiveHandler<Cmd>(_ => { });
        act.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void TrySendReturnsFalseWhenNoHandler()
    {
        Dispatcher.TrySend(new Cmd(1)).Should().BeFalse();
    }

    [Test]
    public void TrySendRunsHandlerWhenRegistered()
    {
        var seen = 0;
        Dispatcher.RegisterExclusiveHandler<Cmd>(c => seen = c.X);

        Dispatcher.TrySend(new Cmd(9)).Should().BeTrue();
        seen.Should().Be(9);
    }

    [Test]
    public void TrySendRespectsARejectingFilter()
    {
        var ran = false;
        Dispatcher.RegisterExclusiveHandler<Cmd>(_ => ran = true);
        Dispatcher.RegisterFilter(typeof(Cmd), _ => false);

        Dispatcher.TrySend(new Cmd(1)).Should().BeFalse();
        ran.Should().BeFalse();
    }

    [Test]
    public void TrySendRunsHandlerWhenFilterAccepts()
    {
        var ran = false;
        Dispatcher.RegisterExclusiveHandler<Cmd>(_ => ran = true);
        Dispatcher.RegisterFilter(typeof(Cmd), _ => true);

        Dispatcher.TrySend(new Cmd(1)).Should().BeTrue();
        ran.Should().BeTrue();
    }

    [Test]
    public void TrySendOutReturnsTheHandlersOutputContext()
    {
        Dispatcher.RegisterOutputHandler(
            typeof(IntegrationCommand<Cmd, Guid>),
            cmd => ((IntegrationCommand<Cmd, Guid>)cmd).Command.X == 42 ? (object)KnownId : Guid.Empty);

        Dispatcher.TrySend<Cmd, Guid>(new Cmd(42), out var id).Should().BeTrue();
        id.Should().Be(KnownId);
    }

    [Test]
    public void DispatchInvokesAllRegisteredEventHandlers()
    {
        var count = 0;
        Dispatcher.RegisterHandler<Cmd>(_ => count++);
        Dispatcher.RegisterHandler<Cmd>(_ => count++);

        Dispatcher.Dispatch(new Cmd(1));

        count.Should().Be(2);
    }
}
