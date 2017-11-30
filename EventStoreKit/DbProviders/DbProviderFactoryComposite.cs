using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace EventStoreKit.DbProviders
{
    public class DbProviderFactoryComposite : IDbProviderFactory
    {
        #region Private fields

        private readonly Func<IDbProvider> DefaultProvider;
        private readonly Dictionary<Type, Func<IDbProvider>> ProvidersMap = new Dictionary<Type, Func<IDbProvider>>();

        #endregion

        public DbProviderFactoryComposite()
        {
            
        }

        public IDbProvider Create()
        {
            return new DbProviderStub( StorageMap );
        }

        public IDbProvider Create<TModel>() where TModel : class
        {
            return Create();
        }
    }
}