using System;
using System.Collections.Generic;
using CommonDomain;
using CommonDomain.Persistence;

namespace EventStoreKit.Services
{
    public interface ISagaFactory
    {
        SagaFactory RegisterSagaConstructor<TSaga>( Func<string,TSaga> sagaBuilder ) where TSaga : ISaga;
    }

    public class SagaFactory : IConstructSagas, ISagaFactory
    {
        private readonly Dictionary<Type, Func<string, ISaga>> SagaBuilders = new Dictionary<Type, Func<string, ISaga>>();

        #region Implementation of IConstructSagas

        public ISaga Build( Type type, string id )
        {
            var entity = 
                SagaBuilders.ContainsKey( type ) ?
                SagaBuilders[type]( id ) :
                Activator.CreateInstance( type, id ) as ISaga;
            return entity;
        }

        #endregion

        public SagaFactory RegisterSagaConstructor<TSaga>( Func<string,TSaga> sagaBuilder ) where TSaga : ISaga
        {
            SagaBuilders[typeof (TSaga)] = sagaId => sagaBuilder( sagaId );
            return this;
        }
    }
}