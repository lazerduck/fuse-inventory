using Fuse.Core.Areas.Security.Interfaces;
using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Fuse.Core.Responses;

namespace Fuse.Core.Areas.Security.Services;

public class EntraSettingsService(IFuseStore store, IEntraSecretProtector secretProtector) : IEntraSettingsService
{
    public async Task<EntraSettingsInfo> GetSettings()
    {
        var settings = (await store.GetAsync()).SecurityContext.Entra;
        return new EntraSettingsInfo(
            settings.Enabled,
            settings.TenantId,
            settings.ClientId,
            settings.PublicOrigin,
            settings.SessionMinutes,
            !string.IsNullOrEmpty(settings.ProtectedClientSecret));
    }

    public async Task<Result> UpdateSettings(UpdateEntraSettings command)
    {
        if (command.SessionMinutes is < 5 or > 120)
            return Result.Failure("Session lifetime must be between 5 and 120 minutes.");

        if (!Uri.TryCreate(command.PublicOrigin, UriKind.Absolute, out var origin)
            || origin.AbsolutePath != "/"
            || origin.Query.Length != 0
            || origin.Fragment.Length != 0
            || origin.UserInfo.Length != 0
            || !(origin.Scheme == "https" || origin.Scheme == "http" && origin.IsLoopback)
            || command.TenantId == Guid.Empty
            || command.ClientId == Guid.Empty)
        {
            return Result.Failure("Enter tenant and client IDs and a public HTTPS origin (HTTP is allowed for localhost).");
        }

        // Keep secret retention, connection changes and session invalidation in one store mutation.
        var result = Result.Success();
        var protectedSecret = string.IsNullOrWhiteSpace(command.ClientSecret)
            ? null
            : secretProtector.Protect(command.ClientSecret);

        await store.UpdateAsync(snapshot =>
        {
            var security = snapshot.SecurityContext;
            var current = security.Entra;
            var secret = protectedSecret ?? current.ProtectedClientSecret;
            if (protectedSecret is null && (current.ClientId != command.ClientId || current.TenantId != command.TenantId))
                secret = "";

            if (command.Enabled && string.IsNullOrEmpty(secret))
            {
                result = Result.Failure("A client secret is required.");
                return snapshot;
            }

            var settings = new EntraSettings
            {
                Enabled = command.Enabled,
                TenantId = command.TenantId,
                ClientId = command.ClientId,
                PublicOrigin = origin.GetLeftPart(UriPartial.Authority),
                ProtectedClientSecret = secret,
                SessionMinutes = command.SessionMinutes,
                Version = Guid.NewGuid()
            };

            return snapshot with
            {
                SecurityContext = security with
                {
                    Entra = settings,
                    Posture = settings.Enabled ? SecurityPosture.FullyRestricted : security.Posture,
                    Sessions = security.Sessions.Where(session => !session.IsEntra).ToArray(),
                    Users = security.Users.Select(user => user with
                    {
                        LastEntraGroupIds = [],
                        EntraValidUntil = null
                    }).ToArray()
                }
            };
        });

        return result;
    }
}
