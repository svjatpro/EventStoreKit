using EventStoreKit.NEventStore;
using NEventStore.Domain;
using NEventStore.Domain.Persistence;

namespace EventStoreKit;

// Saga load/transition/save helpers for POCO sagas (hosted in NesSagaHost). They collapse the
// load -> transition -> dispatch -> save boilerplate and keep the host type hidden from callers.
public static class EventSagaProcessor
{
    // Live path: load the saga (replay folds it to the before-state), run its React policy on that
    // before-state, dispatch the commands it returns, then fold the new event in and save. React is
    // live-only — it never runs during the replay inside GetById, so old events never re-react.
    public static void ProcessAndDispatch<TSaga>(
        this ISagaRepository repository,
        string sagaId,
        object @event,
        ICommandSender commands )
        where TSaga : class
    {
        var host = repository.GetById<NesSagaHost<TSaga>>( sagaId );
        foreach ( var command in host.React( @event ) )
        {
            commands.Send( command );
        }
        host.Transition( @event );
        repository.SaveSaga( host );
    }

    // Seeding path (e.g. migration): fold events into the saga's stream without reacting. React is
    // never called, so the policy can't fire during a seed — only state is rebuilt.
    public static void Seed<TSaga>( this ISagaRepository repository, string sagaId, IEnumerable<object> events )
        where TSaga : class
    {
        var host = repository.GetById<NesSagaHost<TSaga>>( sagaId );
        foreach ( var @event in events )
        {
            host.Transition( @event );
        }
        repository.SaveSaga( host );
    }

    // Persist a saga's uncommitted events (hides the Save(saga, Guid.NewGuid(), _ => {}) noise).
    public static void SaveSaga( this ISagaRepository repository, ISaga saga )
    {
        repository.Save( saga, Guid.NewGuid(), _ => { } );
    }
}
