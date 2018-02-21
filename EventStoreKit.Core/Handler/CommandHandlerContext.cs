using System;
using CommonDomain;

namespace EventStoreKit.Handler
{
    public class CommandHandlerContext<TEntity>
        where TEntity : IAggregate
    {
        private TEntity EntityInstance;
        private readonly Func<TEntity> EntityInitializer;

        public CommandHandlerContext( Func<TEntity> entityInitializer )
        {
            EntityInitializer = entityInitializer;
        }

        public TEntity Entity
        {
            get
            {
                if ( EntityInstance == null )
                    EntityInstance = EntityInitializer();
                return EntityInstance;
            }
            set => EntityInstance = value;
        }
    }
}