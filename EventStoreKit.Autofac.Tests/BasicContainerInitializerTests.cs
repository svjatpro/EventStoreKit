using System;
using Autofac;
using EventStoreKit.Autofac;
using EventStoreKit.Services;
using EventStoreKit.Utility;
using NUnit.Framework;

namespace EventStoreKit.Tests
{
    public class BasicContainerInitializerTests
    {
        protected ContainerBuilder Builder;
        protected IContainer Container;
        protected EventStoreKitService Service;

        protected void InitializeContainer( 
            Action<IEventStoreKitServiceBuilder> preInitialize = null, 
            Action<IComponentContext, IEventStoreKitServiceBuilder> initialize = null )
        {
            Builder.InitializeEventStoreKitService( preInitialize, initialize );
            Container = Builder.Build();
            Service = Container.Resolve<IEventStoreKitService>().OfType<EventStoreKitService>();
        }

        [SetUp]
        protected void SetupBasic()
        {
            Builder = new ContainerBuilder();
        }
    }
}
