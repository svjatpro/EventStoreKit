﻿
using EventStoreKit.Messages;

namespace EventStoreKit.Handler
{
    public interface IEventHandler<TEvent>
        where TEvent : Message
    {
        void Handle( TEvent message );
    }
}