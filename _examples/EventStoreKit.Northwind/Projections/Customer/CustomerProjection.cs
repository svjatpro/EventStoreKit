using System;
using System.Reactive.Concurrency;
using EventStoreKit.DbProviders;
using EventStoreKit.Logging;
using EventStoreKit.Projections;
using EventStoreKit.ProjectionTemplates;
using EventStoreKit.SearchOptions;
using EventStoreKit.Services.Configuration;
using OSMD.Common.ReadModels;

namespace OSMD.Projections.Projections
{
    public class CustomerProjection : SqlProjectionBase<CustomerModel>
    {
        #region Event handlers

        #endregion

        public CustomerProjection(
            ILogger<CustomerProjection> logger, 
            IScheduler scheduler,
            IEventStoreConfiguration config,
            IDbProviderFactory dbProviderFactory ) : 
            base( logger, scheduler, config, dbProviderFactory )
        {
            //RegisterTemplate<PersonProjectionTemplate<PersonModel>>( ProjectionTemplateOptions.InsertCaching );
        }

        #region Overrides of EventQueueSubscriber

        public string Name { get { return "Customers Projection"; } }

        #endregion

        #region Implementation of IPersonProjection

        public CustomerModel GetById( Guid id )
        {
            return DbProviderFactory.Run( db => db.Single<CustomerModel>( c => c.Id == id ) );
        }
        public QueryResult<CustomerModel> GetCustomers( SearchOptions options )
        {
            return Search( options );
        }

        #endregion
    }
}
