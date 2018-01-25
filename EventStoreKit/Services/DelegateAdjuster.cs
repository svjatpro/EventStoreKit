using System;
using System.Linq.Expressions;

namespace EventStoreKit.Services
{
    public class DelegateAdjuster
    {
        public static Action<TBase> CastArgument<TBase, TDerived>( Action<TDerived> source ) where TDerived : TBase
        {
            if( typeof( TDerived ) == typeof( TBase ) )
                return (Action<TBase>)( (Delegate)source );
            var sourceParameter = Expression.Parameter( typeof( TBase ), "source" );
            var result = Expression.Lambda<Action<TBase>>(
                Expression.Call(
                    Expression.Constant( source.Target ),
                    source.Method,
                    Expression.Convert( sourceParameter, typeof( TDerived ) ) ),
                sourceParameter );
            return result.Compile();
        }

        public static Func<TDerived> CastResultToDerived<TBase, TDerived>( Func<TBase> source ) where TDerived : TBase
        {
            if( typeof( TDerived ) == typeof( TBase ) )
                return (Func<TDerived>)( (Delegate)source );
            var result = Expression
                .Lambda<Func<TDerived>>( Expression
                    .Convert( Expression
                        .Invoke( Expression.Constant( source ) ), typeof( TDerived ) ) );
            return result.Compile();
        }
    }
}
