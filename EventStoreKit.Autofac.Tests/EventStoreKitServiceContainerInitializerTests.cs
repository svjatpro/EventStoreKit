using System;
using System.Reactive.Concurrency;
using Autofac;
using EventStoreKit.Autofac;
using EventStoreKit.DbProviders;
using EventStoreKit.Logging;
using EventStoreKit.Projections;
using EventStoreKit.Services;
using EventStoreKit.Services.Configuration;
using EventStoreKit.Utility;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace EventStoreKit.Tests
{
    [TestFixture]
    public class EventStoreKitServiceContainerInitializerTests
    {
        #region Private members

        private ContainerBuilder Builder;
        private IContainer Container;
        private EventStoreKitService Service;

        private class DbProviderFactory1 : IDbProviderFactory
        {
            public IDataBaseConfiguration DefaultDataBaseConfiguration { get; }
            public IDbProvider Create() { return new DbProviderStub( null ); }
            public IDbProvider Create( IDataBaseConfiguration configuration ) { return new DbProviderStub( null ); }
            public DbProviderFactory1( IDataBaseConfiguration config ) { DefaultDataBaseConfiguration = config; } 
        }
        private class DbProviderFactory2 : DbProviderFactory1
        {
            public DbProviderFactory2( IDataBaseConfiguration config ) : base( config ) { }
        }
        public class Subscriber1 : EventQueueSubscriber
        {
            public Subscriber1( ) : base( new EventStoreSubscriberContext
            {
                Configuration = new EventStoreConfiguration(),
                DbProviderFactory = new DbProviderFactoryStub(),
                Scheduler = Substitute.For<IScheduler>(),
                Logger = Substitute.For<ILogger>()
            } ) { }
        }

        private void InitializeContainer( Func<EventStoreKitService> initializer )
        {
            Builder.InitializeEventStoreKitService( initializer );
            Container = Builder.Build();
            Service = Container.Resolve<IEventStoreKitService>().OfType<EventStoreKitService>();
        }

        [SetUp]
        protected void Setup()
        {
            Builder = new ContainerBuilder();
        }

        #endregion

        #region Default DataBase

        [Test]
        public void DefaultDbProviderFactorySetByServiceShouldBeAvailableThroughTheContainer()
        {
            var dbConfig = new DataBaseConfiguration( DataBaseConnectionType.None, "data source1" );
            InitializeContainer( () => new EventStoreKitService( false ).SetDataBase<DbProviderFactory1>(dbConfig) );
            
            Container.Resolve<IDataBaseConfiguration>().Should().Be( dbConfig );
            Container.Resolve<IDbProviderFactory>().GetType().Should().Be( typeof(DbProviderFactory1) );

            var dbProvider1 = Container.Resolve<IDbProvider>();
            dbProvider1.GetType().Should().Be( typeof( DbProviderStub ) );
            Container.Resolve<IDbProvider>().Should().NotBe( dbProvider1 );
            Container.Resolve<Func<IDbProvider>>()().Should().NotBe( dbProvider1 );
        }

        [Test]
        public void DefaultDbProviderFactoryAndConfigurationSetByContainerShouldBeAvailableThroughTheService()
        {
            var dbConfig = new DataBaseConfiguration( DataBaseConnectionType.None, "data source1" );
            Builder.RegisterInstance( dbConfig ).As<IDataBaseConfiguration>();
            Builder.RegisterType<DbProviderFactory1>().As<IDbProviderFactory>();
            InitializeContainer( () => new EventStoreKitService( false ) );

            Container.Resolve<IDataBaseConfiguration>().Should().Be( dbConfig );
            Container.Resolve<IDbProviderFactory>().GetType().Should().Be( typeof( DbProviderFactory1 ) );

            var dbProvider1 = Container.Resolve<IDbProvider>();
            dbProvider1.GetType().Should().Be( typeof( DbProviderStub ) );
            Container.Resolve<IDbProvider>().Should().NotBe( dbProvider1 );
            Container.Resolve<Func<IDbProvider>>()().Should().NotBe( dbProvider1 );

            Service.GetDataBaseProviderFactory().DefaultDataBaseConfiguration.Should().Be( dbConfig );
        }

        [Test]
        public void DefaultDbProviderFactorySetByContainerShouldBeAvailableThroughTheService()
        {
            var dbConfig = new DataBaseConfiguration( DataBaseConnectionType.None, "data source1" );
            Builder.RegisterInstance( new DbProviderFactory1( dbConfig ) ).As<IDbProviderFactory>();
            InitializeContainer( () => new EventStoreKitService( false ) );

            Container.Resolve<IDbProviderFactory>().GetType().Should().Be( typeof( DbProviderFactory1 ) );

            var dbProvider1 = Container.Resolve<IDbProvider>();
            dbProvider1.GetType().Should().Be( typeof( DbProviderStub ) );
            Container.Resolve<IDbProvider>().Should().NotBe( dbProvider1 );
            Container.Resolve<Func<IDbProvider>>()().Should().NotBe( dbProvider1 );

            Service.GetDataBaseProviderFactory().DefaultDataBaseConfiguration.Should().Be( dbConfig );
        }

        #endregion

        #region Separate DataBase for EventStore and Subscribers

        [Test]
        public void StoreAndSubscribersDbProviderFactorySetByServiceShouldBeAvailabeThroughTheContainer()
        {
            var dbConfig1 = new DataBaseConfiguration( DataBaseConnectionType.None, "data source1" );
            var dbConfig2 = new DataBaseConfiguration( DataBaseConnectionType.None, "data source2" );
            InitializeContainer( () => new EventStoreKitService( false )
                .SetEventStoreDataBase<DbProviderFactory1>( dbConfig1 )
                .SetSubscriberDataBase<DbProviderFactory2>( dbConfig2 ) );

            // default data base is Subscriber data base
            Container.Resolve<IDataBaseConfiguration>().Should().Be( dbConfig2 );
            Container.Resolve<IDbProviderFactory>().GetType().Should().Be( typeof( DbProviderFactory2 ) );
        }

        #endregion

        #region RegisterSubscribers

        [Test]
        public void SubscriberRegisteredByContainerShouldBeAvailableThroughTheService()
        {
            Builder.RegisterType<Subscriber1>().AsSelf().SingleInstance();
            InitializeContainer( () => new EventStoreKitService( false ) );

            Service.GetSubscriber<Subscriber1>().Should().Be( Container.Resolve<Subscriber1>() );
        }

        [Test]
        public void SubscriberRegisteredByServiceShouldBeAvailableThroughTheContainer()
        {
            InitializeContainer( () => new EventStoreKitService( false ).RegisterEventSubscriber<Subscriber1>() );

            Container.Resolve<Subscriber1>().Should().Be( Service.GetSubscriber<Subscriber1>() );
        }

        #endregion

    }
}
