using System;
using System.Collections.Generic;
using System.Data;
using System.Linq.Expressions;
using System.Threading;
using EventStoreKit.DbProviders;
using EventStoreKit.Handler;
using EventStoreKit.linq2db;
using EventStoreKit.Messages;
using EventStoreKit.Projections;
using EventStoreKit.Services;
using EventStoreKit.Utility;
using FluentAssertions;
using FluentAssertions.Primitives;
using NEventStore;
using NEventStore.Persistence.Sql;
using NEventStore.Persistence.Sql.SqlDialects;
using NUnit.Framework;

namespace EventStoreKit.Tests
{
    [TestFixture]
    public class EventStoreKitServiceCommandHandlerTests
    {
        #region Private members

        private const string ConnectionStringDb1 = "data source=db1";
        private const string ConnectionStringDb2 = "data source=db2";
        private const string ConnectionStringDb3 = "data source=db3";

        private EventStoreKitService Service;
// ReSharper disable NotAccessedField.Local
        private Subscriber1 Projection1;
        private Subscriber2 Projection2;
        private IDbProviderFactory EventStoreDb;
        private IDbProviderFactory ReadModel1Db;
        private IDbProviderFactory ReadModel2Db;
        private IDbProviderFactory ReadModel3Db;
// ReSharper restore NotAccessedField.Local

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
            // clean all data
            var clean = new Action<string>( connectionString =>
            {
                new Linq2DbProviderFactory( new DataBaseConfiguration
                    {
                        DbConnectionType = DbConnectionType.SqlLite,
                        ConnectionString = connectionString
                    } )
                    .Run( db =>
                    {
                        db.CreateTable<TestReadModel1>();
                        db.CreateTable<TestReadModel2>();
                        db.CreateTable<TestReadModel3>();
                    } );

                using( var wireup = Wireup
                    .Init()
                    .UsingSqlPersistence( connectionString, DataBaseConfiguration.ResolveSqlProviderName( DbConnectionType.SqlLite ), connectionString )
                    .WithDialect( new SqliteDialect() )
                    .InitializeStorageEngine()
                    .UsingJsonSerialization()
                    .Build() )
                {
                }
            } );

            clean( ConnectionStringDb1 );
            clean( ConnectionStringDb2 );
            clean( ConnectionStringDb3 );

            Thread.Sleep( 100 );
        }

        [SetUp]
        protected void Setup()
        {
            Service = new EventStoreKitService();
        }

        private void InitializeService()
        {
            Projection1 = Service.ResolveSubscriber<Subscriber1>();
            Projection2 = Service.ResolveSubscriber<Subscriber2>();

            EventStoreDb = Service.ResolveDbProviderFactory<Commits>();
            ReadModel1Db = Service.ResolveDbProviderFactory<TestReadModel1>();
            ReadModel2Db = Service.ResolveDbProviderFactory<TestReadModel2>();
            ReadModel3Db = Service.ResolveDbProviderFactory<TestReadModel3>();

            Thread.Sleep( 100 );
        }

        private TestEvent1 RaiseEvent()
        {
            var id = Guid.NewGuid();
            var msg = new TestEvent1
            {
                Id = id,
                Name = "name_" + id
            };
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
                .SetEventStoreDataBase<Linq2DbProviderFactory>( DbConnectionType.SqlLite, ConnectionStringDb1 )
                .RegisterEventSubscriber<Subscriber1>()
                .RegisterEventSubscriber<Subscriber2>();
            InitializeService();

            var msg = RaiseEvent();

            EventStoreDb.Should().ContainsCommit( msg.Id );
            EventStoreDb.Should().NotContainsReadModel<TestReadModel1>( r => r.Id == msg.Id );
            EventStoreDb.Should().NotContainsReadModel<TestReadModel2>( r => r.Id == msg.Id );
            EventStoreDb.Should().NotContainsReadModel<TestReadModel3>( r => r.Id == msg.Id );

            ReadModel1Db.Should().NotContainsCommit( msg.Id );
            ReadModel1Db.Should().ContainsReadModel<TestReadModel1>( r => r.Id == msg.Id );
            ReadModel1Db.Should().ContainsReadModel<TestReadModel2>( r => r.Id == msg.Id );
            ReadModel1Db.Should().ContainsReadModel<TestReadModel3>( r => r.Id == msg.Id );
        }

    }
}
