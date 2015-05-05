using System;
using CommonDomain;
using CommonDomain.Persistence;

namespace EventStoreKit.Services
{
    public class EntityFactory : IConstructAggregates
    {
        #region Implementation of IConstructAggregates

        public IAggregate Build( Type type, Guid id, IMemento snapshot )
        {
            var entity = Activator.CreateInstance( type, id ) as IAggregate;
            return entity;
        }

        #endregion
    }
}