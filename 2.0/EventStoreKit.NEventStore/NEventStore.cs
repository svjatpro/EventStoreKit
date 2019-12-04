using System;
using System.Collections.Generic;
using System.Text;
using EventStoreKit.Core.EventStore;
using NEventStore;

namespace EventStoreKit.NEventStore
{
    public class NEventStoreAdapter : IEventStore
    {
        //private readonly IIdGenerator IdGenerator;
        private readonly IStoreEvents StoreEvents;

        //public NEventStoreAdapter( Wireup wireup = null, IDataBaseConfiguration configuration = null, ILoggerFactory loggerFactory = null )
        //{
        //    IdGenerator = new SequentialIdgenerator();

        //    if ( wireup == null )
        //    {
        //        wireup = Wireup.Init();
        //        if ( configuration == null )
        //            configuration = new DataBaseConfiguration( DataBaseConnectionType.None, null );
        //    }

        //    if ( configuration != null )
        //    {
        //        if ( configuration.DataBaseConnectionType == DataBaseConnectionType.None )
        //        {
        //            wireup = wireup.UsingInMemoryPersistence();
        //        }
        //        else
        //        {
        //            var persistanceWireup =
        //                configuration.ConfigurationString != null ?
        //                wireup.UsingSqlPersistence( configuration.ConfigurationString ) :
        //                wireup.UsingSqlPersistence( null, configuration.ConnectionProviderName, configuration.ConnectionString );

        //            var dialectTypeMap = new Dictionary<DataBaseConnectionType, Type>
        //            {
        //                {DataBaseConnectionType.MsSql2000, typeof(MsSqlDialect)},
        //                {DataBaseConnectionType.MsSql2005, typeof(MsSqlDialect)},
        //                {DataBaseConnectionType.MsSql2008, typeof(MsSqlDialect)},
        //                {DataBaseConnectionType.MsSql2012, typeof(MsSqlDialect)},
        //                {DataBaseConnectionType.MySql, typeof(MySqlDialect)},
        //                {DataBaseConnectionType.SqlLite, typeof(SqliteDialect)}
        //            };
        //            wireup = persistanceWireup
        //                .WithDialect( (ISqlDialect) Activator.CreateInstance( dialectTypeMap[configuration.DataBaseConnectionType] ) )
        //                .PageEvery( 1024 )
        //                .InitializeStorageEngine()
        //                .UsingJsonSerialization();
        //        }
        //    }

        //    if ( loggerFactory != null )
        //    {
        //        wireup = wireup.LogTo( type => loggerFactory.Create<NEventStoreAdapter>() );
        //    }

        //    StoreEvents = new AsynchronousDispatchSchedulerWireup(
        //            wireup,
        //            new DelegateMessageDispatcher( DispatchCommit ),
        //            DispatcherSchedulerStartup.Auto )
        //        .UsingEventUpconversion()
        //        //.WithConvertersFrom( AppDomain.CurrentDomain.GetAssemblies() /*.Where( a => a.FullName.StartsWith( "Code.CL.Domain" ) )*/.ToArray() )
        //        .Build();
        //}

        //private void DispatchCommit( ICommit commit )
        //{
        //    foreach ( var message in commit.Events )
        //    {
        //        message.ProcessEvent( e =>
        //        {
        //            e.Version = commit.StreamRevision;
        //            e.BucketId = commit.BucketId;
        //            e.CheckpointToken = commit.CheckpointToken;

        //            //MessagePublished.Execute( this, new MessageEventArgs( e ) );
        //        } );
        //    }
        //}

        //public event EventHandler<MessageEventArgs> MessagePublished;

        public NEventStoreAdapter()
        {

        }

        public void AppendToStream( string streamId, IMessage message )
        {
            throw new NotImplementedException();
        }

        public void AppendToStream( string streamId, params IMessage[] messages )
        {
            throw new NotImplementedException();
        }
    }
}
