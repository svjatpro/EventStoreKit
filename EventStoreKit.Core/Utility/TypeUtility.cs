using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace EventStoreKit.Core.Utility
{
    public static class TypeUtility
    {
        public static object TryCreateInstance( this Type targetType, Dictionary<Type, object> arguments )
        {
            var ctor = targetType.GetConstructor( arguments );
            return
                ctor != null ? 
                ctor.Invoke( arguments.Values.ToArray() ) : 
                null;
        }
        public static ConstructorInfo GetConstructor( this Type targetType, Dictionary<Type, object> arguments )
        {
            var ctor = targetType
                .GetConstructors( BindingFlags.Public | BindingFlags.Instance )
                .FirstOrDefault( c =>
                {
                    var args = c.GetParameters();
                    if( args.Length != arguments.Count )
                        return false;
                    var types = arguments.Keys.ToList();
                    for( var i = 0; i < types.Count; i++ )
                    {
                        if( args[i].ParameterType != types[i] )
                            return false;
                    }
                    return true;
                } );
            return ctor;
        }
    }
}
