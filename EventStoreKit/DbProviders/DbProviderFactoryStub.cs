namespace EventStoreKit.DbProviders
{
    public class DbProviderFactoryStub : IDbProviderFactory
    {
        public IDbProvider Create()
        {
            return new DbProviderStub();
        }

        public IDbProvider Create<TModel>() where TModel : class
        {
            return Create();
        }
    }
}