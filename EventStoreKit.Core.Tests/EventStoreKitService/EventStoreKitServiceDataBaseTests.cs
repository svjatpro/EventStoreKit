using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using EventStoreKit.DbProviders;
using EventStoreKit.Handler;
using EventStoreKit.Messages;
using EventStoreKit.Projections;
using EventStoreKit.Services;
using EventStoreKit.Utility;
using FluentAssertions;
using NUnit.Framework;

namespace EventStoreKit.Tests
{
    public static class TestUtility
    {
        public static IList<TReadModel> GetStorage<TReadModel>( this ConcurrentDictionary<Type,IList> storageMap )
        {
            return storageMap.GetOrAdd( typeof(TReadModel), new List<TReadModel>() ).OfType<List<TReadModel>>();
        }
    }

    [TestFixture]
    public class EventStoreKitServiceDataBaseTests
    {
        #region Private members

        private EventStoreKitService Service;

        private static ConcurrentDictionary<Type, IList> StorageMap1 = new ConcurrentDictionary<Type, IList>();
        private static ConcurrentDictionary<Type, IList> StorageMap2 = new ConcurrentDictionary<Type, IList>();

        private class DbProviderFactory1 : IDbProviderFactory
        {
            public DbProviderFactory1( IDataBaseConfiguration defaultDataBaseConfiguration ) { DefaultDataBaseConfiguration = defaultDataBaseConfiguration; }
            public IDataBaseConfiguration DefaultDataBaseConfiguration { get; set; }
            public IDbProvider Create() { return new DbProviderStub( StorageMap1 ); }
            public IDbProvider Create( IDataBaseConfiguration configuration ) { return new DbProviderStub( StorageMap1 ); }
        }
        private class DbProviderFactory2 : IDbProviderFactory
        {
            public DbProviderFactory2( IDataBaseConfiguration defaultDataBaseConfiguration ) { DefaultDataBaseConfiguration = defaultDataBaseConfiguration; }
            public IDataBaseConfiguration DefaultDataBaseConfiguration { get; set; }
            public IDbProvider Create() { return new DbProviderStub( StorageMap2 ); }
            public IDbProvider Create( IDataBaseConfiguration configuration ) { return new DbProviderStub( StorageMap2 ); }
        }
        
        private class TestReadModel1 { public Guid Id { get; set; } public string Name { get; set; } }
        private class TestReadModel2 { public Guid Id { get; set; } public string Name { get; set; } }
        private class TestReadModel3 { public Guid Id { get; set; } public string Name { get; set; } }

        private class TestEvent1 : DomainEvent { public string Name { get; set; } }

        private class Subscriber1 : SqlProjectionBase<TestReadModel1>, IEventHandler<TestEvent1>
        {
            public Subscriber1( IEventStoreSubscriberContext context ) : base( context ) {}
            public void Handle( TestEvent1 message ) { DbProviderFactory.Run( db => db.Insert( message.CopyTo( m => new TestReadModel1() ) ) ); }
            public IDbProviderFactory GetDbProviderFactory() { return DbProviderFactory; }
        }
        private class Subscriber2 : SqlProjectionBase<TestReadModel2>, IEventHandler<TestEvent1>
        {
            public Subscriber2( IEventStoreSubscriberContext context ) : base( context )
            {
                RegisterReadModel<TestReadModel3>();
            }
            public void Handle( TestEvent1 message )
            {
                DbProviderFactory.Run( db =>
                {
                    db.Insert( message.CopyTo( m => new TestReadModel2() ) );
                    return db.Insert( message.CopyTo( m => new TestReadModel3() ) );
                } );
            }
            public IDbProviderFactory GetDbProviderFactory() { return DbProviderFactory; }
        }
        
        [SetUp]
        protected void Setup()
        {
            Service = new EventStoreKitService( false );

            StorageMap1.Clear();
            StorageMap2.Clear();
        }

        [TearDown]
        protected void Teardown()
        {
            Service?.Dispose();
        }

        private TestEvent1 RaiseEvent()
        {
            var id = Guid.NewGuid();
            var msg = new TestEvent1 { Id = id, Name = "name_" + id };
            Service.RaiseEvent( msg );
            Service.Wait();
            Thread.Sleep( 500 );
            return msg;
        }
       
        #endregion

        [Test]
        public void SetDatabaseShouldMapSubscriversToSingleDb()
        {
            Service
                .SetDataBase<DbProviderFactory1>( new DataBaseConfiguration( DataBaseConnectionType.None, "db1" ) )
                .SetEventStoreDataBase<DbProviderFactory2>( new DataBaseConfiguration( DataBaseConnectionType.None, "db2" ) )
                .RegisterEventSubscriber<Subscriber1>()
                .RegisterEventSubscriber<Subscriber2>()
                .Initialize();

            RaiseEvent();

            StorageMap1.GetStorage<TestReadModel1>().Count.Should().Be( 1 );
            StorageMap1.GetStorage<TestReadModel2>().Count.Should().Be( 1 );
            StorageMap1.GetStorage<TestReadModel3>().Count.Should().Be( 1 );

            StorageMap2.GetStorage<TestReadModel1>().Count.Should().Be( 0 );
            StorageMap2.GetStorage<TestReadModel2>().Count.Should().Be( 0 );
            StorageMap2.GetStorage<TestReadModel3>().Count.Should().Be( 0 );
        }

        [Test]
        public void SetSubscribersDatabaseShouldMapSubscriversToSingleDb()
        {
            Service
                .SetSubscriberDataBase<DbProviderFactory1>( new DataBaseConfiguration( DataBaseConnectionType.None, "db1" ) )
                .SetEventStoreDataBase<DbProviderFactory2>( new DataBaseConfiguration( DataBaseConnectionType.None, "db2" ) )
                .RegisterEventSubscriber<Subscriber1>()
                .RegisterEventSubscriber<Subscriber2>()
                .Initialize();

            RaiseEvent();

            StorageMap1.GetStorage<TestReadModel1>().Count.Should().Be( 1 );
            StorageMap1.GetStorage<TestReadModel2>().Count.Should().Be( 1 );
            StorageMap1.GetStorage<TestReadModel3>().Count.Should().Be( 1 );

            StorageMap2.GetStorage<TestReadModel1>().Count.Should().Be( 0 );
            StorageMap2.GetStorage<TestReadModel2>().Count.Should().Be( 0 );
            StorageMap2.GetStorage<TestReadModel3>().Count.Should().Be( 0 );
        }

        [Test]
        public void MapSubscribersToDifferentDatabaseConfig()
        {
            var config1 = new DataBaseConfiguration( DataBaseConnectionType.None, "db1" );
            var config2 = new DataBaseConfiguration( DataBaseConnectionType.None, "db2" );
            Service
                .SetSubscriberDataBase<DbProviderFactory1>( config1 )
                .RegisterEventSubscriber<Subscriber1>()
                .RegisterEventSubscriber<Subscriber2>( config2 )
                .Initialize();

            RaiseEvent();

            Service.GetSubscriber<Subscriber1>().GetDbProviderFactory().DefaultDataBaseConfiguration.Should().Be( config1 );
            Service.GetSubscriber<Subscriber2>().GetDbProviderFactory().DefaultDataBaseConfiguration.Should().Be( config2 );
        }

        [Test]
        public void MapSubscribersToDifferentDatabaseProviders()
        {
            Service
                .SetSubscriberDataBase<DbProviderFactory1>( new DataBaseConfiguration( DataBaseConnectionType.None, "db1" ) )
                .RegisterEventSubscriber<Subscriber1>()
                .RegisterEventSubscriber<Subscriber2>( new DbProviderFactory2( new DataBaseConfiguration( DataBaseConnectionType.None, "db2" ) ) )
                .Initialize();

            RaiseEvent();

            Service.GetSubscriber<Subscriber1>().GetDbProviderFactory().Should().BeOfType<DbProviderFactory1>();
            Service.GetSubscriber<Subscriber2>().GetDbProviderFactory().Should().BeOfType<DbProviderFactory2>();
        }

        [Test]
        public void MapSubscribersToDifferentDatabaseProviders2()
        {
            Service
                .RegisterEventSubscriber( context => new Subscriber1( context ), new DbProviderFactory1( new DataBaseConfiguration( DataBaseConnectionType.None, "db1" ) ) )
                .RegisterEventSubscriber<Subscriber2>( new DbProviderFactory2( new DataBaseConfiguration( DataBaseConnectionType.None, "db2" ) ) )
                .Initialize();

            RaiseEvent();

            Service.GetSubscriber<Subscriber1>().GetDbProviderFactory().Should().BeOfType<DbProviderFactory1>();
            Service.GetSubscriber<Subscriber2>().GetDbProviderFactory().Should().BeOfType<DbProviderFactory2>();
        }

    }
}
