# Initialize Autofac container #

NeventStoreKit.Autofac extention module allows to initialize event store infrastructure through the autofac container as a whole project components initialization,
so you can not to use EventStoreService directly.

## Lets create example northwind autofac module

```cs
public class NorthwindModule : Module
{
    protected override void Load( ContainerBuilder builder )
    {
        // register command handlers in different ways
        builder.RegisterType<CustomerHandler>(); // register class is enough, all mapped handlers will be extracted automatically
        builder.RegisterType<ProductHandler>().AsImplementedInterfaces(); // register implemented interfaces directly is Ok, bu not required
        builder.RegisterType<OrderHandler>().SingleInstance(); // register as single instance
        builder.RegisterType<OrderDetailHandler>().ExternallyOwned(); // register as short live object, created for each handled command

        // register events subscribers in different ways
        builder.RegisterType<ProductProjection>().AsImplementedInterfaces();  // register implemented interfaces directly is Ok, but not required
        builder.RegisterType<CustomerProjection>().SingleInstance(); // register as single instance
        builder.RegisterType<OrderProjection>().ExternallyOwned(); // register as short live object, created for each handled command
        builder.Register( ctx => new OrderDetailProjection( 
            ctx.Resolve<IEventStoreSubscriberContext>(),
            ctx.Resolve<CustomArg>(), ... ) );

        // configure data base
        builder
            .RegisterInstance( new DataBaseConfiguration( DataBaseConnectionType.SqlLite, "data source=northwind" ) )
            .As<IDataBaseConfiguration>()
            .SingleInstance();
        // configure Data base Provider Factory ( default provider is primitive in-memory, so it is recomended to use linq2db or custom provider )
        builder
            .RegisterType<Linq2DbProviderFactory>()
            .As<IDbProviderFactory>()
            .SingleInstance();
    }
}
```

Then, during creating ContainerBuilder there should be additional method called: builder.InitializeEventStoreKitService()
```cs
var builder = new ContainerBuilder(); // create container builder
... 
builder.RegisterModule<NorthwindModule>(); // register northwind module and other types
builder.InitializeEventStoreKitService(); // call the initialization method, which will initalize EventStoreKitService through the container builder

var container = builder.Build();
...

```
This method receive all required components, register in container, initialize the service and register additional stuff in container
## Components, registered automatically in container
- IEventStoreKitService itself
```cs 
var service = container.Resolve<IEventStoreKitService>();
```
- ... 

## Lifetime for command handlers and event subscribers


## Database configuring via container

To configure EventStore and subscribers to single DataBase:
```cs
public class NorthwindModule : Module
{
    protected override void Load( ContainerBuilder builder )
    {
        // ....         
        builder.RegisterType<Linq2DbProviderFactory>().As<IDbProviderFactory>();
        builder.Register( ctx => DataBaseConfiguration.Initialize( DbConnectionType.MsSql2012, connectionString ) )
            .As<IDataBaseConfiguration>()
            .ExternallyOwned();
    }
}
```

