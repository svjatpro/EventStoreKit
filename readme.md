# EventStoreKit #

EventStoreKit is a library, which provide various tool to work with EventSourcing / CQRS projects.

## The goal of this libraries is to combine low-entry barier to start new project, and 

## Easy start to make project prototypes or experiment with EventSourcing

* small code examples, registration, and use of stuff, probably dummy code

## Let's get started

From **NuGet**:
* `Install-Package EventStoreKit.Core`

* Create EventStoreKit service
```cs
var service = new EventStoreKitService()
    .Initialize();
```

* Create domain classes
```cs
class Aggregate1 : AggregateBase,
    ICommandHandler<Command1>
{
    public Aggregate1( Guid id )
    {
        Id = id;
        Register<Event1>( Apply );
    }
    public void Handle( Command1 )
    {
        Raise( new Event1{ Id = id, ... } );
    }
}

service = new EventStoreKitService()
    .RegisterAggregateCommandHandler<Aggregate1>()
    .Initialize();
```

* Send command
```cs
service.SendCommand( new Command1{...} );
```

* Subscribe for events
```cs
public class Projection1 : SqlProjection,
    IEventHandler<Event1>
{
    public Handle( Event1 message )
    {
    }
}

service = new EventStoreKitServie()
    .RegisterAggregateCommandHandler<Aggregate1>()
    .RegisterEventSubscriber<Projection1>()
    .Initialize();

projection = service.GetSubscriber<Projection1>();

```




## EventStoreKit Extensions ##

Easy migration to another technology

- [EventStoreKit.linq2db](https://github.com/svjatpro/EventStoreKit/tree/2.0.0.x/EventStoreKit.linq2db) - Implementation of IDataBaseProvider and IDataBaseProviderFactory based on [linq2db](https://github.com/linq2db/linq2db)
- [EventStoreKit.log4net](https://github.com/svjatpro/EventStoreKit/tree/2.0.0.x/EventStoreKit.log4net) - Implementation of ILogger and ILoggerFactory based on [log4net](https://github.com/apache/logging-log4net)
- [EventStoreKit.Autofac](https://github.com/svjatpro/EventStoreKit/tree/2.0.0.x/EventStoreKit.Autofac) - Integration with [Autofac](https://github.com/autofac/Autofac)


## Command Handlers

First approach is to map commands to aggregate method

```cs
class Aggregate1 : AggregateBase,
    ICommandHandler<Command1>,
    ICommandHandler<Command2>
{
    public Aggregate1( Guid id ) { ... }

    public void Handle( Command1 command )
    {
        Raise( new Event1{} );
    }

    public void Handle( Command2 command )
    {
        if( ... )
            throw new InvalidOperationException();
        Raise( new Event1{} );
    }
}

// register aggregate with service on the application start
service.RegisterAggregateCommandHandler<Aggregate1>();

// and then all sent commands will be handled by appropriate method of aggregate, initialized by EventStore
service.SendCommand( new Command1{...} );
```

Second approach is to map commands to external command handler class,
use this approach if handler can call more than one aggregate methods, depends on command content,
or if there is complex validation logic, which can be applyed before aggregate creation
```cs
class Aggregate1 : AggregateBase
{
    public Aggregate1( Guid id ) { ... }
    public Aggregate1( CreateCommand1 cmd ) 
    {
        Raise( new CreatedEvent{} );
    }

    public void Method2( Command2 command )
    {
        Raise( new Event2{} );
    }
}
class AggregateHandler : 
    ICommandHandler<CreateCommand1, Aggregate1>,
    ICommandHandler<Command2, Aggregate1>
{
    public void Handle( CreateCommand1 command, CommandHandlerContext<Aggregate1> context )
    {
        context.Entity = new Aggregate1( command );
    }

    public void Handle( Command2 command, CommandHandlerContext<Aggregate1> context )
    {
        if( cmd... ) // in this case aggregate is not created
            throw new InvalidOperationException();

        if( cmd... )
            context.Entity.Method1();

        if( cmd... )
            context.Entity.Method2();
    }
}

// register aggregate handler with service on the application start
service.RegisterCommandHandler<AggregateHandler>();

// and then all sent commands will be handled by appropriate method of aggregate, initialized by EventStore
service.SendCommand( new Command1{...} );
```



## Waiting for message processing by subscribers

Sometimes it's necessary to make sure that the subscriber has processed the message you send 
or messages, coused by command you sent.
There are following extension methods to do this:

*  QueuedMessages( this IEventSubscriber subscriber )

Wait until all messages, which are in subscriber queue at the moment will be processed
```cs
service.SendCommand( new Command1{ Id = id } );
subscriber.QueuedMessages().Wait();
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

## ICurrentUserProvider

Used to initialize published messages with UserId, to track initiators of the events

```cs
// example of custom user provider
class CurrentUserProvider : ICurrentUserIdProvider
{
    public CurrentUserId
    {
        get
        {
            var login = Thread.CurrentPrincipal.Identity.Name.With( n => n.Trim().ToLower() );
            if ( string.IsNullOrWhiteSpace( login ) )
                return null;
            return YourUserProjection.GetByLogin( login ).With( u => u.UserId );
        }
    }
}
```

## Sagas

* simple saga example

```cs
// create saga class
private class Saga1 : SagaBase,
    IEventHandler<TestEvent1>, 
    IEventHandler<TestEvent2>, 
    ICommandHandler<SagaCommand1>
{
    public Saga1( string id )
    { 
        Id = id; 
    }

    public void Handle( TestEvent1 message )
    {
        Dispatch( new TestCommand2 { Id = message.Id, ... } );
    }

    public void Handle( TestEvent2 message )
    {
        Dispatch( new TestCommand3 { Id = message.Id, ... } );
    }

    public void Handle( SagaCommand1 command )
    {
        Dispatch( new TestCommand4 { Id = command.Id } );
    }
}

// register
Service
    ... 
    .RegisterSaga<Saga1>()
    ... 
    .Initialize();
```

* Custom factory for saga

```cs
private class Saga1 : SagaBase,
    IEventHandler<TestEvent1>,
    IEventHandler<TestEvent2>
{
    public Saga1( string id, ISomeProjection1 projection1, IExternalService service1 )
    { 
        Id = id; 
        ... 
    }

    public void Handle( TestEvent1 message )
    {
        Projection1
            .GetTargetModels( ... )
            .ForEach( model => 
            {
                Dispatch( new TestCommand2 { Id = message.Id, Some } );
            } );         
    }
    ... 
}

Service
    ... 
    .RegisterSaga<Saga1>( 
        sagaFactory: 
            ( service, sagaId ) => 
            new Saga1( sagaId, service.GetSubscriber<ISomeProjection1>(), externalService ) )
    ... 
    .Initialize();
```

* Custom saga id mapping

By default sagaId generated by following pattern "{SagaClassName}_{message.Id}".
To use another patterns, one can configure patterns for each message type on saga registration

```cs
private class Saga1 : SagaBase,
    IEventHandler<TestEvent1>,
    IEventHandler<TestEvent2>
{
    public Saga1( string id ) { Id = id; }

    public void Handle( TestEvent1 message )
    {
        Dispatch( new TestCommand2 { Id = message.Id, Some } );
    }
    ... 
}

Service
    ... 
    .RegisterSaga<Saga1>( 
        sagaIdResolve: SagaId
            .From<TestEvent1>( msg => $"Saga1_{msg.AnotherId}" )
            .From<TestEvent2>( msg => $"Saga1_{msg.AnotherId}" ), )
    ... 
    .Initialize();
```

* Transien message handling

Transiend handling means, that message will not saved in saga's stream. 
You can mix both messages in single saga. 

```cs
private class Saga1 : SagaBase,
    IEventHandler<TestEvent1>,
    IEventHandler<TestEvent2>, 
    IEventHandlerTransient<TestEvent3>
{
    public Saga1( string id ) { Id = id; }

    public void Handle( TestEvent1 message )
    {
        Dispatch( new TestCommand2 { Id = message.Id, Some } );
    }
    ... 
    public void Handle( TestEvent3 message )
    {
        Dispatch( new TestCommand2 { Id = message.Id, Some } );
    }
}
```

* Saga instances can be Cached. 

this can be usefull for sagas with long lifecicle.

```cs
Service
    ... 
    .RegisterSaga<Saga1>( cached : true )
    ... 
    .Initialize();
```