using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
                StringValue = ( value as string ) ?? value.ToString();

                if ( value is JArray )
                {
                    var jValue = (JArray)value;
                    if ( jValue.Type == JTokenType.Array )
                        ValueInternal = jValue.ToObject<IList<string>>();
                }
                else if ( value is IEnumerable<string> )
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
                else if ( int.TryParse( StringValue, out intVal ) )
                {
                    ValueInternal = intVal;
                }
                else if ( DateTime.TryParse( StringValue, out date ) )
                {
                    ValueInternal = date;
                }
                else
                {
                    ValueInternal = value;
                }
            }
        }

        [JsonIgnore]
        public string StringValue { get; set; }
    }
}
