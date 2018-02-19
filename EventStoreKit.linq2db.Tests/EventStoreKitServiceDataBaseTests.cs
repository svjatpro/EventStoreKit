using System;
using System.Threading;
using EventStoreKit.DbProviders;
using EventStoreKit.Handler;
using EventStoreKit.linq2db;
using EventStoreKit.Messages;
using EventStoreKit.Projections;
using EventStoreKit.Services;
using EventStoreKit.Utility;
using FluentAssertions;
using NEventStore;
using NEventStore.Persistence.Sql.SqlDialects;
using NUnit.Framework;

namespace EventStoreKit.Tests
{
    [TestFixture]
    public class EventStoreKitServiceDataBaseTests
    {
        #region Private members

        private const string ConnectionStringDb1 = "data source=db1";
        private const string ConnectionStringDb2 = "data source=db2";
        private const string ConnectionStringDb3 = "data source=db3";

        private EventStoreKitService Service;

        private class TestReadModel1 { public Guid Id { get; set; } public string Name { get; set; } }
        private class TestReadModel2 { public Guid Id { get; set; } public string Name { get; set; } }
        private class TestReadModel3 { public Guid Id { get; set; } public string Name { get; set; } }

        private class TestEvent1 : DomainEvent { public string Name { get; set; } }

        private class Subscriber1 : SqlProjectionBase<TestReadModel1>, IEventHandler<TestEvent1>
        {
            public Subscriber1( IEventStoreSubscriberContext context ) : base( context ){}
            public void Handle( TestEvent1 message ) { DbProviderFactory.Run( db => db.Insert( message.CopyTo( m => new TestReadModel1() ) ) ); }
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
        }

        [OneTimeSetUp]
        protected void ResetDataBases()
        {
            // make sure all tables exists
            var initializeDb = new Action<string>( connectionString =>
            {
                using( var wireup = Wireup
                    .Init()
                    .UsingSqlPersistence( connectionString, DataBaseConfiguration.ResolveSqlProviderName( DataBaseConnectionType.SqlLite ), connectionString )
                    .WithDialect( new SqliteDialect() )
                    .InitializeStorageEngine()
                    .UsingJsonSerialization()
                    .Build() )
                {
                }

                new Linq2DbProviderFactory( new DataBaseConfiguration( DataBaseConnectionType.SqlLite, connectionString ) )
                    .Run( db =>
                    {
                        db.CreateTable<TestReadModel1>();
                        db.CreateTable<TestReadModel2>();
                        db.CreateTable<TestReadModel3>();
                    } );
            } );

            initializeDb( ConnectionStringDb1 );
            initializeDb( ConnectionStringDb2 );
            initializeDb( ConnectionStringDb3 );

            Thread.Sleep( 100 );
        }

        [SetUp]
        protected void Setup()
        {
            Service = new EventStoreKitService( false );
        }

        [TearDown]
        protected void TearDown()
        {
            Service?.Dispose();
        }
        
        private TestEvent1 RaiseEvent()
        {
            var id = Guid.NewGuid();
            var msg = new TestEvent1  { Id = id, Name = "name_" + id };
            Service.RaiseEvent( msg );
            Service.Wait();
            Thread.Sleep( 300 );
            return msg;
        }
       
        #endregion

        [Test]
        public void EventStoreShouldBeMappedToSingleDb()
        {
            Service
                .SetEventStoreDataBase<Linq2DbProviderFactory>( new DataBaseConfiguration( DataBaseConnectionType.SqlLite, ConnectionStringDb1 ) )
                .RegisterEventSubscriber<Subscriber1>()
                .Initialize();
            
            var msg = RaiseEvent();

            var db = new Linq2DbProviderFactory( new DataBaseConfiguration( DataBaseConnectionType.SqlLite, ConnectionStringDb1 ) );
            db.Should().ContainsCommit( msg.Id );
        }

        [Test]
        public void AllProjectionsShouldBeMappedToSingleDb()
        {
            Service
                .SetSubscriberDataBase<Linq2DbProviderFactory>( new DataBaseConfiguration( DataBaseConnectionType.SqlLite, ConnectionStringDb1 ) )
                .RegisterEventSubscriber<Subscriber1>()
                .RegisterEventSubscriber<Subscriber2>()
                .Initialize();
            
            var msg = RaiseEvent();

            var db = new Linq2DbProviderFactory( new DataBaseConfiguration( DataBaseConnectionType.SqlLite, ConnectionStringDb1 ) );
            db.Should().NotContainsCommit( msg.Id );
            db.Should().ContainsReadModel<TestReadModel1>( r => r.Id == msg.Id );
            db.Should().ContainsReadModel<TestReadModel2>( r => r.Id == msg.Id );
            db.Should().ContainsReadModel<TestReadModel3>( r => r.Id == msg.Id );
        }

        [Test]
        public void EventStoreAndSubscribersShouldBeMappedToSingleDb()
        {
            Service
                .SetDataBase<Linq2DbProviderFactory>( new DataBaseConfiguration( DataBaseConnectionType.SqlLite, ConnectionStringDb1 ) )
                .RegisterEventSubscriber<Subscriber1>()
                .RegisterEventSubscriber<Subscriber2>()
                .Initialize();
            
            var msg = RaiseEvent();

            var db = new Linq2DbProviderFactory( new DataBaseConfiguration( DataBaseConnectionType.SqlLite, ConnectionStringDb1 ) );
            db.Should().ContainsCommit( msg.Id );
            db.Should().ContainsReadModel<TestReadModel1>( r => r.Id == msg.Id );
            db.Should().ContainsReadModel<TestReadModel2>( r => r.Id == msg.Id );
            db.Should().ContainsReadModel<TestReadModel3>( r => r.Id == msg.Id );
        }

        [Test]
        public void SubscribersAndEventStoreShouldBeMappedToSingeDb()
        {
            Service
                .SetEventStoreDataBase<Linq2DbProviderFactory>( new DataBaseConfiguration( DataBaseConnectionType.SqlLite, ConnectionStringDb1 ) )
                .SetSubscriberDataBase<Linq2DbProviderFactory>( new DataBaseConfiguration( DataBaseConnectionType.SqlLite, ConnectionStringDb1 ) )
                .RegisterEventSubscriber<Subscriber1>()
                .RegisterEventSubscriber<Subscriber2>()
                .Initialize();
            
            var msg = RaiseEvent();

            var db = new Linq2DbProviderFactory( new DataBaseConfiguration( DataBaseConnectionType.SqlLite, ConnectionStringDb1 ) );
            db.Should().ContainsCommit( msg.Id );
            db.Should().ContainsReadModel<TestReadModel1>( r => r.Id == msg.Id );
            db.Should().ContainsReadModel<TestReadModel2>( r => r.Id == msg.Id );
            db.Should().ContainsReadModel<TestReadModel3>( r => r.Id == msg.Id );
        }

        [Test]
        public void SubscribersAndEventStoreShouldBeMappedToDifferentDbs()
        {
            Service
                .SetEventStoreDataBase<Linq2DbProviderFactory>( new DataBaseConfiguration( DataBaseConnectionType.SqlLite, ConnectionStringDb1 ) )
                .SetSubscriberDataBase<Linq2DbProviderFactory>( new DataBaseConfiguration( DataBaseConnectionType.SqlLite, ConnectionStringDb2 ) )
                .RegisterEventSubscriber<Subscriber1>()
                .RegisterEventSubscriber<Subscriber2>( new DataBaseConfiguration( DataBaseConnectionType.SqlLite, ConnectionStringDb3 ) )
                .Initialize();

            var msg = RaiseEvent();

            var db1 = new Linq2DbProviderFactory( new DataBaseConfiguration( DataBaseConnectionType.SqlLite, ConnectionStringDb1 ) );
            var db2 = new Linq2DbProviderFactory( new DataBaseConfiguration( DataBaseConnectionType.SqlLite, ConnectionStringDb2 ) );

            db1.Should().ContainsCommit(msg.Id);
            db1.Should().NotContainsReadModel<TestReadModel1>(r => r.Id == msg.Id);
            db1.Should().NotContainsReadModel<TestReadModel2>(r => r.Id == msg.Id);
            db1.Should().NotContainsReadModel<TestReadModel3>(r => r.Id == msg.Id);

            db2.Should().NotContainsCommit( msg.Id );
            db2.Should().ContainsReadModel<TestReadModel1>( r => r.Id == msg.Id );
            db2.Should().NotContainsReadModel<TestReadModel2>( r => r.Id == msg.Id );
            db2.Should().NotContainsReadModel<TestReadModel3>( r => r.Id == msg.Id );
        }

        [Test]
        public void EventStoreAndSubscribersShouldBeMappedToDifferentDbs()
        {
            Service
                .SetSubscriberDataBase<Linq2DbProviderFactory>( new DataBaseConfiguration( DataBaseConnectionType.SqlLite, ConnectionStringDb2 ) )
                .SetEventStoreDataBase<Linq2DbProviderFactory>( new DataBaseConfiguration( DataBaseConnectionType.SqlLite, ConnectionStringDb1 ) )
                .RegisterEventSubscriber<Subscriber1>()
                .RegisterEventSubscriber<Subscriber2>( new DataBaseConfiguration( DataBaseConnectionType.SqlLite, ConnectionStringDb3 ) )
                .Initialize();

            var msg = RaiseEvent();

            var db1 = new Linq2DbProviderFactory( new DataBaseConfiguration( DataBaseConnectionType.SqlLite, ConnectionStringDb1 ) );
            var db2 = new Linq2DbProviderFactory( new DataBaseConfiguration( DataBaseConnectionType.SqlLite, ConnectionStringDb2 ) );
            var db3 = new Linq2DbProviderFactory( new DataBaseConfiguration( DataBaseConnectionType.SqlLite, ConnectionStringDb3 ) );

            db1.Should().ContainsCommit( msg.Id );
            db1.Should().NotContainsReadModel<TestReadModel1>( r => r.Id == msg.Id );
            db1.Should().NotContainsReadModel<TestReadModel2>( r => r.Id == msg.Id );
            db1.Should().NotContainsReadModel<TestReadModel3>( r => r.Id == msg.Id );

            db2.Should().NotContainsCommit( msg.Id );
            db2.Should().ContainsReadModel<TestReadModel1>( r => r.Id == msg.Id );
            db2.Should().NotContainsReadModel<TestReadModel2>( r => r.Id == msg.Id );
            db2.Should().NotContainsReadModel<TestReadModel3>( r => r.Id == msg.Id );

            db3.Should().NotContainsCommit( msg.Id );
            db3.Should().NotContainsReadModel<TestReadModel1>( r => r.Id == msg.Id );
            db3.Should().ContainsReadModel<TestReadModel2>( r => r.Id == msg.Id );
            db3.Should().ContainsReadModel<TestReadModel3>( r => r.Id == msg.Id );
        }
    }
}
