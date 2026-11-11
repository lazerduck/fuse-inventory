using System.Security.Cryptography;
using Fuse.Core.Helpers;
using Fuse.Core.Areas.Security.Interfaces;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;

namespace Fuse.Core.Areas.Security.Entra;

public sealed class EntraIdentityService(IFuseStore store) : IEntraIdentityService
{
    public async Task<Result<Session>> SignIn(
        VerifiedEntraIdentity identity,
        Guid configurationVersion,
        Guid? linkUserId = null,
        int? linkSessionVersion = null)
    {
        Result<Session> result = Result<Session>.Failure("Entra sign-in could not be completed.", ErrorType.Unauthorized);
        await store.UpdateAsync(snapshot =>
        {
            var security = snapshot.SecurityContext;
            var settings = security.Entra;
            var now = DateTime.UtcNow;
            if (!settings.Enabled || settings.Version != configurationVersion || settings.TenantId != identity.TenantId
                || identity.ObjectId == Guid.Empty || identity.ValidUntil <= now || !security.Users.Any(u => u.IsAdmin))
                return snapshot;

            var user = security.Users.SingleOrDefault(u => u.EntraTenantId == identity.TenantId && u.EntraObjectId == identity.ObjectId);
            if (linkUserId is { } targetId)
            {
                var target = security.Users.SingleOrDefault(u => u.Id == targetId);
                if (target is null || target.IsDisabled || target.SessionVersion != linkSessionVersion
                    || (user is not null && user.Id != targetId)
                    || (target.EntraObjectId is not null && (target.EntraObjectId != identity.ObjectId || target.EntraTenantId != identity.TenantId)))
                {
                    result = Result<Session>.Failure("This identity cannot be linked to this account.", ErrorType.Conflict);
                    return snapshot;
                }
                user = target;
            }
            if (user?.IsDisabled == true)
                return snapshot;

            user ??= new FuseUser(Guid.NewGuid(), $"{identity.DisplayName} ({identity.ObjectId.ToString()[..8]})",
                "", "", false, [], now, now);
            var expiry = new[] { identity.ValidUntil, now.AddMinutes(settings.SessionMinutes) }.Min();
            user = user with
            {
                EntraTenantId = identity.TenantId,
                EntraObjectId = identity.ObjectId,
                LastEntraGroupIds = identity.GroupIds.Distinct().ToArray(),
                EntraVerifiedAt = now,
                EntraValidUntil = expiry,
                UpdatedAt = now
            };
            var session = new Session(Convert.ToHexString(RandomNumberGenerator.GetBytes(32)), user.Id, expiry)
            {
                IsEntra = true,
                Version = user.SessionVersion,
                EntraConfigurationVersion = settings.Version,
                EntraGroupIds = user.LastEntraGroupIds
            };
            result = Result<Session>.Success(session);
            return snapshot with
            {
                SecurityContext = security with
                {
                    Users = security.Users.Where(u => u.Id != user.Id).Append(user).ToArray(),
                    Sessions = security.Sessions.Where(s => s.ExpiresAt > now).Append(session).ToArray()
                }
            };
        });

        return result;
    }

    public async Task<Result> Revoke(Guid userId, bool? disabled = null)
    {
        Result result = Result.Failure("User not found.", ErrorType.NotFound);
        await store.UpdateAsync(snapshot =>
        {
            var security = snapshot.SecurityContext;
            var user = security.Users.SingleOrDefault(u => u.Id == userId);
            if (user is null)
                return snapshot;

            // Preserve a usable local recovery administrator.
            if (disabled == true && user.IsAdmin && !security.Users.Any(u => u.Id != userId && u.IsAdmin && !u.IsDisabled && u.PasswordHash.Length > 0))
            {
                result = Result.Failure("Keep at least one enabled local administrator for recovery.");
                return snapshot;
            }

            result = Result.Success();
            return snapshot with
            {
                SecurityContext = security with
                {
                    Users = security.Users.Select(u => u.Id != userId ? u : u with
                    {
                        IsDisabled = disabled ?? u.IsDisabled,
                        SessionVersion = u.SessionVersion + 1,
                        LastEntraGroupIds = [],
                        EntraValidUntil = null,
                        UpdatedAt = DateTime.UtcNow
                    }).ToArray(),
                    Sessions = security.Sessions.Where(s => s.UserId != userId).ToArray()
                }
            };
        });

        return result;
    }
}
