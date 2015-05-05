using System;

namespace EventStoreKit.ReadModels
{
    public interface IOrganizationReadModel
    {
        Guid OrganizationId { get; }
    }
}