using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using EventStoreKit.Utility;

namespace EventStoreKit.ProjectionTemplates
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
            var readmodelType = typeof (TReadModel);
            var sourceProperties = readmodelType
                .GetProperties( BindingFlags.Public | BindingFlags.Instance )
                .ToList();

            var parameter = CustomUpdate.Return(
                expr => expr.Parameters[0],
                Expression.Parameter( readmodelType, "entity" ) );
            var customBindings = CustomUpdate
                .With( e => e.Body.OfType<MemberInitExpression>() )
                .With( m => m.Bindings )
                .Return( b => b.ToList(), new List<MemberBinding>() );

            var binders = new List<MemberBinding>();
            sourceProperties.ForEach( property =>
            {
                var customBinding = customBindings.SingleOrDefault( b => b.Member.Name == property.Name );
                if ( customBinding != null )
                {
                    binders.Add( customBinding );
                }
                else if ( NewValues.ContainsKey( property ) )
                {
                    binders.Add( Expression.Bind( property,
                        Expression.Constant( NewValues[property], property.PropertyType ) ) );
                }
                else if ( updateAllFields )
                {
                    binders.Add( Expression.Bind( property, Expression.MakeMemberAccess( parameter, property ) ) );
                }
            } );

            if ( binders.Any() )
            {
                var ctor = Expression.New( readmodelType );
                var memberInit = Expression.MemberInit( ctor, binders );
                var evaluator = Expression.Lambda<Func<TReadModel, TReadModel>>( memberInit, parameter );

                return evaluator;
            }
            else
            {
                return null;
            }
        }
    }
}
