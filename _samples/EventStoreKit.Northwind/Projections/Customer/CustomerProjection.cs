using System;
using System.Reactive.Concurrency;
using EventStoreKit.DbProviders;
using EventStoreKit.Handler;
using EventStoreKit.Logging;
using EventStoreKit.Northwind.Aggregates;
using EventStoreKit.Northwind.Messages.Events;
using EventStoreKit.Projections;
using EventStoreKit.SearchOptions;
using EventStoreKit.Services;
using EventStoreKit.Services.Configuration;
using OSMD.Common.ReadModels;

namespace OSMD.Projections.Projections
{
    public class CustomerProjection : SqlProjectionBase<CustomerModel>,
        IEventHandler<CustomerCreatedEvent>
    {
        #region Event handlers

        public void Handle(CustomerCreatedEvent msg)
        {
            DbProviderFactory.Run( db => db.Insert( new CustomerModel
            {
                Id = msg.Id,
                CompanyName = msg.CompanyName,
                ContactName = msg.ContactName,
                ContactTitle = msg. ContactTitle,
                ContactPhone = msg.ContactPhone,
                Address = msg.Address,
                City = msg.City,
                Region = msg.Region,
                Country = msg.Country,
                PostalCode = msg.PostalCode
    } ) );
        }

        #endregion

        public CustomerProjection( IEventStoreSubscriberContext context ) : base( context )
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
