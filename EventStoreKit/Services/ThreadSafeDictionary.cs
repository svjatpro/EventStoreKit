using System.Collections.Generic;
using System.Threading;

namespace EventStoreKit.Services
{
    public class ThreadSafeDictionary<TKey, TValue> : Dictionary<TKey, TValue>
    {
        private readonly ReaderWriterLockSlim Locker = new ReaderWriterLockSlim();

        public void EnterWriteLock() { Locker.EnterWriteLock(); }
        public void ExitWriteLock() { Locker.ExitWriteLock(); }

        public new void Clear()
        {
            Locker.EnterWriteLock();
            try
            {
                base.Clear();
            }
            finally
            {
                Locker.ExitWriteLock();
            }
        }
        public new bool ContainsKey(TKey key)
        {
            bool contains;
            Locker.EnterReadLock();
            try
            {
                contains = base.ContainsKey(key);
            }
            finally
            {
                Locker.ExitReadLock();
            }
            return contains;
        }
        public void ThreadSafeAdd(TKey key, TValue value)
        {
            Locker.EnterWriteLock();
            try
            {
                if (!base.ContainsKey(key))
                    Add(key, value);
            }
            finally
            {
                Locker.ExitWriteLock();
            }
        }
        public void AddOrUpdate( TKey key, TValue value )
        {
            Locker.EnterWriteLock();
            try
            {
                if ( !base.ContainsKey( key ) )
                    Add( key, value );
                else
                    base[key] = value;
            }
            finally
            {
                Locker.ExitWriteLock();
            }
        }
        public void RemoveIfExist( TKey key )
        {
            Locker.EnterWriteLock();
            try
            {
                if ( base.ContainsKey( key ) )
                    base.Remove( key );
            }
            finally
            {
                Locker.ExitWriteLock();
            }
        }
        public new void Remove(TKey key)
        {
            Locker.EnterWriteLock();
            try
            {
                base.Remove(key);
            }
            finally
            {
                Locker.ExitWriteLock();
            }
        }
        
        public new TValue this[TKey key]
        {
            get
            {
                Locker.EnterReadLock();
                TValue value;
                try
                {
                    value = base[key];
                }
                finally
                {
                    Locker.ExitReadLock();
                }
                return value;
            }
        }

        public new bool TryGetValue( TKey key, out TValue value )
        {
            Locker.EnterReadLock();
            bool result;
            try
            {
                result = base.TryGetValue( key, out value );
            }
            finally
            {
                Locker.ExitReadLock();
            }
            return result;
        }
    }
}
