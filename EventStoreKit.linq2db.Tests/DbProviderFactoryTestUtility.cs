using System;
using System.Linq.Expressions;
using EventStoreKit.DbProviders;
using EventStoreKit.Utility;
using FluentAssertions;
using FluentAssertions.Primitives;

namespace EventStoreKit.Tests
{
    public static class DbProviderFactoryTestUtility
    {
        public static void NotContainsCommit( this ObjectAssertions factory, Guid id )
        {
            var result = factory.Subject
                .OfType<IDbProviderFactory>()
                .Run( db => db.SingleOrDefault<Commits>( c => c.StreamIdOriginal == id.ToString() ) );
            result.Should().BeNull();
        }
        public static void ContainsCommit( this ObjectAssertions factory, Guid id )
        {
            var result = factory.Subject
                .OfType<IDbProviderFactory>()
                .Run( db => db.SingleOrDefault<Commits>( c => c.StreamIdOriginal == id.ToString() ) );
            result.Should().NotBeNull();
        }
        public static void NotContainsReadModel<TReadModel>( this ObjectAssertions factory, Expression<Func<TReadModel, bool>> predicat ) where TReadModel : class
        {
            var result = factory.Subject
                .OfType<IDbProviderFactory>()
                .Run( db => db.SingleOrDefault( predicat ) );
            result.Should().BeNull();
        }
        public static void ContainsReadModel<TReadModel>( this ObjectAssertions factory, Expression<Func<TReadModel,bool>> predicat ) where TReadModel : class
        {
            var result = factory.Subject
                .OfType<IDbProviderFactory>()
                .Run( db => db.SingleOrDefault( predicat ) );
            result.Should().NotBeNull();
        }
    }
}