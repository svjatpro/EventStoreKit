using System;
using CommonDomain;
using CommonDomain.Persistence;

namespace EventStoreKit.Services
{
    public class SagaFactory : IConstructSagas
    {
        #region Implementation of IConstructSagas

        public ISaga Build( Type type, string id )
        {
            var entity = Activator.CreateInstance( type, id ) as ISaga;
            return entity;
        }

        #endregion

        //todo: public void RegisterSagaInitializer( Type sagaType, () => ... )
    }
}