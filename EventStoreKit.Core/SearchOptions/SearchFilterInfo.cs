using System;
using System.Collections.Generic;

namespace EventStoreKit.SearchOptions
{
    public interface IFilter
    {
        string FieldName { get; }
        SearchComparisonType Comparison { get; }
        object Value { get; }
        string StringValue { get; }
    }

    public class SearchFilterInfo : IFilter
    {
        #region Private fields

        private object ValueInternal;

        #endregion

        public string FieldName { get; set; }
        public SearchComparisonType Comparison { get; set; }

        public object Value
        {
            get { return ValueInternal; }
            set
            {
                int intVal;
                DateTime date;
                var stringValue = value.ToString();

                if ( value is IEnumerable<string> )
                {
                    ValueInternal = (IList<string>) value;
                }
                else if ( value is IEnumerable<Guid> )
                {
                    ValueInternal = (IList<Guid>) value;
                }
                else if ( value is int )
                {
                    ValueInternal = (int) value;
                }
                else if ( int.TryParse( stringValue, out intVal ) )
                {
                    ValueInternal = intVal;
                }
                else if ( DateTime.TryParse( stringValue, out date ) )
                {
                    ValueInternal = date;
                }
                else
                {
                    ValueInternal = value;
                }
            }
        }

        public string StringValue { get { return ValueInternal.ToString(); } }
    }
}
