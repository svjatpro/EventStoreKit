using System;
using System.Reflection;
using Autofac;
using EventStoreKit.CommandBus;
using EventStoreKit.Example;
using EventStoreKit.linq2db;
using EventStoreKit.Northwind.Messages.Commands;
using EventStoreKit.Services;

namespace EventStoreKit.Northwind.Console
{
    class Program
    {
        static void Main( string[] args )
        {
            const string dbConfig = "NorthwindDb";
            log4net.Config.XmlConfigurator.Configure();

            var builder = new ContainerBuilder();
            builder.RegisterModule( new EventStoreModule( DbProviderFactory.SqlDialectType( dbConfig ), configurationString: dbConfig ) );
            builder.RegisterModule( new NorthwindModule() );
            builder.RegisterType<CurrentUserProviderStub>().As<ICurrentUserProvider>().SingleInstance();
            var container = builder.Build();
            
            var commandBus = container.Resolve<ICommandBus>();

            commandBus.Send( new CreateCustomerCommand
            {
                Id = Guid.NewGuid(),
                CompanyName = "company1",
                ContactName = "contact1",
                ContactTitle = "contacttitle1",
                ContactPhone = "contactphone",
                Address = "address",
                City = "city",
                Country = "country",
                Region = "region",
                PostalCode = "zip"
            } );


            IDbProviderFactory
            {
                IDbProvider Create();
                IDbProvider Create<ModelType>();
            }
            DbProviderFactory
            {
                // constructors receive default connection string, or if everything exist in single Db, then this is all we need
                public DbProviderFactory( string configString ){}
                public DbProviderFactory( SqlType sqlType, string connectionString ){}

                // if we have several data bases, then we need additionaly map each ( or primary ) model to appropriate DataBase
                public DbProviderFactory MapModel<ModelType>( string configString ){}
                public DbProviderFactory MapModel<ModelType>( SqlType sqlType, string connectionString ){}
            }

            builder
                .Register( c => 
                    new DbProviderFactory( projectionConfig )
                      .MapModel<Commits>( commitsConfig ) )
                .As<IDbProviderFactory>()
                .SingleInstance();

            // just default provider ( projections ) - register in Autofac module
            builder
                .Register( c => c.Resolve<IDbProviderFactory>().Create() )
                .As<IDbProvider>().ExternallyOwned();

            abstract class SqlProjectionBase
            {
                public SqlProjectionBase( Func<IDbProvider> DbProviderFactory ){}
            }
            abstract class SqlProjectionBase<TModel>
            {
                public SqlProjectionBase( IDbProviderFactory DbProviderFactory )
                    : base( () => DbProviderFactory.Create<TModel>() )
                    {}
            }

        }
    }
}
