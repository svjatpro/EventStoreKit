﻿using System;
using Autofac;
using EventStoreKit.Autofac;
using EventStoreKit.DbProviders;
using EventStoreKit.Services;
using FluentAssertions;
using NUnit.Framework;

namespace EventStoreKit.Tests
{
    [TestFixture]
    public class EventStoreKitServiceContainerInitializerTests
    {
        #region Private members

        private IContainer Container;
        private IEventStoreKitService Service;

        private class DbProviderFactory1 : DbProviderFactoryStub
        {
            public new IDataBaseConfiguration DefaultDataBaseConfiguration { get; }
            public DbProviderFactory1( IDataBaseConfiguration config ) { DefaultDataBaseConfiguration = config; } 
        }

        private void InitializeContainer( Func<EventStoreKitService> initializer )
        {
            var builder = new ContainerBuilder();
            builder.InitializeEventStoreKitService( initializer );
            Container = builder.Build();
            Service = Container.Resolve<IEventStoreKitService>();
        }

        #endregion

        #region Default DataBase

        [Test]
        public void DefaultDbProviderFactorySetByServiceShouldBeAvailableThroughTheContainer()
        {
            var dbConfig = new DataBaseConfiguration( DataBaseConnectionType.SqlLite, "data source=db1" );
            InitializeContainer( 
                () => new EventStoreKitService().SetDataBase<DbProviderFactory1>(dbConfig) );

            Container.Resolve<IDbProviderFactory>().GetType().Should().Be( typeof(DbProviderFactory1) );
            Container.Resolve<IDataBaseConfiguration>().Should().Be(dbConfig);
            // container.Resolve<IDataBaseConfiguration>() + keyed(subscriber type)
            // container.Resolve<IEventStoreSubscriberContext>() + keyed(subscriber type) // ?
            // container.Resolve<IDataBaseProvider>() + keyed(subscriber type)
            // container.Resolve<IDataBaseProviderFactory>() + keyed(subscriber type)
        }

        [Test]
        public void DefaultDbProviderFactoryAndConfigurationSetByContainerShouldBeAvailableThroughTheService()
        {
            // builder.Register( new DataBaseConfiguration( DbConnectionType.SqlLite, "data source=db1" ) ).As<IDataBaseConfiguration>()
            // builder.RegisterType<Linq2DbProviderFactory>().As<IDataBaseProviderFactory>()

            // container.Resolve<IDataBaseConfiguration>() + keyed
            // container.Resolve<IEventStoreSubscriberContext>() + keyed
            // container.Resolve<IDataBaseProvider>() + keyed
            // container.Resolve<IDataBaseProviderFactory>() + keyed

            // server.GetDataBaseProviderFactory<TModel>()
            // server.GetDataBaseProviderFactory<Commit>()
        }

        [Test]
        public void DefaultDbProviderFactorySetByContainerShouldBeAvailableThroughTheService()
        {
            // builder.Register( ctx => new Linq2DbProviderFactory( new DataBaseConfiguration( DbConnectionType.SqlLite, "data source=db1" )  ) ).As<IDataBaseProviderFactory>()

            // container.Resolve<IDataBaseConfiguration>() + keyed - null / failed (denied)
            // container.Resolve<IEventStoreSubscriberContext>() + keyed
            // container.Resolve<IDataBaseProvider>() + keyed
            // container.Resolve<IDataBaseProviderFactory>() + keyed

            // server.GetDataBaseProviderFactory<TModel>()
            // server.GetDataBaseProviderFactory<Commit>()
        }

        #endregion

        #region Separate DataBase for EventStore and Subscribers

        [Test]
        public void StoreAndSubscribersDbProviderFactorySetByServiceShouldBeAvailabeThroughTheContainer()
        {
            // service.SetEventStoreDataBase<ProviderFactory1>( new DataBaseConfiguration( DbConnectionType.SqlLite, "data source=db1" ) )
            // service.SetSubscriberDataBase<ProviderFactory2>( new DataBaseConfiguration( DbConnectionType.SqlLite, "data source=db2" ) )

            // container.Resolve<IDataBaseConfiguration>() + keyed(subscriber type) - resolves Subscriber factory
            // container.Resolve<IEventStoreSubscriberContext>() + keyed(subscriber type) - resolves Subscriber factory
            // container.ResolveKeyed<IDataBaseProvider>() + keyed(subscriber type) - resolves Subscriber factory
            // container.ResolveKeyed<IDataBaseProviderFactory>() + keyed(subscriber type) - resolves Subscriber factory
        }

        #endregion


        /*
        // separate DB for subscriber

        .RegisterEventSubscriber<TSubscriber>( new DataBaseConfiguration( DataBaseConnectionType.SqlLite, ConnectionStringDb3 ) );

        --

        // separate */

    }
}
