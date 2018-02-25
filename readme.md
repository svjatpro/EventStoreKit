# EventStoreKit #

EventStoreKit is a library, which provide various tool to work with EventSourcing / CQRS projects.

## The goal of this libraries is to combine low-entry barier to start new project, and 

## Easy start to make project prototypes or experiment with EventSourcing

* small code examples, registration, and use of stuff, probably dummy code

## Easy migration to another technology

* there are various extentions, like
* switch to another IOC
* switch to another DbProvider
* switch to log4net

## Let's get started

From **NuGet**:
* `Install-Package EventStoreKit`

## EventStoreKit Extensions ##

- [EventStoreKit.linq2db](https://github.com/svjatpro/EventStoreKit/tree/2.0.0.x/EventStoreKit.linq2db) - Implementation of IDataBaseProvider based on [linq2db](https://github.com/linq2db/linq2db)


## Waiting for message processing by subscribers ##

Sometimes it's necessary to make sure that the subscriber has processed the message.
suppose you are creating simple web application, and decided to use sync model instead of CQRS
in this way, the controller method have to looks like this


```cs
service.SendCommand( new Command1{ Id = id } );
subscriber.QueuedMessages().Wait();  // Wait until all messages, which are in subscriber queue will be processed

```

```cs
service.SendCommand( new Command1{ Id = id } );
subscriber.When<Message1>( msg => msg.Id == id ).Wait();  // Wait for message processed by subscriber

```

```cs
service.SendCommand( new RequestCommand{ Id = id } );
subscriber
    .When( MessageMatch
        .Is<ConfirmedMessage>( msg => msg.Id == id )
        .BreakBy<RejectedMessage>( msg => msg.Id == id ) )
    .Wait();  // Wait for message processed by subscriber
```
