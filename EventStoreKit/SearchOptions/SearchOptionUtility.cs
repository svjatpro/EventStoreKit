using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using EventStoreKit.Constants;
using EventStoreKit.Utility;

namespace EventStoreKit.SearchOptions
{
    public static class SearchOptionUtility
    {
        public static string FilterKey( this SearchOptions options )
        {
            return options.Filters
                .With( filters => string.Join( "#", filters
                    .Select( f => 
                        f.Field + "_" + 
                        ( f.Data.Comparison != SearchComparisonType.None ? ( ((int)(f.Data.Comparison)).ToString() + "_" ) : "" ) +
                        f.Data.StringValue ) ) );
        }
        public static Expression<Func<T, bool>> GetGuidPredicat<T>( this SearchFilterInfo option, Expression<Func<T, Guid>> getProperty )
        {
            Guid value;
            if ( option.Data.Value is Guid )
                value = (Guid)option.Data.Value;
            else if ( !Guid.TryParse( option.Data.StringValue, out value ) )
                return e => false;
            var equal = Expression.Equal( getProperty.Body, Expression.Constant( value ) );
            return Expression.Lambda<Func<T, bool>>( equal, getProperty.Parameters[0] );
        }
        public static Expression<Func<T, bool>> GetGuidPredicat<T>( this SearchFilterInfo option, Expression<Func<T, Guid?>> getProperty )
        {
            Guid? value;
            Guid parsedValue;
            if ( option.Data.Value is Guid )
            {
                value = (Guid) option.Data.Value;
            }
            else 
            {
                if ( Guid.TryParse( option.Data.StringValue, out parsedValue ) )
                    value = parsedValue;
                else
                    value = null;
            }
            var equal = Expression.Equal( getProperty.Body, Expression.Constant( value, typeof(Guid?) ) );
            return Expression.Lambda<Func<T, bool>>( equal, getProperty.Parameters[0] );
        }

        public static Expression<Func<T,bool>> GetBooleanPredicat<T>( this SearchFilterInfo option, Expression<Func<T,bool>> getProperty )
        {
            var value = Equals( option.Data.Value, true ) || Equals( option.Data.StringValue.ToLower(), "true" );
            var equal = Expression.Equal( getProperty.Body, Expression.Constant( value ) );
            return Expression.Lambda<Func<T, bool>>( equal, getProperty.Parameters[0] );
        }

        public static Expression<Func<T,bool>> GetStringContainsPredicat<T>( this SearchFilterInfo option, Expression<Func<T,string>> getProperty )
        {
            var value = Expression.Constant( option.Data.StringValue/*.Trim()*/, typeof(string) );
            var method = typeof( string ).GetMethod( "Contains", new[] { typeof( string ) } );
            var propertyExp = (MemberExpression)getProperty.Body;

            var call = Expression.Call( propertyExp, method, value );
            return Expression.Lambda<Func<T, bool>>( call, getProperty.Parameters[0] );
        }

        public static Expression<Func<T, bool>> GetDateTimeComparerPredicat<T>( this SearchFilterInfo option, Expression<Func<T, DateTime>> getProperty )
        {
            DateTime date;
            if ( option.Data.Value is DateTime )
                date = (DateTime) option.Data.Value;
            else if ( !DateTime.TryParse( option.Data.StringValue, new CultureInfo("en"), DateTimeStyles.None, out date ) )
                return e => true;

            var value = Expression.Constant( date, typeof( DateTime ) );
            var propertyExp = (MemberExpression)getProperty.Body;
            switch ( option.Data.Comparison )
            {
                case SearchComparisonType.On:
                    var yearProperty = Expression.MakeMemberAccess( getProperty.Body, typeof( DateTime ).GetProperty( "Year" ) );
                    var monthProperty = Expression.MakeMemberAccess( getProperty.Body, typeof( DateTime ).GetProperty( "Month" ) );
                    var dayProperty = Expression.MakeMemberAccess( getProperty.Body, typeof( DateTime ).GetProperty( "Day" ) );
                    var yearEqual = Expression.Equal( yearProperty, Expression.Constant( date.Year, typeof( int ) ) );
                    var monthEqual = Expression.Equal( monthProperty, Expression.Constant( date.Month, typeof( int ) ) );
                    var dayEqual = Expression.Equal( dayProperty, Expression.Constant( date.Day, typeof( int ) ) );
                    return Expression.Lambda<Func<T, bool>>( 
                        Expression.AndAlso( Expression.AndAlso( yearEqual, monthEqual ), dayEqual ),
                        getProperty.Parameters[0] );
                case SearchComparisonType.After:
                    return Expression.Lambda<Func<T,bool>>( Expression.GreaterThan( propertyExp, value ), getProperty.Parameters[0] );
                case SearchComparisonType.Before:
                    return Expression.Lambda<Func<T,bool>>( Expression.LessThan( propertyExp, value ), getProperty.Parameters[0] );
                default:
                    return e => true;
            }
        }

        public static Expression<Func<T, bool>> GetDateTimeComparerPredicat<T>( this SearchFilterInfo option, Expression<Func<T, DateTime?>> getProperty )
        {
            DateTime date;
            if ( option.Data.Value is DateTime )
                date = (DateTime)option.Data.Value;
            else if ( !DateTime.TryParse( option.Data.StringValue, out date ) )
                return e => true;

            var value = Expression.Constant( date, typeof( DateTime ) );
            var dataValue = Expression.MakeMemberAccess( getProperty.Body, typeof( DateTime? ).GetProperty( "Value" ) );
            var propertyExp = dataValue;
            switch ( option.Data.Comparison )
            {
                case SearchComparisonType.On:
                    var yearProperty = Expression.MakeMemberAccess( dataValue, typeof( DateTime ).GetProperty( "Year" ) );
                    var monthProperty = Expression.MakeMemberAccess( dataValue, typeof( DateTime ).GetProperty( "Month" ) );
                    var dayProperty = Expression.MakeMemberAccess( dataValue, typeof( DateTime ).GetProperty( "Day" ) );
                    var yearEqual = Expression.Equal( yearProperty, Expression.Constant( date.Year, typeof( int ) ) );
                    var monthEqual = Expression.Equal( monthProperty, Expression.Constant( date.Month, typeof( int ) ) );
                    var dayEqual = Expression.Equal( dayProperty, Expression.Constant( date.Day, typeof( int ) ) );
                    return Expression.Lambda<Func<T, bool>>(
                        Expression.AndAlso( Expression.AndAlso( yearEqual, monthEqual ), dayEqual ),
                        getProperty.Parameters[0] );
                case SearchComparisonType.After:
                    return Expression.Lambda<Func<T, bool>>( Expression.GreaterThan( propertyExp, value ), getProperty.Parameters[0] );
                case SearchComparisonType.Before:
                    return Expression.Lambda<Func<T, bool>>( Expression.LessThan( propertyExp, value ), getProperty.Parameters[0] );
                default:
                    return e => true;
            }
        }

        public static Expression<Func<T, bool>> GetIntComparerPredicat<T>( this SearchFilterInfo option, Expression<Func<T, int>> getProperty )
        {
            int value;
            if( option.Data.Value is int )
                value = (int)option.Data.Value;
            else if ( !int.TryParse( option.Data.StringValue, out value ) )
                return r => true;
            return GetValueComparerPredicat( option, getProperty, value );
        }

        public static Expression<Func<T, bool>> GetDecimalComparerPredicat<T>( this SearchFilterInfo option, Expression<Func<T, decimal>> getProperty )
        {
            decimal value;
            if ( option.Data.Value is decimal )
                value = (decimal)option.Data.Value;
            else if ( !decimal.TryParse( 
                option.Data.StringValue, 
                NumberStyles.Float,
                Thread.CurrentThread.CurrentCulture, out value ) )
                //new CultureInfo( "en" ), out value ) )
            {
                return r => true;
            }
            return GetValueComparerPredicat( option, getProperty, value );
        }

        public static Expression<Func<T, bool>> GetValueComparerPredicat<T, TVal>( 
            this SearchFilterInfo option, 
            Expression<Func<T, TVal>> getProperty,
            TVal val )
        {
            var value = Expression.Constant( val, typeof( TVal ) );
            var propertyExp = (MemberExpression)getProperty.Body;
            switch ( option.Data.Comparison )
            {
                case SearchComparisonType.On:
                    return Expression.Lambda<Func<T, bool>>( Expression.Equal( propertyExp, value ), getProperty.Parameters[0] );
                case SearchComparisonType.After:
                    return Expression.Lambda<Func<T, bool>>( Expression.GreaterThan( propertyExp, value ), getProperty.Parameters[0] );
                case SearchComparisonType.Before:
                    return Expression.Lambda<Func<T, bool>>( Expression.LessThan( propertyExp, value ), getProperty.Parameters[0] );
                default:
                    return e => true;
            }
        }

        public static Expression<Func<T, bool>> GetIntListPredicat<T>( 
            this SearchFilterInfo option,
            Expression<Func<T, int>> getProperty )
        {
            IList<int> values = ( option.Data.Value as IEnumerable<int> ).With( v => v.ToList() );
            if ( values == null )
            {
                values = 
                    ( option.Data.Values.With( v => v.Select( int.Parse ) ) ?? new[] {int.Parse( option.Data.StringValue )} )
                    .ToList();
            }
            if ( values.Count() == 1 )
            {
                var equal = Expression.Equal( getProperty.Body, Expression.Constant( values[0] ) );
                return Expression.Lambda<Func<T, bool>>( equal, getProperty.Parameters[0] );
            }
            else
            {
                var method = typeof( ICollection<int> ).GetMethod( "Contains", new[] { typeof( int ) } );
                var call = Expression.Call( Expression.Constant( values, typeof( ICollection<int> ) ), method, getProperty.Body );
                return Expression.Lambda<Func<T, bool>>( call, getProperty.Parameters[0] );
            }
        }
    }
}