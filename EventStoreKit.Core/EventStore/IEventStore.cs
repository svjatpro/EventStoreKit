using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EventStoreKit.DbProviders;
using EventStoreKit.Logging;
using EventStoreKit.Messages;
using EventStoreKit.Projections;
using EventStoreKit.Services;
using EventStoreKit.Utility;
using NEventStore;
using NEventStore.Dispatcher;
using NEventStore.Persistence.Sql;
using NEventStore.Persistence.Sql.SqlDialects;

namespace EventStoreKit.Core.EventStore
{
    // message interfaces - no need
    // basic class Message - no need for local stuff, dispatcher can be splitted to command / events

    // #1. send command
    // #2. saga process event
    // #3. projection process event

    // save event:
    //  domain: just an event, it will be serialized, get all meta properties from domain event itself
    //  saga: streamId, domain event

    //public interface IMessage
    //{
    //    DateTime Created { get; }
    //    Guid EventId { get; }
    //}
    public interface IEvent
    {
        DateTime Created { get; }
        Guid EventId { get; }
    }
    public interface ICommand
    {
        
    }


    public class MessageEventArgs : EventArgs
    {
        public readonly Message Message;

        public MessageEventArgs(Message message)
        {
            Message = message;
        }
    }

    public interface IEventStore
    {
        event EventHandler<MessageEventArgs> MessagePublished;

        void AppendToStream( Message message );

        // void SubscribeForAll();
        // void SubscribeForStream();
    
        // void DeleteStream( string streamId )
        // void DeleteMessage( string streamId, Guid messageId )
    }

    public class NEventStoreAdapter : IEventStore
    {
        private readonly IStoreEvents StoreEvents;
        
        public NEventStoreAdapter( Wireup wireup = null, IDataBaseConfiguration configuration = null, ILoggerFactory loggerFactory = null )
        {
            if( wireup == null )
            {
                wireup = Wireup.Init();
                if (configuration == null)
                    configuration = new DataBaseConfiguration(DataBaseConnectionType.None, null);
            }

            if ( configuration != null )
            {
                if ( configuration.DataBaseConnectionType == DataBaseConnectionType.None )
                {
                    wireup = wireup.UsingInMemoryPersistence();
                }
                else
                {
                    var persistanceWireup =
                        configuration.ConfigurationString != null ? 
                        wireup.UsingSqlPersistence( configuration.ConfigurationString ) : 
                        wireup.UsingSqlPersistence( null, configuration.ConnectionProviderName, configuration.ConnectionString );

                    var dialectTypeMap = new Dictionary<DataBaseConnectionType, Type>
                    {
                        {DataBaseConnectionType.MsSql2000, typeof(MsSqlDialect)},
                        {DataBaseConnectionType.MsSql2005, typeof(MsSqlDialect)},
                        {DataBaseConnectionType.MsSql2008, typeof(MsSqlDialect)},
                        {DataBaseConnectionType.MsSql2012, typeof(MsSqlDialect)},
                        {DataBaseConnectionType.MySql, typeof(MySqlDialect)},
                        {DataBaseConnectionType.SqlLite, typeof(SqliteDialect)}
                    };
                    wireup = persistanceWireup
                        .WithDialect( (ISqlDialect) Activator.CreateInstance( dialectTypeMap[configuration.DataBaseConnectionType] ) )
                        .PageEvery( 1024 )
                        .InitializeStorageEngine()
                        .UsingJsonSerialization();
                }
            }

            if ( loggerFactory != null )
            {
                wireup = wireup.LogTo( type => loggerFactory.Create<NEventStoreAdapter>() );
            }

            StoreEvents = new AsynchronousDispatchSchedulerWireup(
                    wireup,
                    new DelegateMessageDispatcher( DispatchCommit ),
                    DispatcherSchedulerStartup.Auto )
                .UsingEventUpconversion()
                //.WithConvertersFrom( AppDomain.CurrentDomain.GetAssemblies() /*.Where( a => a.FullName.StartsWith( "Code.CL.Domain" ) )*/.ToArray() )
                .Build();
        }

        private void DispatchCommit( ICommit commit )
        {
            foreach ( var message in commit.Events )
            {
                message.ProcessEvent( e =>
                {
                    e.Version = commit.StreamRevision;
                    e.BucketId = commit.BucketId;
                    e.CheckpointToken = commit.CheckpointToken;

                    MessagePublished.Execute( this, new MessageEventArgs( e ) );
                } );
            }
        }

        public event EventHandler<MessageEventArgs> MessagePublished;

        public void AppendToStream(Message message)
        {
            using(var stream = StoreEvents.CreateStream(message.Id))
            {
                stream.Add(new EventMessage { Body=message });
                stream.CommitChanges(Guid.NewGuid());
            }
        }
    }

    public class DomainHub // todo: rename it
    {
        public DomainHub()
        {
            
        }
    }
}
