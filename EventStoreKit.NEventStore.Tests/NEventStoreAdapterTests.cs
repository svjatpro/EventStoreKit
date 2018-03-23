using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EventStoreKit.Core.EventStore;
using EventStoreKit.DbProviders;
using EventStoreKit.Services;
using NUnit.Framework;
using NUnit.Framework.Internal;

namespace EventStoreKit.NEventStore.Tests
{
    [TestFixture]
    public class NEventStoreAdapterTests
    {
        [Test]
        public void Test1()
        {
            var adapter = new NEventStoreAdapter();

            adapter.MessagePublished += (o, msg) =>
            {
                Console.WriteLine( msg.Message.Id );
            };


        }
    }
}
