using System;
using System.Linq.Expressions;

namespace OSMD.Domain
{
    public static class SearchOptionUtility
    {
        public static Expression<Func<T,bool>> GetBooleanPredicat<T>( this SearchFilterInfo option, Expression<Func<T,bool>> getProperty )
        {
            var value = Equals( option.Data.Value, true ) || Equals( option.Data.Value, "true" );
            var equal = Expression.Equal( getProperty.Body, Expression.Constant( value ) );
            return Expression.Lambda<Func<T, bool>>( equal, getProperty.Parameters[0] );
        }

        //public static Expression<Func<T, bool>> GetIntListPredicat<T>( this SearchFilterInfo option, Expression<Func<T, int>> getProperty )
        //{
        //    IList<int> values = ( option.Data.Value as IEnumerable<int> ).With( v => v.ToList() );
        //    if( values == null )
        //        values = ( option.Data.Values.With( v => v.Select( int.Parse ) ) ?? new[] { int.Parse( option.Data.StringValue ) } ).ToList();

        //    if ( values.Count() == 1 )
        //    {
        //        var equal = Expression.Equal( getProperty.Body, Expression.Constant( values[0] ) );
        //        return Expression.Lambda<Func<T, bool>>( equal, getProperty.Parameters[0] );
        //    }
        //    else
        //    {
        //        Expression<Func<T, bool>> contains = e => values.Contains( 1 );
        //        //var propertyGetter = Expression.MakeMemberAccess( getProperty.Parameters[0], getProperty.Body )
        //        var methodCall = (MethodCallExpression)contains.Body;
        //        //return Expression.Lambda<Func<T, bool>>( Expression.Call( methodCall.Method, getProperty.Body ), getProperty.Parameters[0] );
        //        return Expression.Lambda<Func<T, bool>>( Expression.Call( methodCall.Method, methodCall.Arguments[0], getProperty.Body ), getProperty.Parameters[0] );

        //        //return r => values.Contains( r.CurrentStatusId );   

        //        //Expression<Func<ResearchView, bool>> like = r => Sql.Like( r.ClientPath, "stub" );
        //        //var methodCall = (MethodCallExpression)like.Body;
        //        //var methods = options.Data.Values
        //        //   .Select( v => Expression.Call( methodCall.Method, methodCall.Arguments[0], Expression.Constant( string.Format( "%{0}%", v ) ) ) )
        //        //   .ToList();
        //        //Expression binaryExpression = null;
        //        //methods
        //        //   .Skip( 1 )
        //        //   .ToList()
        //        //   .ForEach( expr => binaryExpression = Expression.OrElse( binaryExpression ?? methods[0], expr ) );
        //        //return Expression.Lambda<Func<ResearchView, bool>>( binaryExpression, like.Parameters[0] );
        //    }
        //}
    }
}