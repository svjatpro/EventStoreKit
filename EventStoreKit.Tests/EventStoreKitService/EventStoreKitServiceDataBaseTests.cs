using System;
using System.Linq;
using System.Reactive.Concurrency;
using System.Threading;
using EventStoreKit.DbProviders;
using EventStoreKit.Handler;
using EventStoreKit.linq2db;
using EventStoreKit.Logging;
using EventStoreKit.Messages;
using EventStoreKit.Projections;
using EventStoreKit.Services;
using EventStoreKit.Services.Configuration;
using EventStoreKit.Utility;
using FluentAssertions;
using NUnit.Framework;

namespace EventStoreKit.Tests
{
    [TestFixture]
    public class EventStoreKitServiceDataBaseTests
    {
        #region Private members

        private EventStoreKitService Service;

        private class TestReadModel1
        {
            public string Name { get; set; }
        }

        private class TestEvent1 : DomainEvent
        {
            public string Name { get; set; }
        }

        private class Subscriber : SqlProjectionBase<TestReadModel1>, 
            IEventHandler<TestEvent1>
        {
            public Subscriber( IEventStoreSubscriberContext context ) : base( context ){}
            
            public void Handle( TestEvent1 message )
            {
                DbProviderFactory.Run( db => db.Insert( message.CopyTo( m => new TestReadModel1() ) ) );
            }
        }

        [SetUp]
        protected void Setup()
        {
            Service = new EventStoreKitService();
        }

        #endregion

        [Test]
        public void EventStoreShouldBeMappedToSingleDbByEventStore()
        {
            Service.SetEventStoreDataBase<Linq2DbProviderFactory>( DbConnectionType.SqlLite, "data source=db1" );
            Service.ResolveDbProviderFactory<Commits>().Run( db => db.Delete<Commits>( c => true ) );

            var id = Guid.NewGuid();
            Service.Raise( new TestEvent1{ Id = id } );
            Thread.Sleep( 100 );

            var commits = Service.ResolveDbProviderFactory<Commits>().Run( db => db.Query<Commits>().ToList() );
            commits.Count.Should().Be( 1 );
            commits[0].StreamIdOriginal.Should().Be( id.ToString() );
        }
    }
}
