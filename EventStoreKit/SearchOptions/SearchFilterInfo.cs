using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EventStoreKit.SearchOptions
{
    public class SearchFilterInfo
    {
        public string Field { get; set; }
        public SearchFilterData Data { get; set; }
        
        public class SearchFilterData
        {
            #region Private fields

            private object ValueInternal;

            #endregion

            public string Type { get; set; }
            public string Comparison { get; set; } // lt - Before, gt - After, eq - On

            public object Value
            {
                get { return ValueInternal; }
                set
                {
                    ValueInternal = value;
                    StringValue = ( value as string ) ?? value.ToString();
                    if ( value is JArray )
                    {
                        var jValue = (JArray) value;
                        if( jValue.Type == JTokenType.Array )
                            Values = jValue.ToObject<IList<string>>();
                    }
                    if ( value is IEnumerable<string> )
                        Values = (IList<string>)value;
                    if ( value is IEnumerable<Guid> )
                        Guids = (IList<Guid>)value;
                    int intVal;
                    if( value is int )
                        IntValue = (int)value;
                    else if( int.TryParse( StringValue, out intVal ) )
                        IntValue = intVal;

                    DateTime date;
                    if ( DateTime.TryParse( StringValue, out date ) )
                        DateValue = date;
                }
            }

            [JsonIgnore]
            public string StringValue { get; set; }
            [JsonIgnore]
            public IList<string> Values { get; set; }
            [JsonIgnore]
            public IList<Guid> Guids { get; set; }
            [JsonIgnore]
            public int IntValue { get; set; }
            [JsonIgnore]
            public DateTime DateValue { get; set; }
        }
    }
}
