using System;

namespace EventStoreKit.Services
{
    public interface IUserAuth
    {
        Guid UserId { get; }
        Guid OrganizationId { get; }
        Guid CultureId { get; }
        string Login { get; }
        string Email { get; }
        string FirstName { get; }
        string LastName { get; }
    }

    public interface ISecurityManager
    {
        IUserAuth CurrentUser { get; }
        //IDictionary<UserGlobalPermission, bool> PermissionsMap { get; }
    }
}