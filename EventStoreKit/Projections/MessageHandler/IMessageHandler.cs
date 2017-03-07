using System;
using EventStoreKit.Messages;

namespace EventStoreKit.Projections.MessageHandler
{
    internal interface IMessageHandler
    {
        Type Type { get; }
        bool IsAlive { get; }

        void Process( Message message );
        IMessageHandler Combine( Action<Message> process, bool runBefore = false );
    }
}