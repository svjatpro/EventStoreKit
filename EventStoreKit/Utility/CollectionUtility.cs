using System.Collections.Generic;

namespace EventStoreKit.Utility
{
    public static class CollectionUtility
    {
        public static TValue Get<TKey, TValue>( this Dictionary<TKey, TValue> dictionary, TKey key )
        {
            TValue value; // = default(TValue);
            dictionary.TryGetValue( key, out value );
            return value;
        }
    }
}
