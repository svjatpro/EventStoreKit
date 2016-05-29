using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using EventStoreKit.Utility;

namespace EventStoreKit.Sql.ProjectionTemplates
{
    public class ObjectExpressionBuilder<TReadModel>
    {
        private readonly Expression<Func<TReadModel, TReadModel>> CustomUpdate;
        private readonly Dictionary<PropertyInfo, object> NewValues;

        public ObjectExpressionBuilder( Expression<Func<TReadModel,TReadModel>> customUpdate, Dictionary<PropertyInfo,object> newValues )
        {
            CustomUpdate = customUpdate;
            NewValues = newValues;
        }

        public Expression<Func<TReadModel, TReadModel>> GenerateUpdatExpression( bool updateAllFields )
        {
            var readmodelType = typeof( TReadModel );
            var sourceProperties = readmodelType.GetProperties( BindingFlags.Public | BindingFlags.Instance ).ToList();

            Expression<Func<TReadModel, TReadModel>> evaluator = null;

            var parameter = CustomUpdate.Return(
                expr => expr.Parameters[0],
                Expression.Parameter( readmodelType, "entity" ) );
            var customBindings = CustomUpdate
                .With( e => e.Body.OfType<MemberInitExpression>() )
                .With( m => m.Bindings )
                .Return( b => b.ToList(), new List<MemberBinding>() );



            if ( PropertiesMap.Any() )
            {
                var binders = customBindings.Union( PropertiesMap
                    .Where( p => p.Value.Validator( (TEvent) @event ) && !customBindings.Any( b => b.Member.Name == p.Key.Name ) )
                    .Select(
                        p =>
                        {
                            var customBinding = customBindings.SingleOrDefault( b => b.Member.Name == p.Key.Name );
                            var valueConst = Expression.Constant( p.Value.Getter( db, (TEvent) @event ), p.Key.PropertyType );
                            return customBinding ?? Expression.Bind( p.Key, valueConst );
                        } ) )
                    .ToList();
                var memberInit = Expression.MemberInit( ctor, binders );
                evaluator = Expression.Lambda<Func<TReadModel, TReadModel>>( memberInit, parameter );
            }

            return evaluator;
        }

        // 1 - custom binding
        // 2 - constants, taken from event via configured getters
        // 3 (?) - default, binding expression, get from e => new{ property = e.property }

        //var sourceType = typeof( TSource );
        //var destType = typeof( TDestination );
        //var sourceProperties = sourceType.GetProperties( BindingFlags.Public | BindingFlags.Instance ).ToList();
        //var destProperties = destType.GetProperties( BindingFlags.Public | BindingFlags.Instance ).ToList();
        //var customBindings = customAssigns
        //    .Body.OfType<MemberInitExpression>()
        //    .With( m => m.Bindings )
        //    .Return( b => b.ToList(), new List<MemberBinding>() );
        //var parameter = customAssigns.Parameters[0];
        //var bindings = destProperties
        //    .Select( destProp =>
        //    {
        //        var customBinding = customBindings.SingleOrDefault( b => b.Member.Name == destProp.Name );
        //        return
        //            customBinding ??
        //            sourceProperties
        //                .SingleOrDefault( p => p.Name == destProp.Name )
        //                .With( srcProp => Expression.Bind( destProp, Expression.MakeMemberAccess( parameter, srcProp ) ) );
        //    } )
        //    .Where( bind => bind != null )
        //    .ToList();
        //var ctor = Expression.New( destType );
        //var memberInit = Expression.MemberInit( ctor, bindings );
        //var lambda = Expression.Lambda<Func<TSource, TDestination>>( memberInit, parameter );
        //return lambda.Compile()( source );
    }
}
