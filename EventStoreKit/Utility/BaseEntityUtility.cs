using System;
using System.Linq;
using System.Linq.Expressions;
using System.Monads;
using System.Reflection;

namespace EventStoreKit.Utility
{
    public static class BaseEntityUtility
    {
        /// <summary>
        /// Picks the property name from Expression
        ///  Expression should looks like this ( entity => entity.Property )
        ///  otherwise the method will throw ArgumentException
        /// </summary>
        /// <exception cref="ArgumentException"></exception>
        public static string GetPropertyName<TPropertyDelegate>( this Expression<TPropertyDelegate> propertyDescriptor )
        {
            return propertyDescriptor
                .CheckNull( "propertyDescriptor" )
                .Body.OfType<MemberExpression>()
                .Return( e => e, propertyDescriptor.Body.OfType<UnaryExpression>().With( e => e.Operand.OfType<MemberExpression>() ) )
                .CheckNull( () => new ArgumentException( "Can't use this kind of Expression!" ) )
                .Member.Name;
        }
        public static string GetPropertyName<TEntity, TProperty>( this TEntity entity, Expression<Func<TEntity, TProperty>> propertyDescriptor )
        {
            return propertyDescriptor.GetPropertyName();
        }
        public static string GetPropertyName<TEntity, TProperty>( this TEntity entity, Expression<Func<TProperty>> propertyDescriptor )
        {
            return propertyDescriptor.GetPropertyName();
        }
        public static string GetPropertyName<TEntity, TProperty>( Expression<Func<TEntity, TProperty>> propertyDescriptor )
        {
            return propertyDescriptor.GetPropertyName();
        }

        public static string ToLowerCamelCase( this string str )
        {
            if (!string.IsNullOrEmpty(str)) 
                return Char.ToLowerInvariant(str[0]) + str.Substring(1);
            return "";
        }
        public static string ToUpperCamelCase(this string str)
        {
            if (!string.IsNullOrEmpty(str))
                return Char.ToUpperInvariant(str[0]) + str.Substring(1);
            return "";
        }

        public static PropertyInfo ResolveProperty( this Type type, params string[] properties )
        {
            //var type = typeof( T );
            PropertyInfo property = null;
            foreach ( var propertyName in properties.Where( p => p != null ).Distinct().ToList() )
            {
                if ( ( property = type.GetProperty( propertyName ) ).IsNotNull() )
                    break;
            }
            return property;
        }
    }
}