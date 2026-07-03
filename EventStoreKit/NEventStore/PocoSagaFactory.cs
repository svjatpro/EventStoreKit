using NEventStore.Domain;
using NEventStore.Domain.Persistence;

namespace EventStoreKit;

// Default saga factory: builds the requested NEventStore saga type (the internal host wrapping a
// POCO saga) via its (string id) constructor. Clients register POCO sagas; the host type is supplied
// by the saga helpers, never named in client code.
public sealed class PocoSagaFactory : IConstructSagas
{
    public ISaga Build( Type type, string id )
    {
        return (ISaga)Activator.CreateInstance( type, id )!;
    }
}
