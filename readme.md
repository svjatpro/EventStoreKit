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

- [EventStoreKit.linq2db](https://github.com/svjatpro/EventStoreKit/tree/2.0.0.x/EventStoreKit.linq2db) - Implementation of IDataBaseProvider and IDataBaseProviderFactory based on [linq2db](https://github.com/linq2db/linq2db)
- [EventStoreKit.log4net](https://github.com/svjatpro/EventStoreKit/tree/2.0.0.x/EventStoreKit.log4net) - Implementation of ILogger and ILoggerFactory based on [log4net](https://github.com/apache/logging-log4net)
- [EventStoreKit.Autofac](https://github.com/svjatpro/EventStoreKit/tree/2.0.0.x/EventStoreKit.Autofac) - Integration with [Autofac](https://github.com/autofac/Autofac)


## Waiting for message processing by subscribers

Sometimes it's necessary to make sure that the subscriber has processed the message you send or messages, coused by command you sent.
There are following extension methods to do this:

* Task QueuedMessages( this IEventSubscriber subscriber )
Wait until all messages, which are in subscriber queue at the moment will be processed
```cs
var task = subscriber.QueuedMessages();
service.SendCommand( new Command1{ Id = id } );
task.Wait();
```
* Task<TMessage> When( this IEventSubscriber subscriber, Func<TMessage,bool> predicat )
Wait for message processed by subscriber, detected by simple expression
```cs
var task = subscriber.When<Message1>( msg => msg.Id == id );
service.SendCommand( new Command1{ Id = id } );
task.Wait();
```

* Task<List<TMessage>> When( this IEventSubscriber subscriber, MessageMatch match )
Wait for messages processed by subscriber, detected by complex rules
return all matched messages
```cs
try
{
    var task = subscriber
        .When( MessageMatch
            .Is<ConfirmedMessage1>( msg => msg.Id == id )
            .And<ConfirmedMessage2>( msg => msg.Id == id )
            .Ordered() // default option is unordered processing
            .BreakBy<RejectedMessage1>( msg => msg.Id == id )
            .BreakBy<RejectedMessage2>( msg => { if( msg.Id == id ) throw new Exception1(); } ) )
    service.SendCommand( new RequestCommand{ Id = id } );
    task.Wait();
}
catch( Exception1 exception )
{
    ...
}
```

There are some cases, when this methods could be usefull:

* Unit tests
The most common usage is unit tests, one can easily test async scenarios, like this:
```cs
[Test]
{    
    subscriber1.Handle( new Event1{ Id = id } );
    subscriber1.QueuedMessages().Wait();

    var result = subscriber1.GetModels();
    result[0].Id.Should.Be( id );
    result[0].CalculatedField.Should.Be( ... );
    ...
}
```
* Sync wrappers for async scenarios
One can create sync api, which implemented by embedded async event sourcing schema, 
so the client even may not aware about it async or event sourcing nature and use it as sync API

```cs
class UserService
{
    public UserDto Create( string login, string email, string first, ... )
    {
        var id = Guid.NewGuid();
        var taskCreate = UserProjection
            .When( MessageMatch
                .Is<UserCreatedEvent>( msg => msg.Id == id )
                .And<UserPasswordUpdatedEvent>( msg => msg.Id == id )
                .Ordered()
                .BreakBy<UserCreateRejectedEvent>(
                    msg => 
                    {
                        if( msg.Id == id )
                            throw new UserCreationRejectedException( id, login, msg.Reason );
                    } ) )
            .ContinueWith( create => 
            {
                if( create.IsFaulted )
                    throw create.Exception;
                var user = UserProjection.GetById( id );
                return user;
            } );

        commandBus.SendCommand( new RequestToCreateUser{ id, login, email, ... } );
        taskCreate.Wait();
        return taskCreate.Result();
    }
}
```

* Sync Web API
Suppose you are creating simple web application, and decided to use sync model instead of CQRS
in this way, the controller method have to looks like this:

```cs
...
public HttpResponseMessage Post( CreateUserRequest user )
{
    try
    {
        var id = Guid.NewGuid();
        var taskCreate = UserProjection
            .When( MessageMatch
                .Is<UserCreatedEvent>( msg => msg.Id == id )
                .And<UserPasswordUpdatedEvent>( msg => msg.Id == id )
                .Ordered()
                .BreakBy<UserCreateRejectedEvent>(
                    msg => 
                    {
                        if( msg.Id == id )
                            throw new UserCreationRejectedException( id, login, msg.Reason );
                    } ) )
            .ContinueWith( create => 
            {
                if( create.IsFaulted )
                    throw create.Exception;
                var user = UserProjection.GetById( id );
                return user;
            } );

        commandBus.SendCommand( new RequestToCreateUser{ id, login, email, ... } );
        taskCreate.Wait();

        return Request.CreateResponse( HttpStatusCode.OK, new { taskCreate.Result } );
    }
    catch( UserCreationRejectedException exception )
    {
        return Request.CreateErrorResponse( HttpStatusCode.Forbidden, new { Message = exception.Message } );
    }
}
```
Please note, that it is recomended to use this methods for short live scenarios only, to not reduce performance.
