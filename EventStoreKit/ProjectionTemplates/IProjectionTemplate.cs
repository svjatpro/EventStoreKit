using System;
using System.Collections.Generic;
using EventStoreKit.Messages;

namespace EventStoreKit.ProjectionTemplates
{
    public interface IProjectionTemplate
    {
        void PreprocessEvent( Message @event );
        void Flush();
        IList<Type> GetReadModels(); 
    }
}