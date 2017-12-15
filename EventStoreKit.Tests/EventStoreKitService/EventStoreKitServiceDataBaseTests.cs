using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using EventStoreKit.DbProviders;
using EventStoreKit.Handler;
using EventStoreKit.linq2db;
using EventStoreKit.Messages;
using EventStoreKit.Projections;
using EventStoreKit.Services;
using EventStoreKit.Utility;
using FluentAssertions;
using FluentAssertions.Primitives;
using NUnit.Framework;

namespace EventStoreKit.Tests
{
    public static class TestUtility
    {
        public static void NotContainsCommit( this ObjectAssertions factory, Guid id )
        {
            var result = factory.Subject
                .OfType<IDbProviderFactory>()
                .Run( db => db.SingleOrDefault<Commits>( c => c.StreamIdOriginal == id.ToString() ) );
            result.Should().BeNull();
        }
        public static void ContainsCommit( this ObjectAssertions factory, Guid id )
        {
            var result = factory.Subject
                .OfType<IDbProviderFactory>()
                .Run( db => db.SingleOrDefault<Commits>( c => c.StreamIdOriginal == id.ToString() ) );
            result.Should().NotBeNull();
        }
        public static void NotContainsReadModel<TReadModel>( this ObjectAssertions factory, Expression<Func<TReadModel, bool>> predicat ) where TReadModel : class
        {
            var result = factory.Subject
                .OfType<IDbProviderFactory>()
                .Run( db => db.SingleOrDefault( predicat ) );
            result.Should().BeNull();
        }
        public static void ContainsReadModel<TReadModel>( this ObjectAssertions factory, Expression<Func<TReadModel,bool>> predicat ) where TReadModel : class
        {
            var result = factory.Subject
                .OfType<IDbProviderFactory>()
                .Run( db => db.SingleOrDefault( predicat ) );
            result.Should().NotBeNull();
        }
    }

    [TestFixture]
    public class EventStoreKitServiceDataBaseTests
    {
        #region Private members

        private const string ConnectionStringDb1 = "data source=db1";
        private const string ConnectionStringDb2 = "data source=db2";
        private const string ConnectionStringDb3 = "data source=db3";

        private EventStoreKitService Service;
        private Subscriber1 Projection1;
        private Subscriber2 Projection2;
        private IDbProviderFactory EventStoreDb;
        private IDbProviderFactory ReadModel1Db;
        private IDbProviderFactory ReadModel2Db;
        private IDbProviderFactory ReadModel3Db;

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
                        db.CreateTable<Commits>( overwrite: false );
                        db.Delete<Commits>( c => true );
                        db.CreateTable<TestReadModel1>( overwrite: true );
                        db.CreateTable<TestReadModel2>( overwrite: true );
                        db.CreateTable<TestReadModel3>( overwrite: true );
                    } );
            } );

            clean( ConnectionStringDb1 );
            clean( ConnectionStringDb2 );
            clean( ConnectionStringDb3 );
            //Thread.Sleep(200);
        }

        private TestEvent1 RaiseEvent()
        {
            var msg = new TestEvent1
            {
                Id = Guid.NewGuid(),
                Name = "name1"
            };
            Service.Raise( msg );
            Service.Wait();
            //Task.WaitAll( Projection1.WaitMessagesAsync(), Projection2.WaitMessagesAsync() );
            //Projection1.WaitMessagesAsync();
            //Projection2.WaitMessages();
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

        [Test]
        public void AllProjectionsShouldBeMappedToSingleDb()
        {
            Service
                .SetSubscriberDataBase<Linq2DbProviderFactory>( DbConnectionType.SqlLite, ConnectionStringDb1 )
                .RegisterEventSubscriber<Subscriber1>()
                .RegisterEventSubscriber<Subscriber2>();
            InitializeService();

            var msg = RaiseEvent();

            EventStoreDb.Should().NotContainsReadModel<TestReadModel1>( r => r.Id == msg.Id );
            EventStoreDb.Should().NotContainsReadModel<TestReadModel2>( r => r.Id == msg.Id );
            EventStoreDb.Should().NotContainsReadModel<TestReadModel3>( r => r.Id == msg.Id );

            ReadModel1Db.Should().NotContainsCommit( msg.Id );
            ReadModel1Db.Should().ContainsReadModel<TestReadModel1>( r => r.Id == msg.Id );
            ReadModel1Db.Should().ContainsReadModel<TestReadModel2>( r => r.Id == msg.Id );
            ReadModel1Db.Should().ContainsReadModel<TestReadModel3>( r => r.Id == msg.Id );
        }

        [Test]
        public void EventStoreAndSubscribersShouldBeMappedToSingleDb()
        {
            Service
                .SetEventStoreDataBase<Linq2DbProviderFactory>( DbConnectionType.SqlLite, ConnectionStringDb1 )
                .SetSubscriberDataBase<Linq2DbProviderFactory>( DbConnectionType.SqlLite, ConnectionStringDb1 )
                .RegisterEventSubscriber<Subscriber1>()
                .RegisterEventSubscriber<Subscriber2>();
            InitializeService();

            var msg = RaiseEvent();

            EventStoreDb.Should().ContainsCommit( msg.Id );
            EventStoreDb.Should().ContainsReadModel<TestReadModel1>( r => r.Id == msg.Id );
            EventStoreDb.Should().ContainsReadModel<TestReadModel2>( r => r.Id == msg.Id );
            EventStoreDb.Should().ContainsReadModel<TestReadModel3>( r => r.Id == msg.Id );

            ReadModel1Db.Should().ContainsCommit( msg.Id );
            ReadModel1Db.Should().ContainsReadModel<TestReadModel1>( r => r.Id == msg.Id );
            ReadModel1Db.Should().ContainsReadModel<TestReadModel2>( r => r.Id == msg.Id );
            ReadModel1Db.Should().ContainsReadModel<TestReadModel3>( r => r.Id == msg.Id );
        }

        [Test]
        public void SubscribersAndEventStoreShouldBeMappedToSingeDb()
        {
            Service
                .SetSubscriberDataBase<Linq2DbProviderFactory>(DbConnectionType.SqlLite, ConnectionStringDb1)
                .SetEventStoreDataBase<Linq2DbProviderFactory>(DbConnectionType.SqlLite, ConnectionStringDb1)
                .RegisterEventSubscriber<Subscriber1>()
                .RegisterEventSubscriber<Subscriber2>();
            InitializeService();

            var msg = RaiseEvent();
            
            EventStoreDb.Should().ContainsCommit(msg.Id);
            EventStoreDb.Should().ContainsReadModel<TestReadModel1>(r => r.Id == msg.Id);
            EventStoreDb.Should().ContainsReadModel<TestReadModel2>(r => r.Id == msg.Id);
            EventStoreDb.Should().ContainsReadModel<TestReadModel3>(r => r.Id == msg.Id);

            ReadModel1Db.Should().ContainsCommit(msg.Id);
            ReadModel1Db.Should().ContainsReadModel<TestReadModel1>(r => r.Id == msg.Id);
            ReadModel1Db.Should().ContainsReadModel<TestReadModel2>(r => r.Id == msg.Id);
            ReadModel1Db.Should().ContainsReadModel<TestReadModel3>(r => r.Id == msg.Id);
        }

        [Test]
        public void SubscribersStoreShouldBeMappedToDifferentDbs()
        {
            Service
                .SetSubscriberDataBase<Linq2DbProviderFactory>( DbConnectionType.SqlLite, ConnectionStringDb1 )
                .SetEventStoreDataBase<Linq2DbProviderFactory>( DbConnectionType.SqlLite, ConnectionStringDb2 )
                .RegisterEventSubscriber<Subscriber1>()
                .RegisterEventSubscriber<Subscriber2>( DbConnectionType.SqlLite, ConnectionStringDb3 );
            InitializeService();

            var msg = RaiseEvent();

            EventStoreDb.Should().ContainsCommit(msg.Id);
            EventStoreDb.Should().NotContainsReadModel<TestReadModel1>(r => r.Id == msg.Id);
            EventStoreDb.Should().NotContainsReadModel<TestReadModel2>(r => r.Id == msg.Id);
            EventStoreDb.Should().NotContainsReadModel<TestReadModel3>(r => r.Id == msg.Id);

            //ReadModel1Db.Should().NotContainsCommit(msg.Id);
            //ReadModel1Db.Should().ContainsReadModel<TestReadModel1>(r => r.Id == msg.Id);
            //ReadModel1Db.Should().NotContainsReadModel<TestReadModel2>(r => r.Id == msg.Id);
            //ReadModel1Db.Should().NotContainsReadModel<TestReadModel3>(r => r.Id == msg.Id);

            //ReadModel2Db.Should().NotContainsCommit(msg.Id);
            //ReadModel2Db.Should().NotContainsReadModel<TestReadModel1>(r => r.Id == msg.Id);
            //ReadModel2Db.Should().ContainsReadModel<TestReadModel2>(r => r.Id == msg.Id);
            //ReadModel2Db.Should().ContainsReadModel<TestReadModel3>(r => r.Id == msg.Id);
        }
    }
}
