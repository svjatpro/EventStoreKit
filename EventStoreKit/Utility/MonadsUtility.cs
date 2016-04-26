using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace EventStoreKit.Utility
{
    public static class MonadsUtility
    {
        public static TResult With<TSource, TResult>( this TSource source, Func<TSource, TResult> func ) where TSource : class
        {
            if ( source == null )
                return default( TResult );
            return func( source );
        }
        public static TResult With<TSource, TResult>( this TSource? source, Func<TSource?, TResult> action ) where TSource : struct
        {
            return source.HasValue ? action( source ) : default( TResult );
        }
        public static IEnumerable<TResult> With<TSource, TResult>( this IEnumerable<TSource> source, Func<TSource, TResult> action )
        {
            return source != null ? source.Select( action ) : null;
        }


        public static TResult Return<TSource, TResult>( this TSource source, Func<TSource, TResult> action, TResult defaultValue ) where TSource : class
        {
            return source != default( TSource ) ? action( source ) : defaultValue;
        }
        public static TResult Return<TSource, TResult>( this TSource? source, Func<TSource?, TResult> action, TResult defaultValue ) where TSource : struct
        {
            return source.HasValue ? action( source ) : defaultValue;
        }
        

        public static TSource Do<TSource>( this TSource source, Action<TSource> action ) where TSource : class
        {
            if ( source != default( TSource ) )
                action( source );
            return source;
        }
        public static TSource? Do<TSource>( this TSource? source, Action<TSource?> action ) where TSource : struct
        {
            if ( source.HasValue )
                action( source );
            return source;
        }
        public static IEnumerable Do( this IEnumerable source, Action<object> action )
        {
            if ( source != null )
                foreach ( var element in source )
                    action( element );
            return source;
        }
        public static IEnumerable<TSource> Do<TSource>( this IEnumerable<TSource> source, Action<TSource> action )
        {
            if ( source != null )
                foreach ( var element in source )
                    action( element );
            return source;
        }
        public static IEnumerable<TSource> Do<TSource>( this IEnumerable<TSource> source, Action<TSource, int> action )
        {
            if ( source != null )
                foreach ( var element in source.Select( ( s, i ) => new { Source = s, Index = i } ) )
                    action( element.Source, element.Index );
            return source;
        }
        

        public static TResult OfType<TResult>( this object source )
        {
            return source is TResult ? (TResult) source : default( TResult );
        }


        public static EventHandler<TArgs> Execute<TArgs>( this EventHandler<TArgs> source, object sender, TArgs args ) where TArgs : EventArgs
        {
            if ( source != null )
                source.Invoke( sender, args );
            return source;
        }

        
        public static TSource CheckNull<TSource>( this TSource source, string argumentName ) where TSource : class
        {
            if ( source == null )
                throw new ArgumentNullException( argumentName );
            return source;
        }
        public static TSource CheckNull<TSource>( this TSource source, Func<Exception> exceptionSource ) where TSource : class
        {
            if ( source == null )
                throw exceptionSource();
            return source;
        }
    }
}
