using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EventStoreKit.Core.EventStore;
using EventStoreKit.DbProviders;
using EventStoreKit.Messages;
using EventStoreKit.Services;
using NUnit.Framework;
using NUnit.Framework.Internal;

namespace EventStoreKit.NEventStore.Tests
{
    [TestFixture]
    public class NEventStoreAdapterTests
    {
        private class TestEvent1 : DomainEvent
        {
            
        }

        [Test]
        public void Test1()
        {
            var adapter = new NEventStoreAdapter();

            //adapter.MessagePublished += (o, msg) =>
            //{
            //    Console.WriteLine( msg.Message.Id );
            //};

            //adapter.AppendToStream( new TestEvent1 { Id = Guid.NewGuid() } );
            //adapter.AppendToStream( new TestEvent1 { Id = Guid.NewGuid() } );

            Thread.Sleep(1000);
        }
    }
}
