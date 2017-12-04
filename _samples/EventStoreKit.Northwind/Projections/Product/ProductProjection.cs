﻿using System;
using EventStoreKit.DbProviders;
using EventStoreKit.Handler;
using EventStoreKit.Northwind.Messages.Commands;
using EventStoreKit.Projections;
using EventStoreKit.SearchOptions;
using EventStoreKit.Services;
using OSMD.Common.ReadModels;

namespace EventStoreKit.Northwind.Projections.Customer
{
    public class ProductProjection : SqlProjectionBase<ProductModel>,
        IEventHandler<ProductCreatedEvent>
    {
        #region Event handlers

        public void Handle(ProductCreatedEvent msg)
        {
            DbProviderFactory.Run( db => db.Insert( new ProductModel
            {
                Id = msg.Id,
                ProductName = msg.ProductName,
                UnitPrice = msg.UnitPrice
            } ) );
        }

        #endregion

        public ProductProjection( IEventStoreSubscriberContext context ) : base( context )
        {
        }

        public string Name => "Products Projection";

        public CustomerModel GetById( Guid id )
        {
            return DbProviderFactory.Run( db => db.Single<CustomerModel>( c => c.Id == id ) );
        }
        public QueryResult<ProductModel> GetProducts( SearchOptions.SearchOptions options )
        {
            return Search( options );
        }
       
    }
}