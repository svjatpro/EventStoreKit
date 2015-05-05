using System;

namespace EventStoreKit.Services.IdGenerators
{
    class DefaultIdGenerator : IIdGenerator
    {
        #region Implementation of IIdGenerator

        public Guid NewGuid()
        {
            return Guid.NewGuid();
        }

        #endregion
    }
}
