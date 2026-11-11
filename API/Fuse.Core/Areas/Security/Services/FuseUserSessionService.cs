using Fuse.Core.Areas.Security.Interfaces;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using System.Security.Cryptography;

namespace Fuse.Core.Areas.Security.Services;

public class FuseUserSessionService(IFuseStore fuseStore) : IFuseUserSessionService
{
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(30);

    public async Task<Result<string>> CreateSession(FuseUser user)
    {
        if (user is null)
            return Result<string>.Failure("User is required.", ErrorType.Validation);

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var expiresAt = DateTime.UtcNow.Add(SessionLifetime);
        var session = new Session(token, user.Id, expiresAt) { Version = user.SessionVersion };

        await fuseStore.UpdateAsync(s => s with
        {
            SecurityContext = s.SecurityContext with
            {
                Sessions = s.SecurityContext.Sessions.Append(session).ToList()
            }
        });

        return Result<string>.Success(token);
    }

    public async Task<Result<string>> RefreshSession(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return Result<string>.Failure("Token is required.", ErrorType.Validation);

        var snapshot = await fuseStore.GetAsync();
        var existing = snapshot.SecurityContext.Sessions.FirstOrDefault(s => s.Token == token);

        if (existing is null)
            return Result<string>.Failure("Session not found.", ErrorType.NotFound);

        if (existing.ExpiresAt <= DateTime.UtcNow)
        {
            await fuseStore.UpdateAsync(s => s with
            {
                SecurityContext = s.SecurityContext with
                {
                    Sessions = s.SecurityContext.Sessions.Where(x => x.Token != token).ToList()
                }
            });

            return Result<string>.Failure("Session has expired.", ErrorType.Unauthorized);
        }

        if (existing.IsEntra)
            return Result<string>.Failure("Sign in with Microsoft again to refresh Entra membership.", ErrorType.Unauthorized);
        var validation = await ValidateSession(token);
        if (!validation.IsSuccess)
            return Result<string>.Failure("Session has been revoked.", ErrorType.Unauthorized);

        var refreshedToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        await fuseStore.UpdateAsync(s => s with
        {
            SecurityContext = s.SecurityContext with
            {
                Sessions = s.SecurityContext.Sessions
                    .Select(x =>
                    {
                        if (x.Token != token) return x;
                        return x with
                        {
                            Token = refreshedToken,
                            ExpiresAt = DateTime.UtcNow.Add(SessionLifetime)
                        };
                    })
                    .ToList()
            }
        });

        return Result<string>.Success(refreshedToken);
    }

    public async Task<Result> DeleteSession(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return Result.Failure("Token is required.", ErrorType.Validation);

        await fuseStore.UpdateAsync(s => s with
        {
            SecurityContext = s.SecurityContext with
            {
                Sessions = s.SecurityContext.Sessions.Where(x => x.Token != token).ToList()
            }
        });

        return Result.Success();
    }

    public async Task<Result<Guid>> ValidateSession(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return Result<Guid>.Failure("Token is required.", ErrorType.Validation);

        var snapshot = await fuseStore.GetAsync();
        var session = snapshot.SecurityContext.Sessions.FirstOrDefault(s => s.Token == token);

        if (session is null)
            return Result<Guid>.Failure("Session not found.", ErrorType.Unauthorized);

        if (session.ExpiresAt <= DateTime.UtcNow)
            return Result<Guid>.Failure("Session has expired.", ErrorType.Unauthorized);

        var user = snapshot.SecurityContext.Users.FirstOrDefault(u => u.Id == session.UserId);
        if (user is null || user.IsDisabled || user.SessionVersion != session.Version)
            return Result<Guid>.Failure("Session has been revoked.", ErrorType.Unauthorized);
        var entra = snapshot.SecurityContext.Entra;
        if (session.IsEntra && (!entra.Enabled || entra.Version != session.EntraConfigurationVersion
            || user.EntraTenantId != entra.TenantId))
            return Result<Guid>.Failure("Entra sign-in must be refreshed.", ErrorType.Unauthorized);

        return Result<Guid>.Success(session.UserId);
    }

    public async Task<Result<DateTime>> GetExpiry(string token)
    {
        var snapshot = await fuseStore.GetAsync();
        var session = snapshot.SecurityContext.Sessions.FirstOrDefault(s => s.Token == token);

        if (session is null)
        {
            return Result<DateTime>.Failure("Could not find session", ErrorType.NotFound);
        }

        return Result<DateTime>.Success(session.ExpiresAt);
    }
}
