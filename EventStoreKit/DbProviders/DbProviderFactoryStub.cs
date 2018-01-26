using System;
using System.Collections;
using System.Collections.Concurrent;
using EventStoreKit.Services;

namespace EventStoreKit.DbProviders
{
    public class DbProviderFactoryStub : IDbProviderFactory
    {
        // a simplest impementation of in-memory storage, 
        //  do not use the same ReadModel in more than one projection - it is not thread safe!
        private volatile ConcurrentDictionary<Type, IList> StorageMap = new ConcurrentDictionary<Type, IList>();

        public IDbProvider Create()
        {
            return new DbProviderStub( StorageMap );
        }

        public IDbProvider Create( IDataBaseConfiguration configuration )
        {
            return new DbProviderStub( StorageMap );
        }
    }
}