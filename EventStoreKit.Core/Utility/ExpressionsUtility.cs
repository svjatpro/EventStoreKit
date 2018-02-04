using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using EventStoreKit.SearchOptions;

namespace EventStoreKit.Utility
{
    public static class ExpressionsUtility
    {
        public static string GetPath<T>( this Expression<Func<T, object>> expr )
        {
            var stack = new Stack<string>();

            MemberExpression me;
            switch ( expr.Body.NodeType )
            {
                case ExpressionType.Convert:
                case ExpressionType.ConvertChecked:
                    var ue = expr.Body as UnaryExpression;
                    me = ( ( ue != null ) ? ue.Operand : null ) as MemberExpression;
                    break;
                default:
                    me = expr.Body as MemberExpression;
                    break;
            }

            while ( me != null )
            {
                stack.Push( me.Member.Name );
                me = me.Expression as MemberExpression;
            }
            return string.Join( ".", stack.ToArray() );
        }

        /// <summary>
        /// Get unique type key - for nullable type it will (generic type) + '?'
        /// </summary>
        public static string GetTypeKey( this Type type )
        {
            var typeKey =
                ( type.IsGenericType && type.GetGenericTypeDefinition() == typeof( Nullable<> ) ) ?
                type.GetGenericArguments()[0].Name + "?" :
                type.Name;
            return typeKey;
        }

        static public Dictionary<string, Expression<Func<T, object>>> WithProperty<T>( 
            this Dictionary<string, Expression<Func<T, object>>> dictionary, 
            Expression<Func<T,object>> propertyGetter, 
            Expression<Func<T,object>> sorter ) where T : class
        {
            var entity = (T)Activator.CreateInstance( typeof(T) );
            var propertyName = entity.GetPropertyName( propertyGetter ).ToLower();
            return dictionary.WithProperty( propertyName, sorter );
        }
        static public Dictionary<string, Expression<Func<T, object>>> WithProperty<T>(
            this Dictionary<string, Expression<Func<T, object>>> dictionary,
            string propertyName,
            Expression<Func<T, object>> sorter ) where T : class
        {
            if ( dictionary.ContainsKey( propertyName ) )
                dictionary.Remove( propertyName );
            dictionary.Add( propertyName, sorter );
            return dictionary;
        }

        static public Dictionary<string, Func<SearchFilterInfo, Expression<Func<T, bool>>>> WithProperty<T, TProp>(
            this Dictionary<string, Func<SearchFilterInfo, Expression<Func<T, bool>>>> dictionary,
            //Expression<Func<T, string>> propertyGetter,
            Expression<Func<T, TProp>> propertyGetter,
            Func<SearchFilterInfo, Expression<Func<T, bool>>> predicat ) where T : class
        {
            //var entity = (T)Activator.CreateInstance( typeof( T ) );
            //var propertyName = entity.GetPropertyName( propertyGetter ).ToLower();
            var propertyName = propertyGetter.GetPropertyName().ToLower();
            return dictionary.WithProperty( propertyName, predicat );
        }
        static public Dictionary<string, Func<SearchFilterInfo, Expression<Func<T, bool>>>> WithProperty<T>(
            this Dictionary<string, Func<SearchFilterInfo, Expression<Func<T, bool>>>> dictionary,
            string propertyName, Func<SearchFilterInfo, Expression<Func<T, bool>>> predicat ) where T : class
        {
            if ( dictionary.ContainsKey( propertyName ) )
                dictionary.Remove( propertyName );
            dictionary.Add( propertyName.ToLower(), predicat );
            return dictionary;
        }
        static public Dictionary<string, Func<SearchFilterInfo, Expression<Func<T, bool>>>> WithProperty<T,TProperty>(
            this Dictionary<string, Func<SearchFilterInfo, Expression<Func<T, bool>>>> dictionary,
            string propertyName, Expression<Func<T, TProperty>> getProperty ) where T : class
        {
            var property = typeof( T ).GetProperty( getProperty.GetPropertyName() );
            return dictionary.WithProperty( propertyName, property.GetFilterExpression<T>() );
        }


        public static Func<SearchFilterInfo, Expression<Func<T, bool>>> GetFilterExpression<T,TProp>( Expression<Func<T, TProp>> propertyGetter ) where T : class
        {
            return ( typeof( T ) ).GetProperty( propertyGetter.GetPropertyName() ).With( GetFilterExpression<T> );
        }
        public static Func<SearchFilterInfo, Expression<Func<T, bool>>> GetFilterExpression<T>( this PropertyInfo property ) where T : class
        {
            var parameter = Expression.Parameter( typeof( T ), "entity" );
            var access = Expression.MakeMemberAccess( parameter, property );
            var delegateType = typeof (Func<,>).MakeGenericType( typeof (T), property.PropertyType );
            var accessor = Expression.Lambda( delegateType, access, parameter );

            var typeKey = property.PropertyType.GetTypeKey();
            switch ( typeKey )
            {
                case "Guid":
                    return option => option.GetGuidPredicat( (Expression<Func<T, Guid>>)accessor );
                case "Guid?":
                    return option => option.GetGuidPredicat( (Expression<Func<T, Guid?>>)accessor );
                case "Boolean":
                    return option => option.GetBooleanPredicat( (Expression<Func<T, bool>>) accessor );
                case "String":
                    return option => option.GetStringContainsPredicat( (Expression<Func<T, string>>)accessor );
                case "DateTime":
                    return option => option.GetDateTimeComparerPredicat( (Expression<Func<T, DateTime>>)accessor );
                case "DateTime?":
                    return option => option.GetDateTimeComparerPredicat( (Expression<Func<T, DateTime?>>)accessor );
                case "Int32":
                    return option => option.GetIntComparerPredicat( (Expression<Func<T, int>>)accessor );
                case "Decimal":
                    return option => option.GetDecimalComparerPredicat( (Expression<Func<T, decimal>>)accessor );
                default:
                    return null;
            }
        }

        public static Expression<Func<T,object>> GetAccessExpression<T>( this PropertyInfo property ) where T : class
        {
            var parameter = Expression.Parameter( typeof( T ), "entity" );
            var access = Expression.Convert( Expression.MakeMemberAccess( parameter, property ), typeof( object ) );
            var accessor = Expression.Lambda<Func<T, object>>( access, parameter );
            return accessor;
        }

        /// <summary>
        /// Generates Equal predicat for one or more properties
        ///  GetEqualPredicat( idPropertyInfo, 123 ) call will generate following predicate expression:
        ///     entity => entity.Id == 123
        ///  GetEqualPredicat( new[]{ idPropertyInfo, namePropertyInfo], new []{ 12, "name1" } ) call will generate following predicate expression:
        ///     entity => entity.Id == 12 || entity.Name == "name1"
        /// </summary>
        public static Expression<Func<TReadModel, bool>> GetEqualPredicat<TReadModel>( PropertyInfo property, object compareValue ) where TReadModel : class
        {
            return GetEqualPredicat<TReadModel>( new[] { property }, new[] { compareValue } );
        }
        public static Expression<Func<TReadModel, bool>> GetEqualPredicat<TReadModel>( 
            PropertyInfo [] properties, object[] compareValues ) 
            where TReadModel : class
        {
            var parameter = Expression.Parameter( typeof( TReadModel ), "entity" );
            BinaryExpression binary = null;
            for ( var i = 0; i < properties.Length; i++ )
            {
                var method = Expression.Equal(
                    Expression.MakeMemberAccess( parameter, properties[i] ),
                    Expression.Constant( compareValues[i] ) );
                binary = binary == null ? method : Expression.AndAlso( binary, method );
            }
            return binary == null ? null : Expression.Lambda<Func<TReadModel, bool>>( binary, parameter );
        }

        /// <summary>
        /// Generates update expression for copiyng object, all properties will be copied from source entity,
        ///  beside the ones, which are assigned in custom expression
        /// </summary>
        public static TDestination CopyTo<TSource, TDestination>( this TSource source, Expression<Func<TSource, TDestination>> customAssigns )
        {
            var sourceType = typeof( TSource );
            var destType = typeof( TDestination );
            var sourceProperties = sourceType.GetProperties( BindingFlags.Public | BindingFlags.Instance ).ToList();
            var destProperties = destType.GetProperties( BindingFlags.Public | BindingFlags.Instance ).ToList();
            var customBindings = customAssigns
                .Body.OfType<MemberInitExpression>()
                .With( m => m.Bindings )
                .Return( b => b.ToList(), new List<MemberBinding>() );
            var parameter = customAssigns.Parameters[0];

            var bindings = destProperties
                .Select( destProp =>
                {
                    var customBinding = customBindings.SingleOrDefault( b => b.Member.Name == destProp.Name );
                    return 
                        customBinding ??
                        sourceProperties
                            .SingleOrDefault( p => p.Name == destProp.Name )
                            .With( srcProp => Expression.Bind( destProp, Expression.MakeMemberAccess( parameter, srcProp ) ) );
                } )
                .Where( bind => bind != null )
                .ToList();
            var ctor = Expression.New( destType );
            var memberInit = Expression.MemberInit( ctor, bindings );
            var lambda = Expression.Lambda<Func<TSource, TDestination>>( memberInit, parameter );

            return lambda.Compile()( source );
        }

        /// <summary>
        /// Initialize the given object's public properties with random data
        /// </summary>
        public static TObj GenerateData<TObj>( this TObj obj, Action<TObj> customAssigns = null )
        {
            var type = obj.GetType();
            var properties = type.GetProperties( BindingFlags.Public | BindingFlags.Instance ).ToList();
            properties.ForEach( property =>
            {
                object value;
                var random = new Random( (int) DateTime.Now.Ticks );
                var typeKey = property.PropertyType.GetTypeKey();
                byte[] buf;
                switch ( typeKey )
                {
                    case "String":
                        buf = new byte[10];
                        random.NextBytes( buf );
                        value = BitConverter.ToString( buf );
                        break;
                    case "Guid":
                        value = Guid.NewGuid();
                        break;
                    case "Guid?":
                        value = (Guid?)Guid.NewGuid();
                        break;
                    case "Int32":
                        value = random.Next( 1, 100 );
                        break;
                    case "Decimal":
                        value = (decimal)random.NextDouble();
                        break;
                    case "Boolean":
                        value = random.Next( 0, 1 ) == 1;
                        break;
                    case "DateTime":
                        value = DateTime.Now.AddDays( random.Next( 0, 100 ) );
                        break;
                    case "DateTime?":
                        value = (DateTime?)( DateTime.Now.AddDays( random.Next( 0, 100 ) ) );
                        break;
                    default:
                        return;
                }
                property.SetValue( obj, value, new object[]{} );
            } );

            customAssigns.Do( assign => assign( obj ) );
            return obj;
        }
    }
}