# EventStoreKit #

EventStoreKit is a library, which provide various tool to work with EventSourcing / CQRS projects.
And the goal is to combine several principles, which are not combined easily: 

* Easy start. 

One can start to code/prototyping with single small piece of code, even without any extension. And this code will work.
See the minimalystic samples:

- Dummy
- [Northwind.Console](https://github.com/svjatpro/EventStoreKit/tree/2.0.0.x/_samples/Northwind.Console) - Simple console example, based on Northwind domain model

* Modularity.

* To be a library, and not to be a framework


The goal of this libraries is to combine low-entry barier to start new project, and 
Easy start to make project prototypes or experiment with EventSourcing


## Let's get started

* First we need to install the core package

From **NuGet**:
`Install-Package EventStoreKit.Core`

* Lets create simplest application, kind of event sourcing 'Hello World'

we need an aggregate class, which raises the events, when handle appropriate commands:
```cs
class Aggregate1 : AggregateBase,
    ICommandHandler<CreateCommand1>,
    ICommandHandler<RenameCommand1>
{
    private string Name;

    private bool ValidateName( string name ){ ... }

    public Aggregate1( Guid id )
    {
        Id = id;
        Register<CreatedEvent1>( message => {} );
        Register<RenamedEvent1>( message => { Name = message.Name; } );
    }
    public void Handle( CreateCommand1 cmd )
    {
        Raise( new CreatedEvent1{ Id = id } );
        Raise( new RenamedEvent1{ Id = id, Name = cmd.Name } );
    }
    public void Handle( RenameCommand1 cmd )
    {
        if( cmd.Name == Name )
            return;
        if( !ValidateName( cmd.Name ) )
            throw new InvalidNameException( ... );

        Raise( new RenamedEvent1{ Id = id, Name = cmd.Name } );
    }
}

class CreateCommand1 : DomainCommand 
{ 
}
class RenameCommand1 : DomainCommand 
{ 
    public string Name { get; set; }
}
class CreatedEvent1 : DomainEvent 
{
}
class RenamedEvent1 : DomainEvent 
{
    public string Name { get; set; }
}
```

Then we need a projection, which receives events and holds current state of all entities, associated with aggregate class: 
```cs
class Projection1 : SqlProjection<ReadModel1>,
    IEventHandler<CreatedEvent1>,
    IEventHandler<RenamedEvent1>
{
    public Handle( CreatedEvent1 message )
    {
        DbProviderFactory.Run( db => db.Insert( new ReadModel1{ Id = message.Id } ) ) );
    }

    public Handle( RenamedEvent1 message )
    {
        DbProviderFactory.Run( db => db.Update<ReadModel1>( 
            model => model.Id == message.Id, 
            model => new ReadModel1{ Name = message.Name } ) ) );
    }

    public List<ReadModel1> GetAll()
    {
        return DbProviderFactory.Run( db => db.Query<ReadModel1>().ToList<>() );
    }
}

class ReadModel1
{
    public Guid Id { get; set; }
    public string Name { get; set; }
}
```

Initialize Mediator Service: 
```cs
var service = new EventStoreKitService()
    .RegisterAggregate<Aggregate1>()
    .RegisterSubscriber<Projection1>();
```

Now we can start to use our 'application', 
lets send several commands
```cs
service.SendCommand( new CreateCommand1{ id = ( id1 = Guid.NewGuid() ), Name = 'name1' } );
service.SendCommand( new CreateCommand1{ id = Guid.NewGuid(), Name = 'name2' } );
service.SendCommand( new CreateCommand1{ id = id1, Name = 'name1 updated' } );
```
now get the projection instance,
```cs
projection = service.GetSubscriber<Projection1>();
```
wait until it handle all events in the queue, and get the current state of all entities:
```cs
projection.QueuedMessages().Wait();
projection.GetAll().ForEach( model => Console.WriteLine( model.Name ) );
```

## Configure DataBase

## Integrate with IoC containers

## Configure logging

EventStoreKit Extensions

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

* Lets start with simple example.

First we need to create saga class, inherited from (EventStoreKit) SagaBase; 
and signup to required events/commands:
```cs
private class Saga1 : SagaBase,
    IEventHandler<TestEvent1>, 
    IEventHandler<TestEvent2>, 
    ICommandHandler<SagaCommand1>
{
    public Saga1( string id )
    { 
        Id = id; // Id initialization is required, otherwise saga can't be saved
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
```

Then we need to register saga class in mediator service:
```cs
// register
Service
    ... 
    .RegisterSaga<Saga1>()
    ... 
    .Initialize();
```
If saga registered in this minimalistic way, then each time the one of subscribed message published, 
saga with id = "{SagaType.Name}_{message.Id}" is instantiated from stored events, process message, and save the message in saga stream.

* Instantiation of Saga can be customized. 

If saga have to be instantiated with custom constructor, like this: 
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
```

Then it should be registered with appropriate factory method:
```cs
Service
    ... 
    .RegisterSaga<Saga1>( 
        sagaFactory: 
            ( service, sagaId ) => 
            new Saga1( sagaId, service.GetSubscriber<ISomeProjection1>(), externalService ) )
    ... 
    .Initialize();
```

* Custom saga id mapping.

By default sagaId generated by following pattern "{SagaClass.Name}_{message.Id}".
It is not what we need, if saga covers several entities, related by some parent object, or whatever logic, which goes beyond the single entity.
In this case we need to define saga id pattern for each message, which handled by saga:
```cs
Service
    ... 
    .RegisterSaga<Saga1>( 
        sagaIdResolve: SagaId
            .From<TestEvent1>( msg => $"Saga1_{msg.AnotherId}" )
            .From<TestEvent2>( msg => $"Saga1_{msg.AnotherId}" ), )
    ... 
    .Initialize();
```

* Transient message handling.

Transient handling means that message will processed by saga, but not saved in saga's stream.
You can mix both style in single saga:
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

This can be usefull for sagas with long lifecicle and a lot of messages, stored in saga's stream, 
in this way, first time ( after service recycling ) saga handle new message it instantiated in regular way, 
and then instance is reused for all further messages. All processed messages stored in saga's stream in regular way, so 
the cached instance can be recreated at any time.
```cs
Service
    ... 
    .RegisterSaga<Saga1>( cached : true )
    ... 
    .Initialize();
```