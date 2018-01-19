using System;
using System.Linq.Expressions;

namespace EventStoreKit.Services
{
    public class DelegateAdjuster
    {
        public static Action<TBase> CastArgument<TBase, TDerived>( Expression<Action<TDerived>> source ) where TDerived : TBase
        {
            if ( typeof( TDerived ) == typeof( TBase ) )
            {
                return (Action<TBase>)( (Delegate)source.Compile() );
            }
            var sourceParameter = Expression.Parameter( typeof( TBase ), "source" );
            var result = Expression.Lambda<Action<TBase>>(
                Expression.Invoke(
                    source,
                    Expression.Convert( sourceParameter, typeof( TDerived ) ) ),
                sourceParameter );
            return result.Compile();
        }
        
        public static Func<TDerived> CastArgumentToDerived<TBase, TDerived>( Func<TBase> source ) where TDerived : TBase
        {
            if( typeof( TDerived ) == typeof( TBase ) )
            {
                return (Func<TDerived>)((Delegate)source);
            }
            var sourceParameter = Expression.Parameter( typeof( TDerived ), "source" );
            var result = Expression.Lambda<Func<TDerived>>(
                Expression.Invoke(
                    Expression.Call( source.Method ),
                    Expression.Convert( sourceParameter, typeof( TBase ) ) ),
                sourceParameter );
            return result.Compile();
        }
    }
}
