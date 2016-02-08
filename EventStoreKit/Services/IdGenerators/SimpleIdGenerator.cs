using System;

namespace EventStoreKit.Services.IdGenerators
{
    public class SimpleIdGenerator : IIdGenerator
    {
        #region Implementation of IIdGenerator

        public Guid NewGuid()
        {
            return Guid.NewGuid();
        }

        #endregion
    }
}
