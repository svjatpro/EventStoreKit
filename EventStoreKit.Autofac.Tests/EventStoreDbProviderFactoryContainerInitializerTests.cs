using System;
using System.Reactive.Concurrency;
using Autofac;
using EventStoreKit.DbProviders;
using EventStoreKit.Logging;
using EventStoreKit.Projections;
using EventStoreKit.Services;
using EventStoreKit.Services.Configuration;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace EventStoreKit.Tests
{
    [TestFixture]
    public class EventStoreDbProviderFactoryContainerInitializerTests : BasicContainerInitializerTests
    {
        #region Private members

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
            public Subscriber1() : base( 
                new EventStoreSubscriberContext
                (
                    new EventStoreConfiguration(),
                    Substitute.For<ILogger>(),
                    Substitute.For<IScheduler>(),
                    new DbProviderFactoryStub()
                ) )
            { }
        }

        #endregion

        [Test]
        public void DefaultDbProviderFactorySetByServiceShouldBeAvailableThroughTheContainer()
        {
            var dbConfig = new DataBaseConfiguration( DataBaseConnectionType.None, "data source1" );
            InitializeContainer( builder => builder.SetDataBase<DbProviderFactory1>( dbConfig ) );

            Container.Resolve<IDataBaseConfiguration>().Should().Be( dbConfig );
            Container.Resolve<IDbProviderFactory>().GetType().Should().Be( typeof( DbProviderFactory1 ) );

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
            InitializeContainer();

            Container.Resolve<IDataBaseConfiguration>().Should().Be( dbConfig );
            Container.Resolve<IDbProviderFactory>().GetType().Should().Be( typeof(DbProviderFactory1) );

            var dbProvider1 = Container.Resolve<IDbProvider>();
            dbProvider1.GetType().Should().Be( typeof( DbProviderStub ) );
            Container.Resolve<IDbProvider>().Should().NotBe( dbProvider1 );
            Container.Resolve<Func<IDbProvider>>()().Should().NotBe( dbProvider1 );

            Service.DbProviderFactorySubscriber.Value.DefaultDataBaseConfiguration.Should().Be( dbConfig );
        }

        [Test]
        public void DefaultDbProviderFactorySetByContainerShouldBeAvailableThroughTheService()
        {
            var dbConfig = new DataBaseConfiguration( DataBaseConnectionType.None, "data source1" );
            Builder.RegisterInstance( new DbProviderFactory1( dbConfig ) ).As<IDbProviderFactory>();
            InitializeContainer();

            Container.Resolve<IDbProviderFactory>().GetType().Should().Be( typeof(DbProviderFactory1) );

            var dbProvider1 = Container.Resolve<IDbProvider>();
            dbProvider1.GetType().Should().Be( typeof( DbProviderStub ) );
            Container.Resolve<IDbProvider>().Should().NotBe( dbProvider1 );
            Container.Resolve<Func<IDbProvider>>()().Should().NotBe( dbProvider1 );

            Service.DbProviderFactorySubscriber.Value.DefaultDataBaseConfiguration.Should().Be( dbConfig );
        }

        [Test]
        public void StoreAndSubscribersDbProviderFactorySetByServiceShouldBeAvailabeThroughTheContainer()
        {
            var dbConfig1 = new DataBaseConfiguration( DataBaseConnectionType.None, "data source1" );
            var dbConfig2 = new DataBaseConfiguration( DataBaseConnectionType.None, "data source2" );
            InitializeContainer( builder => builder
                .SetEventStoreDataBase<DbProviderFactory1>( dbConfig1 )
                .SetSubscriberDataBase<DbProviderFactory2>( dbConfig2 ) );

            // default data base is Subscriber data base
            Container.Resolve<IDataBaseConfiguration>().Should().Be( dbConfig2 );
            Container.Resolve<IDbProviderFactory>().GetType().Should().Be( typeof( DbProviderFactory2 ) );
        }
    }
}
