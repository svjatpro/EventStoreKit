using NUnit.Framework;

namespace EventStoreKit.Tests
{
    [TestFixture]
    public class EventStoreKitServiceContainerInitializerTests
    {
        #region Private members

        #endregion

        #region Default DataBase

        [Test]
        public void DefaultDbProviderFactorySetByServiceShouldBeAvailableThroughTheContainer()
        {
            // service.SetDataBase<Linq2DbProviderFactory>( new DataBaseConfiguration( DbConnectionType.SqlLite, "data source=db1" ) )

            // container.Resolve<IDataBaseConfiguration>() + keyed
            // container.Resolve<IEventStoreSubscriberContext>() + keyed
            // container.Resolve<IDataBaseProvider>() + keyed
            // container.Resolve<IDataBaseProviderFactory>() + keyed
        }

        [Test]
        public void DefaultDbProviderFactoryAndConfigurationSetByContainerShouldBeAvailableThroughTheService()
        {
            // builder.Register( new DataBaseConfiguration( DbConnectionType.SqlLite, "data source=db1" ) ).As<IDataBaseConfiguration>()
            // builder.RegisterType<Linq2DbProviderFactory>().As<IDataBaseProviderFactory>()

            // container.Resolve<IDataBaseConfiguration>() + keyed
            // container.Resolve<IEventStoreSubscriberContext>() + keyed
            // container.Resolve<IDataBaseProvider>() + keyed
            // container.Resolve<IDataBaseProviderFactory>() + keyed

            // server.GetDataBaseProviderFactory<TModel>()
            // server.GetDataBaseProviderFactory<Commit>()
        }

        [Test]
        public void DefaultDbProviderFactorySetByContainerShouldBeAvailableThroughTheService()
        {
            // builder.Register( ctx => new Linq2DbProviderFactory( new DataBaseConfiguration( DbConnectionType.SqlLite, "data source=db1" )  ) ).As<IDataBaseProviderFactory>()

            // container.Resolve<IDataBaseConfiguration>() + keyed - null / failed
            // container.Resolve<IEventStoreSubscriberContext>() + keyed
            // container.Resolve<IDataBaseProvider>() + keyed
            // container.Resolve<IDataBaseProviderFactory>() + keyed

            // server.GetDataBaseProviderFactory<TModel>()
            // server.GetDataBaseProviderFactory<Commit>()
        }

        #endregion

        #region Separate DataBase for EventStore and Subscribers

        [Test]
        public void ff()
        {
            //.SetEventStoreDataBase<Linq2DbProviderFactory>( new DataBaseConfiguration( DbConnectionType.SqlLite, "data source=db1" ) )
            //.SetSubscriberDataBase<Linq2DbProviderFactory>( new DataBaseConfiguration( DbConnectionType.SqlLite, "data source=db1" ) )

            //container.ResolveKeyed<IDataBaseProvider>( typeof( TSubscriber ) )
            //container.ResolveKeyed<IDataBaseProviderFactory>( typeof( TSubscriber ) )
            //container.ResolveKeyed<IDataBaseProvider>( typeof( TReadModel ) )
            //container.ResolveKeyed<IDataBaseProviderFactory>( typeof( TReadModel ) )
        }

        #endregion


        /*
        // separate DB for subscriber

        .RegisterEventSubscriber<TSubscriber>( new DataBaseConfiguration( DataBaseConnectionType.SqlLite, ConnectionStringDb3 ) );

        --

        // separate */

    }
}
