using System;
using System.Collections.Generic;

namespace EventStoreKit.Core.EventStore
{
    //public readonly string JsonData;
    //public readonly Guid MessageId;
    //public readonly string Type;
    //public readonly string JsonMetadata;
    //public NewStreamMessage( Guid messageId, string type, string jsonData, string jsonMetadata = null )
    //{
        //Ensure.That( messageId, "MessageId" ).IsNotEmpty();
        //Ensure.That( type, "type" ).IsNotNullOrEmpty();
        //Ensure.That( jsonData, "data" ).IsNotNullOrEmpty();

        //MessageId = messageId;
        //Type = type;
        //JsonData = jsonData;
        //JsonMetadata = jsonMetadata ?? string.Empty;
    //}
    
    
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
    //    Guid MessageId { get; }
    //}
    //public interface IEvent
    //{
    //    DateTime Created { get; }
    //    Guid MessageId { get; }
    //}
    //public interface ICommand
    //{

    //}


    public interface IMessage
    {
        string StreamId { get; }
        DateTime Created { get; }
        Guid MessageId { get; }
        //long EventNumber { get; }
    }
    public interface IDomainMessage : IMessage
    {
        int Version { get; }
        //Guid Id { get; }
        Guid CreatedBy { get; }
    }


    public class Message : IMessage
    {
        // string StreamId  // commit.StreamId
        // Guid MessageId ?
        // DateTime Created // commit.CommitStamp
        // long EventNumber ?

        public string StreamId { get; set; }
        public DateTime Created { get; set; }
        public Guid MessageId { get; set; }
        //public long EventNumber { get; set; }
    }
    
    public class DomainEvent : Message, IDomainMessage
    {
        public int Version { get; set; }
        //public Guid Id { get; set; }
        public Guid CreatedBy { get; set; }
    }

    public class DomainCommand : Message, IDomainMessage
    {
        public int Version { get; set; }
        public Guid Id { get; set; }
        public Guid CreatedBy { get; set; }
    }


    //public class MessageEventArgs : EventArgs
    //{
    //    public readonly IMessage Message;

    //    public MessageEventArgs( IMessage message )
    //    {
    //        Message = message;
    //    }
    //}

    public interface IEventStore
    {
        // 1. write to stream
        // 2. read from stream
        // 3. read all
        // 4. subscribe for events (pooling client)
        // 5. delete stream
        // 6. delete event

        //event EventHandler<MessageEventArgs> MessagePublished;

        //Task<AppendResult> AppendToStream(
        //    StreamId streamId,
        //    int expectedVersion,
        //    NewStreamMessage[] messages,
        //    CancellationToken cancellationToken = default );

        //void AppendToStream( string streamId, IMessage message );
        void AppendToStream( string streamId, params IMessage[] messages );
        
        // void SubscribeForAll();
        // void SubscribeForStream();
    
        // void DeleteStream( string streamId )
        // void DeleteMessage( string streamId, Guid messageId )
    }
    
}
