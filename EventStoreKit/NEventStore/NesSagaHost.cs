using System.Reflection;
using NEventStore.Domain.Core;

namespace EventStoreKit.NEventStore;

// Hosts a base-class-free POCO saga inside NEventStore's SagaBase. Two method conventions on the POCO:
//   void Apply(TEvent)                - evolve: fold the event into state (this is what replay runs)
//   IEnumerable<object> React(TEvent) - decide: pure policy over the BEFORE state, returns commands
// Apply is wired into Transition, so replay and seeding fold without side effects. React is invoked
// explicitly (live only) by the saga pump on the rehydrated before-state. CommonDomain stays hidden.
// Construction: if the POCO opts into a (string sagaId) constructor it receives its stream id at
// birth; otherwise it is default-constructed. Both are optional conventions, never a base type.
internal sealed class NesSagaHost<TSaga> : SagaBase<object>
    where TSaga : class
{
    private static readonly Dictionary<Type, MethodInfo> ApplyMethods = Discover( "Apply" );
    private static readonly Dictionary<Type, MethodInfo> ReactMethods = Discover( "React" );

    private static readonly MethodInfo RegisterFoldMethod = typeof(NesSagaHost<TSaga>)
        .GetMethod( nameof(RegisterFold), BindingFlags.Instance | BindingFlags.NonPublic )!;

    public TSaga Inner { get; }

    public NesSagaHost( string id )
    {
        Id = id;
        Inner = CreateInner( id );
        foreach ( var eventType in ApplyMethods.Keys.Union( ReactMethods.Keys ) )
        {
            ApplyMethods.TryGetValue( eventType, out var fold );
            RegisterFoldMethod.MakeGenericMethod( eventType ).Invoke( this, [fold] );
        }
    }

    // Pass the stream id to the POCO if it exposes a (string sagaId) ctor; else default-construct.
    private static TSaga CreateInner( string id )
    {
        var withId = typeof(TSaga).GetConstructor( [typeof(string)] );
        return (TSaga)( withId is null
            ? Activator.CreateInstance( typeof(TSaga) )!
            : withId.Invoke( [id] ) );
    }

    // Live decision: run the POCO's React for this event (on the current/before state) and return its
    // commands. No state change — folding happens separately via Transition. Empty if no React handler.
    public IReadOnlyList<object> React( object @event )
    {
        if ( ReactMethods.TryGetValue( @event.GetType(), out var method )
            && method.Invoke( Inner, [@event] ) is IEnumerable<object> commands )
        {
            return commands.ToList();
        }
        return [];
    }

    private void RegisterFold<TEvent>( MethodInfo? fold )
        where TEvent : class
    {
        // Transition records every event into the saga stream; Apply (when present) folds it into state.
        Register<TEvent>( @event => fold?.Invoke( Inner, [@event] ) );
    }

    private static Dictionary<Type, MethodInfo> Discover( string methodName )
    {
        return typeof(TSaga)
            .GetMethods( BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic )
            .Where( method => method.Name == methodName && method.GetParameters().Length == 1 )
            .ToDictionary( method => method.GetParameters()[0].ParameterType );
    }
}
