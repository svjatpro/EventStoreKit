using System;
using EventStoreKit.Messages;

namespace EventStoreKit.Services
{
    public interface IEventDispatcher
    {
        void RegisterHandler<T>(Action<T> handler) where T : Message;
    }
}
