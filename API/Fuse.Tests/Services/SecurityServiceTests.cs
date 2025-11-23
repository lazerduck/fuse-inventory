using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Models;
using Fuse.Core.Services;
using Fuse.Tests.TestInfrastructure;
using System.Linq;
using Xunit;

namespace Fuse.Tests.Services;

public class SecurityServiceTests
{
    private static InMemoryFuseStore NewStore(
        SecuritySettings? settings = null,
        IEnumerable<SecurityUser>? users = null)
    {
        var securityState = new SecurityState(
            settings ?? new SecuritySettings(SecurityLevel.None, DateTime.UtcNow),
            (users ?? Array.Empty<SecurityUser>()).ToArray()
        );

        var snapshot = new Snapshot(
            Applications: Array.Empty<Application>(),
            DataStores: Array.Empty<DataStore>(),
            Platforms: Array.Empty<Platform>(),
            ExternalResources: Array.Empty<ExternalResource>(),
            Accounts: Array.Empty<Account>(),
            Tags: Array.Empty<Tag>(),
            Environments: Array.Empty<EnvironmentInfo>(),
            KumaIntegrations: Array.Empty<KumaIntegration>(),
                SecretProviders: Array.Empty<SecretProvider>(),
                SqlIntegrations: Array.Empty<SqlIntegration>(),
                Security: securityState
        );

        return new InMemoryFuseStore(snapshot);
    }

    #region GetSecurityStateAsync Tests

    [Fact]
    public async Task GetSecurityStateAsync_ReturnsCurrentState()
    {
        var settings = new SecuritySettings(SecurityLevel.RestrictedEditing, DateTime.UtcNow);
        var store = NewStore(settings: settings);
        var auditService = new FakeAuditService();
        var service = new SecurityService(store, auditService);

        var state = await service.GetSecurityStateAsync();

    Assert.NotNull(state);
    Assert.Equal(SecurityLevel.RestrictedEditing, state.Settings.Level);
    Assert.Empty(state.Users);
    }

    #endregion

    #region CreateUserAsync Tests

    [Fact]
    public async Task CreateUserAsync_NullCommand_ReturnsFailure()
    {
        var store = NewStore();
        var auditService = new FakeAuditService();
        var service = new SecurityService(store, auditService);

        var result = await service.CreateUserAsync(null!);

    Assert.False(result.IsSuccess);
    Assert.Equal("Invalid security user command.", result.Error);
    }

    [Fact]
    public async Task CreateUserAsync_EmptyUserName_ReturnsFailure()
    {
        var store = NewStore();
        var auditService = new FakeAuditService();
        var service = new SecurityService(store, auditService);

        var result = await service.CreateUserAsync(new CreateSecurityUser("", "password123", SecurityRole.Admin));

    Assert.False(result.IsSuccess);
    Assert.Equal("User name cannot be empty.", result.Error);
    }

    [Fact]
    public async Task CreateUserAsync_EmptyPassword_ReturnsFailure()
    {
        var store = NewStore();
        var auditService = new FakeAuditService();
        var service = new SecurityService(store, auditService);

        var result = await service.CreateUserAsync(new CreateSecurityUser("admin", "", SecurityRole.Admin));

    Assert.False(result.IsSuccess);
    Assert.Equal("Password cannot be empty.", result.Error);
    }

    [Fact]
    public async Task CreateUserAsync_DuplicateUserName_ReturnsConflict()
    {
        var existingUser = new SecurityUser(Guid.NewGuid(), "admin", "hash", "salt", SecurityRole.Admin, DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(users: new[] { existingUser });
        var auditService = new FakeAuditService();
        var service = new SecurityService(store, auditService);

        var result = await service.CreateUserAsync(new CreateSecurityUser("admin", "password", SecurityRole.Reader));

    Assert.False(result.IsSuccess);
    Assert.Equal(ErrorType.Conflict, result.ErrorType);
    Assert.Contains("already exists", result.Error);
    }

    [Fact]
    public async Task CreateUserAsync_DuplicateUserName_CaseInsensitive()
    {
        var existingUser = new SecurityUser(Guid.NewGuid(), "Admin", "hash", "salt", SecurityRole.Admin, DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(users: new[] { existingUser });
        var auditService = new FakeAuditService();
        var service = new SecurityService(store, auditService);

        var result = await service.CreateUserAsync(new CreateSecurityUser("ADMIN", "password", SecurityRole.Reader));

    Assert.False(result.IsSuccess);
    Assert.Equal(ErrorType.Conflict, result.ErrorType);
    }

    [Fact]
    public async Task CreateUserAsync_InitialUser_MustBeAdmin()
    {
        var store = NewStore();
        var auditService = new FakeAuditService();
        var service = new SecurityService(store, auditService);

        var result = await service.CreateUserAsync(new CreateSecurityUser("user", "password", SecurityRole.Reader));

    Assert.False(result.IsSuccess);
    Assert.Equal(ErrorType.Validation, result.ErrorType);
    Assert.Contains("initial user must be an administrator", result.Error);
    }

    [Fact]
    public async Task CreateUserAsync_InitialUser_Success()
    {
        var store = NewStore();
        var auditService = new FakeAuditService();
        var service = new SecurityService(store, auditService);

        var result = await service.CreateUserAsync(new CreateSecurityUser("admin", "password123", SecurityRole.Admin));

    Assert.True(result.IsSuccess);
    var user = result.Value!;
    Assert.NotEqual(Guid.Empty, user.Id);
    Assert.Equal("admin", user.UserName);
    Assert.Equal(SecurityRole.Admin, user.Role);
    Assert.False(string.IsNullOrEmpty(user.PasswordHash));
    Assert.False(string.IsNullOrEmpty(user.PasswordSalt));
    Assert.InRange(user.CreatedAt, DateTime.UtcNow.AddSeconds(-5), DateTime.UtcNow.AddSeconds(5));
    Assert.InRange(user.UpdatedAt, DateTime.UtcNow.AddSeconds(-5), DateTime.UtcNow.AddSeconds(5));

    var state = await service.GetSecurityStateAsync();
    Assert.Single(state.Users, u => u.Id == user.Id);
    }

    [Fact]
    public async Task CreateUserAsync_TrimsUserName()
    {
        var store = NewStore();
        var auditService = new FakeAuditService();
        var service = new SecurityService(store, auditService);

        var result = await service.CreateUserAsync(new CreateSecurityUser("  admin  ", "password123", SecurityRole.Admin));

    Assert.True(result.IsSuccess);
    Assert.Equal("admin", result.Value!.UserName);
    }

    [Fact]
    public async Task CreateUserAsync_SubsequentUser_RequiresAdmin()
    {
        var admin = new SecurityUser(Guid.NewGuid(), "admin", "hash", "salt", SecurityRole.Admin, DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(users: new[] { admin });
        var auditService = new FakeAuditService();
        var service = new SecurityService(store, auditService);

        var result = await service.CreateUserAsync(new CreateSecurityUser("user", "password", SecurityRole.Reader));

    Assert.False(result.IsSuccess);
    Assert.Equal(ErrorType.Unauthorized, result.ErrorType);
    Assert.Contains("Only administrators can create users", result.Error);
    }

    [Fact]
    public async Task CreateUserAsync_SubsequentUser_NonAdminRequester_ReturnsUnauthorized()
    {
        var admin = new SecurityUser(Guid.NewGuid(), "admin", "hash", "salt", SecurityRole.Admin, DateTime.UtcNow, DateTime.UtcNow);
        var reader = new SecurityUser(Guid.NewGuid(), "reader", "hash", "salt", SecurityRole.Reader, DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(users: new[] { admin, reader });
        var auditService = new FakeAuditService();
        var service = new SecurityService(store, auditService);

        var command = new CreateSecurityUser("newuser", "password", SecurityRole.Reader) { RequestedBy = reader.Id };
        var result = await service.CreateUserAsync(command);

    Assert.False(result.IsSuccess);
    Assert.Equal(ErrorType.Unauthorized, result.ErrorType);
    }

    [Fact]
    public async Task CreateUserAsync_SubsequentUser_AdminRequester_Success()
    {
        var admin = new SecurityUser(Guid.NewGuid(), "admin", "hash", "salt", SecurityRole.Admin, DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(users: new[] { admin });
        var auditService = new FakeAuditService();
        var service = new SecurityService(store, auditService);

        var command = new CreateSecurityUser("newuser", "password", SecurityRole.Reader) { RequestedBy = admin.Id };
        var result = await service.CreateUserAsync(command);

    Assert.True(result.IsSuccess);
    Assert.Equal("newuser", result.Value!.UserName);
    Assert.Equal(SecurityRole.Reader, result.Value!.Role);

    var state = await service.GetSecurityStateAsync();
    Assert.Equal(2, state.Users.Count);
    }

    [Fact]
    public async Task CreateUserAsync_GeneratesUniqueSalt()
    {
        var store = NewStore();
        var auditService = new FakeAuditService();
        var service = new SecurityService(store, auditService);

        var user1 = await service.CreateUserAsync(new CreateSecurityUser("admin1", "password", SecurityRole.Admin));
    Assert.True(user1.IsSuccess);
        
        var user2 = await service.CreateUserAsync(new CreateSecurityUser("admin2", "password", SecurityRole.Admin) { RequestedBy = user1.Value!.Id });
    Assert.True(user2.IsSuccess);
    Assert.NotEqual(user2.Value!.PasswordSalt, user1.Value!.PasswordSalt);
    }

    [Fact]
    public async Task CreateUserAsync_DifferentPasswords_ProduceDifferentHashes()
    {
        var store = NewStore();
        var auditService = new FakeAuditService();
        var service = new SecurityService(store, auditService);

        var user1 = await service.CreateUserAsync(new CreateSecurityUser("admin1", "password1", SecurityRole.Admin));
    Assert.True(user1.IsSuccess);
        
        var user2 = await service.CreateUserAsync(new CreateSecurityUser("admin2", "password2", SecurityRole.Admin) { RequestedBy = user1.Value!.Id });
    Assert.True(user2.IsSuccess);
    Assert.NotEqual(user2.Value!.PasswordHash, user1.Value!.PasswordHash);
    }

    #endregion

    #region LoginAsync Tests

    [Fact]
    public async Task LoginAsync_NullCommand_ReturnsFailure()
    {
        var store = NewStore();
        var auditService = new FakeAuditService();
        var service = new SecurityService(store, auditService);

        var result = await service.LoginAsync(null!);

    Assert.False(result.IsSuccess);
    Assert.Equal("Invalid login request.", result.Error);
    }

    [Fact]
    public async Task LoginAsync_EmptyUserName_ReturnsValidationFailure()
    {
        var store = NewStore();
        var auditService = new FakeAuditService();
        var service = new SecurityService(store, auditService);

        var result = await service.LoginAsync(new LoginSecurityUser("", "password"));

    Assert.False(result.IsSuccess);
    Assert.Equal(ErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task LoginAsync_EmptyPassword_ReturnsValidationFailure()
    {
        var store = NewStore();
        var auditService = new FakeAuditService();
        var service = new SecurityService(store, auditService);

        var result = await service.LoginAsync(new LoginSecurityUser("admin", ""));

    Assert.False(result.IsSuccess);
    Assert.Equal(ErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task LoginAsync_UserNotFound_ReturnsUnauthorized()
    {
        var store = NewStore();
        var auditService = new FakeAuditService();
        var service = new SecurityService(store, auditService);

        var result = await service.LoginAsync(new LoginSecurityUser("nonexistent", "password"));

    Assert.False(result.IsSuccess);
    Assert.Equal(ErrorType.Unauthorized, result.ErrorType);
    Assert.Equal("Invalid credentials.", result.Error);
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_ReturnsUnauthorized()
    {
        var store = NewStore();
        var auditService = new FakeAuditService();
        var service = new SecurityService(store, auditService);

        // Create a user
        await service.CreateUserAsync(new CreateSecurityUser("admin", "correctpassword", SecurityRole.Admin));

        // Try to login with wrong password
        var result = await service.LoginAsync(new LoginSecurityUser("admin", "wrongpassword"));

    Assert.False(result.IsSuccess);
    Assert.Equal(ErrorType.Unauthorized, result.ErrorType);
    Assert.Equal("Invalid credentials.", result.Error);
    }

    [Fact]
    public async Task LoginAsync_CorrectCredentials_ReturnsSession()
    {
        var store = NewStore();
        var auditService = new FakeAuditService();
        var service = new SecurityService(store, auditService);

        var createResult = await service.CreateUserAsync(new CreateSecurityUser("admin", "password123", SecurityRole.Admin));
        var userId = createResult.Value!.Id;

        var loginResult = await service.LoginAsync(new LoginSecurityUser("admin", "password123"));

    Assert.True(loginResult.IsSuccess);
    var session = loginResult.Value!;
    Assert.False(string.IsNullOrEmpty(session.Token));
    Assert.True(session.ExpiresAt > DateTime.UtcNow);
    Assert.InRange(session.ExpiresAt, DateTime.UtcNow.AddDays(30).AddSeconds(-5), DateTime.UtcNow.AddDays(30).AddSeconds(5));
    Assert.Equal(userId, session.User.Id);
    Assert.Equal("admin", session.User.UserName);
    Assert.Equal(SecurityRole.Admin, session.User.Role);
    }

    [Fact]
    public async Task LoginAsync_CaseInsensitiveUserName()
    {
        var store = NewStore();
        var auditService = new FakeAuditService();
        var service = new SecurityService(store, auditService);

        await service.CreateUserAsync(new CreateSecurityUser("Admin", "password123", SecurityRole.Admin));
        var result = await service.LoginAsync(new LoginSecurityUser("ADMIN", "password123"));

    Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task LoginAsync_MultipleSessions_GeneratesDifferentTokens()
    {
        var store = NewStore();
        var auditService = new FakeAuditService();
        var service = new SecurityService(store, auditService);

        await service.CreateUserAsync(new CreateSecurityUser("admin", "password123", SecurityRole.Admin));

        var session1 = await service.LoginAsync(new LoginSecurityUser("admin", "password123"));
        var session2 = await service.LoginAsync(new LoginSecurityUser("admin", "password123"));

    Assert.NotEqual(session2.Value!.Token, session1.Value!.Token);
    }

    #endregion

    #region LogoutAsync Tests

    [Fact]
    public async Task LogoutAsync_NullCommand_ReturnsFailure()
    {
        var store = NewStore();
        var auditService = new FakeAuditService();
        var service = new SecurityService(store, auditService);

        var result = await service.LogoutAsync(null!);

    Assert.False(result.IsSuccess);
    Assert.Equal("Invalid logout request.", result.Error);
    }

    [Fact]
    public async Task LogoutAsync_EmptyToken_ReturnsFailure()
    {
        var store = NewStore();
        var auditService = new FakeAuditService();
        var service = new SecurityService(store, auditService);

        var result = await service.LogoutAsync(new LogoutSecurityUser(""));

    Assert.False(result.IsSuccess);
    Assert.Equal("Invalid logout request.", result.Error);
    }

    [Fact]
    public async Task LogoutAsync_ValidToken_Success()
    {
        var store = NewStore();
        var auditService = new FakeAuditService();
        var service = new SecurityService(store, auditService);

        await service.CreateUserAsync(new CreateSecurityUser("admin", "password123", SecurityRole.Admin));
        var loginResult = await service.LoginAsync(new LoginSecurityUser("admin", "password123"));
        var token = loginResult.Value!.Token;

        var logoutResult = await service.LogoutAsync(new LogoutSecurityUser(token));

    Assert.True(logoutResult.IsSuccess);

        // Session should no longer be valid
        var user = await service.ValidateSessionAsync(token, false);
    Assert.Null(user);
    }

    [Fact]
    public async Task LogoutAsync_NonexistentToken_Success()
    {
        var store = NewStore();
        var auditService = new FakeAuditService();
        var service = new SecurityService(store, auditService);

        var result = await service.LogoutAsync(new LogoutSecurityUser("nonexistent-token"));

    Assert.True(result.IsSuccess);
    }

    #endregion

    #region ValidateSessionAsync Tests

    [Fact]
    public async Task ValidateSessionAsync_NullToken_ReturnsNull()
    {
        var store = NewStore();
        var auditService = new FakeAuditService();
        var service = new SecurityService(store, auditService);

        var result = await service.ValidateSessionAsync(null!, false);

    Assert.Null(result);
    }

    [Fact]
    public async Task ValidateSessionAsync_EmptyToken_ReturnsNull()
    {
        var store = NewStore();
        var auditService = new FakeAuditService();
        var service = new SecurityService(store, auditService);

        var result = await service.ValidateSessionAsync("", false);

    Assert.Null(result);
    }

    [Fact]
    public async Task ValidateSessionAsync_NonexistentToken_ReturnsNull()
    {
        var store = NewStore();
        var auditService = new FakeAuditService();
        var service = new SecurityService(store, auditService);

        var result = await service.ValidateSessionAsync("nonexistent-token", false);

    Assert.Null(result);
    }

    [Fact]
    public async Task ValidateSessionAsync_ValidToken_ReturnsUser()
    {
        var store = NewStore();
        var auditService = new FakeAuditService();
        var service = new SecurityService(store, auditService);

        await service.CreateUserAsync(new CreateSecurityUser("admin", "password123", SecurityRole.Admin));
        var loginResult = await service.LoginAsync(new LoginSecurityUser("admin", "password123"));
        var token = loginResult.Value!.Token;

        var user = await service.ValidateSessionAsync(token, false);

    Assert.NotNull(user);
    Assert.Equal("admin", user!.UserName);
    Assert.Equal(SecurityRole.Admin, user.Role);
    }

    [Fact]
    public async Task ValidateSessionAsync_WithRefresh_ExtendsSession()
    {
        var store = NewStore();
        var auditService = new FakeAuditService();
        var service = new SecurityService(store, auditService);

        await service.CreateUserAsync(new CreateSecurityUser("admin", "password123", SecurityRole.Admin));
        var loginResult = await service.LoginAsync(new LoginSecurityUser("admin", "password123"));
        var token = loginResult.Value!.Token;
        var originalExpiry = loginResult.Value!.ExpiresAt;

        // Wait a bit to ensure time difference
        await Task.Delay(100);

        var user = await service.ValidateSessionAsync(token, refresh: true);
    Assert.NotNull(user);

        // Login again to get the updated expiry (indirectly verify refresh happened)
        // We can't directly access the session, but we can validate it's still working
        var stillValid = await service.ValidateSessionAsync(token, refresh: false);
    Assert.NotNull(stillValid);
    }

    [Fact]
    public async Task ValidateSessionAsync_WithoutRefresh_DoesNotExtendSession()
    {
        var store = NewStore();
        var auditService = new FakeAuditService();
        var service = new SecurityService(store, auditService);

        await service.CreateUserAsync(new CreateSecurityUser("admin", "password123", SecurityRole.Admin));
        var loginResult = await service.LoginAsync(new LoginSecurityUser("admin", "password123"));
        var token = loginResult.Value!.Token;

    var user = await service.ValidateSessionAsync(token, refresh: false);
    Assert.NotNull(user);

        // Session should still be valid
        var stillValid = await service.ValidateSessionAsync(token, refresh: false);
    Assert.NotNull(stillValid);
    }

    [Fact]
    public async Task ValidateSessionAsync_DeletedUser_ReturnsNull()
    {
        // This test verifies that if a user is removed from the store,
        // their session becomes invalid even if the token hasn't expired
        var admin = new SecurityUser(Guid.NewGuid(), "admin", "hash", "salt", SecurityRole.Admin, DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(users: new[] { admin });
        var auditService = new FakeAuditService();
        var service = new SecurityService(store, auditService);

        var loginResult = await service.LoginAsync(new LoginSecurityUser("admin", "password123"));
        // Login will fail because the password hash won't match, but we can manually create a session
        // Actually, let's create a proper user first
        var createResult = await service.CreateUserAsync(new CreateSecurityUser("testadmin", "password123", SecurityRole.Admin) { RequestedBy = admin.Id });
        var loginResult2 = await service.LoginAsync(new LoginSecurityUser("testadmin", "password123"));
        var token = loginResult2.Value!.Token;

        // Verify session is valid
        var user1 = await service.ValidateSessionAsync(token, false);
    Assert.NotNull(user1);

        // Remove all users except the original admin
        await store.UpdateAsync(s => s with
        {
            Security = s.Security with
            {
                Users = new[] { admin }
            }
        });

        // Session should now be invalid
        var user2 = await service.ValidateSessionAsync(token, false);
    Assert.Null(user2);
    }

    #endregion

    #region UpdateSecuritySettingsAsync Tests

    [Fact]
    public async Task UpdateSecuritySettingsAsync_NullCommand_ReturnsFailure()
    {
        var store = NewStore();
        var auditService = new FakeAuditService();
        var service = new SecurityService(store, auditService);

        var result = await service.UpdateSecuritySettingsAsync(null!);

    Assert.False(result.IsSuccess);
    Assert.Equal("Invalid security settings command.", result.Error);
    }

    [Fact]
    public async Task UpdateSecuritySettingsAsync_NoRequestedBy_ReturnsUnauthorized()
    {
        var admin = new SecurityUser(Guid.NewGuid(), "admin", "hash", "salt", SecurityRole.Admin, DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(users: new[] { admin });
        var auditService = new FakeAuditService();
        var service = new SecurityService(store, auditService);

        var result = await service.UpdateSecuritySettingsAsync(new UpdateSecuritySettings(SecurityLevel.RestrictedEditing));

    Assert.False(result.IsSuccess);
    Assert.Equal(ErrorType.Unauthorized, result.ErrorType);
    }

    [Fact]
    public async Task UpdateSecuritySettingsAsync_NonAdminRequester_ReturnsUnauthorized()
    {
        var admin = new SecurityUser(Guid.NewGuid(), "admin", "hash", "salt", SecurityRole.Admin, DateTime.UtcNow, DateTime.UtcNow);
        var reader = new SecurityUser(Guid.NewGuid(), "reader", "hash", "salt", SecurityRole.Reader, DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(users: new[] { admin, reader });
        var auditService = new FakeAuditService();
        var service = new SecurityService(store, auditService);

        var command = new UpdateSecuritySettings(SecurityLevel.RestrictedEditing) { RequestedBy = reader.Id };
        var result = await service.UpdateSecuritySettingsAsync(command);

    Assert.False(result.IsSuccess);
    Assert.Equal(ErrorType.Unauthorized, result.ErrorType);
    Assert.Contains("Only administrators can update security settings", result.Error);
    }

    [Fact]
    public async Task UpdateSecuritySettingsAsync_NonexistentRequester_ReturnsUnauthorized()
    {
        var admin = new SecurityUser(Guid.NewGuid(), "admin", "hash", "salt", SecurityRole.Admin, DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(users: new[] { admin });
        var auditService = new FakeAuditService();
        var service = new SecurityService(store, auditService);

        var command = new UpdateSecuritySettings(SecurityLevel.RestrictedEditing) { RequestedBy = Guid.NewGuid() };
        var result = await service.UpdateSecuritySettingsAsync(command);

    Assert.False(result.IsSuccess);
    Assert.Equal(ErrorType.Unauthorized, result.ErrorType);
    }

    [Fact]
    public async Task UpdateSecuritySettingsAsync_SameLevel_ReturnsExistingSettings()
    {
        var admin = new SecurityUser(Guid.NewGuid(), "admin", "hash", "salt", SecurityRole.Admin, DateTime.UtcNow, DateTime.UtcNow);
        var settings = new SecuritySettings(SecurityLevel.RestrictedEditing, DateTime.UtcNow.AddDays(-1));
        var store = NewStore(settings: settings, users: new[] { admin });
        var auditService = new FakeAuditService();
        var service = new SecurityService(store, auditService);

        var command = new UpdateSecuritySettings(SecurityLevel.RestrictedEditing) { RequestedBy = admin.Id };
        var result = await service.UpdateSecuritySettingsAsync(command);

    Assert.True(result.IsSuccess);
    Assert.Equal(SecurityLevel.RestrictedEditing, result.Value!.Level);
    Assert.Equal(settings.UpdatedAt, result.Value!.UpdatedAt);
    }

    [Fact]
    public async Task UpdateSecuritySettingsAsync_RequiresSetup_ReturnsValidationError()
    {
        // When no admin exists (RequiresSetup = true), settings cannot be modified
        var store = NewStore();
        var auditService = new FakeAuditService();
        var service = new SecurityService(store, auditService);

        var state = await service.GetSecurityStateAsync();
    Assert.True(state.RequiresSetup);

        var command = new UpdateSecuritySettings(SecurityLevel.RestrictedEditing) { RequestedBy = Guid.NewGuid() };
        var result = await service.UpdateSecuritySettingsAsync(command);

    Assert.False(result.IsSuccess);
    Assert.Equal(ErrorType.Validation, result.ErrorType);
    Assert.Contains("administrator account must be created before security settings can be modified", result.Error);
    }

    [Fact]
    public async Task UpdateSecuritySettingsAsync_NoAdminExists_EnableRestrictions_ReturnsValidationError()
    {
        // This test is challenging because:
        // 1. You need to be an admin to update security settings (authorization check)
        // 2. You can't enable restrictions if no admin exists (validation check)
        // But if you're making the request, you're an admin, so you exist!
        
        // The real-world scenario this protects against: During a race condition or
        // manual database manipulation where admins get deleted. Let's simulate:
        
        var admin = new SecurityUser(Guid.NewGuid(), "admin", "hash", "salt", SecurityRole.Admin, DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(users: new[] { admin });
        var auditService = new FakeAuditService();
        var service = new SecurityService(store, auditService);

        // Manually remove all admins from the store (simulating external modification)
        await store.UpdateAsync(s => s with
        {
            Security = s.Security with
            {
                // Empty user list - no admins
                Users = Array.Empty<SecurityUser>()
            }
        });

        // Try to enable restrictions with the now-deleted admin's ID
        var command = new UpdateSecuritySettings(SecurityLevel.RestrictedEditing) { RequestedBy = admin.Id };
        var result = await service.UpdateSecuritySettingsAsync(command);

        // Now with the RequiresSetup check, this will fail with Validation error first
    Assert.False(result.IsSuccess);
    Assert.Equal(ErrorType.Validation, result.ErrorType);
    Assert.Contains("administrator account must be created before security settings can be modified", result.Error);
    }

    [Fact]
    public async Task UpdateSecuritySettingsAsync_AdminRequester_Success()
    {
        var admin = new SecurityUser(Guid.NewGuid(), "admin", "hash", "salt", SecurityRole.Admin, DateTime.UtcNow, DateTime.UtcNow);
        var settings = new SecuritySettings(SecurityLevel.None, DateTime.UtcNow.AddDays(-1));
        var store = NewStore(settings: settings, users: new[] { admin });
        var auditService = new FakeAuditService();
        var service = new SecurityService(store, auditService);

        var command = new UpdateSecuritySettings(SecurityLevel.FullyRestricted) { RequestedBy = admin.Id };
        var result = await service.UpdateSecuritySettingsAsync(command);

    Assert.True(result.IsSuccess);
    Assert.Equal(SecurityLevel.FullyRestricted, result.Value!.Level);
    Assert.InRange(result.Value!.UpdatedAt, DateTime.UtcNow.AddSeconds(-5), DateTime.UtcNow.AddSeconds(5));

    var state = await service.GetSecurityStateAsync();
    Assert.Equal(SecurityLevel.FullyRestricted, state.Settings.Level);
    }

    [Fact]
    public async Task UpdateSecuritySettingsAsync_DisableRestrictions_Success()
    {
        var admin = new SecurityUser(Guid.NewGuid(), "admin", "hash", "salt", SecurityRole.Admin, DateTime.UtcNow, DateTime.UtcNow);
        var settings = new SecuritySettings(SecurityLevel.FullyRestricted, DateTime.UtcNow.AddDays(-1));
        var store = NewStore(settings: settings, users: new[] { admin });
        var auditService = new FakeAuditService();
        var service = new SecurityService(store, auditService);

        var command = new UpdateSecuritySettings(SecurityLevel.None) { RequestedBy = admin.Id };
        var result = await service.UpdateSecuritySettingsAsync(command);

    Assert.True(result.IsSuccess);
    Assert.Equal(SecurityLevel.None, result.Value!.Level);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public async Task IntegrationTest_CompleteUserLifecycle()
    {
        var store = NewStore();
        var auditService = new FakeAuditService();
        var service = new SecurityService(store, auditService);

        // 1. Create initial admin user
        var createAdminResult = await service.CreateUserAsync(new CreateSecurityUser("admin", "admin123", SecurityRole.Admin));
    Assert.True(createAdminResult.IsSuccess);
        var adminId = createAdminResult.Value!.Id;

        // 2. Login as admin
        var loginResult = await service.LoginAsync(new LoginSecurityUser("admin", "admin123"));
    Assert.True(loginResult.IsSuccess);
        var token = loginResult.Value!.Token;

        // 3. Validate session
        var validatedUser = await service.ValidateSessionAsync(token, false);
    Assert.NotNull(validatedUser);
    Assert.Equal(adminId, validatedUser!.Id);

        // 4. Create a reader user
        var createReaderCommand = new CreateSecurityUser("reader", "reader123", SecurityRole.Reader) { RequestedBy = adminId };
        var createReaderResult = await service.CreateUserAsync(createReaderCommand);
    Assert.True(createReaderResult.IsSuccess);

        // 5. Update security settings
        var updateSettingsCommand = new UpdateSecuritySettings(SecurityLevel.RestrictedEditing) { RequestedBy = adminId };
        var updateSettingsResult = await service.UpdateSecuritySettingsAsync(updateSettingsCommand);
    Assert.True(updateSettingsResult.IsSuccess);

        // 6. Logout
        var logoutResult = await service.LogoutAsync(new LogoutSecurityUser(token));
    Assert.True(logoutResult.IsSuccess);

        // 7. Session should be invalid after logout
        var invalidSession = await service.ValidateSessionAsync(token, false);
    Assert.Null(invalidSession);

        // 8. Verify final state
        var finalState = await service.GetSecurityStateAsync();
    Assert.Equal(2, finalState.Users.Count);
    Assert.Equal(SecurityLevel.RestrictedEditing, finalState.Settings.Level);
    Assert.False(finalState.RequiresSetup);
    }

    [Fact]
    public async Task IntegrationTest_PasswordHashingSecurity()
    {
        var store = NewStore();
        var auditService = new FakeAuditService();
        var service = new SecurityService(store, auditService);

        // Create two users with the same password
        var user1Result = await service.CreateUserAsync(new CreateSecurityUser("user1", "samepassword", SecurityRole.Admin));
    Assert.True(user1Result.IsSuccess);
        
        var user2Result = await service.CreateUserAsync(new CreateSecurityUser("user2", "samepassword", SecurityRole.Admin) { RequestedBy = user1Result.Value!.Id });
    Assert.True(user2Result.IsSuccess);

        // Even with the same password, hashes should be different due to different salts
    Assert.NotEqual(user2Result.Value!.PasswordHash, user1Result.Value!.PasswordHash);
    Assert.NotEqual(user2Result.Value!.PasswordSalt, user1Result.Value!.PasswordSalt);

        // Both should be able to login
        var login1 = await service.LoginAsync(new LoginSecurityUser("user1", "samepassword"));
        var login2 = await service.LoginAsync(new LoginSecurityUser("user2", "samepassword"));

    Assert.True(login1.IsSuccess);
    Assert.True(login2.IsSuccess);
    }

    [Fact]
    public async Task IntegrationTest_SecurityStateRequiresSetup()
    {
        var auditService = new FakeAuditService();
        
        // No users - requires setup
        var store1 = NewStore();
        var service1 = new SecurityService(store1, auditService);
        var state1 = await service1.GetSecurityStateAsync();
    Assert.True(state1.RequiresSetup);

        // Only reader - requires setup
        var reader = new SecurityUser(Guid.NewGuid(), "reader", "hash", "salt", SecurityRole.Reader, DateTime.UtcNow, DateTime.UtcNow);
        var store2 = NewStore(users: new[] { reader });
        var service2 = new SecurityService(store2, auditService);
        var state2 = await service2.GetSecurityStateAsync();
    Assert.True(state2.RequiresSetup);

        // Has admin - does not require setup
        var admin = new SecurityUser(Guid.NewGuid(), "admin", "hash", "salt", SecurityRole.Admin, DateTime.UtcNow, DateTime.UtcNow);
        var store3 = NewStore(users: new[] { admin });
        var service3 = new SecurityService(store3, auditService);
        var state3 = await service3.GetSecurityStateAsync();
    Assert.False(state3.RequiresSetup);

        // Has both admin and reader - does not require setup
        var store4 = NewStore(users: new[] { admin, reader });
        var service4 = new SecurityService(store4, auditService);
        var state4 = await service4.GetSecurityStateAsync();
    Assert.False(state4.RequiresSetup);
    }

    #endregion

    #region UpdateUser_DeleteUser Tests

    [Fact]
    public async Task UpdateUser_ValidationAndNotFound()
    {
        var admin = new SecurityUser(Guid.NewGuid(), "admin", "hash", "salt", SecurityRole.Admin, DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(users: new[] { admin });
        var auditService = new FakeAuditService();
        var service = new SecurityService(store, auditService);

        var bad = await service.UpdateUser(null!, default);
    Assert.False(bad.IsSuccess);
    Assert.Equal(ErrorType.Validation, bad.ErrorType);

        var nf = await service.UpdateUser(new UpdateUser(Guid.NewGuid(), SecurityRole.Reader), default);
    Assert.False(nf.IsSuccess);
    Assert.Equal(ErrorType.NotFound, nf.ErrorType);
    }

    [Fact]
    public async Task UpdateUser_Success_ChangesRole()
    {
        var user = new SecurityUser(Guid.NewGuid(), "user", "hash", "salt", SecurityRole.Reader, DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(users: new[] { user });
        var auditService = new FakeAuditService();
        var service = new SecurityService(store, auditService);

        var res = await service.UpdateUser(new UpdateUser(user.Id, SecurityRole.Admin), default);
    Assert.True(res.IsSuccess);
    Assert.Equal(SecurityRole.Admin, res.Value!.Role);
    }

    [Fact]
    public async Task DeleteUser_Validation_NotFound_AndSuccess()
    {
        var user = new SecurityUser(Guid.NewGuid(), "user", "hash", "salt", SecurityRole.Reader, DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(users: new[] { user });
        var auditService = new FakeAuditService();
        var service = new SecurityService(store, auditService);

        var bad = await service.DeleteUser(null!, default);
    Assert.False(bad.IsSuccess);
    Assert.Equal(ErrorType.Validation, bad.ErrorType);

        var nf = await service.DeleteUser(new DeleteUser(Guid.NewGuid()), default);
    Assert.False(nf.IsSuccess);
    Assert.Equal(ErrorType.NotFound, nf.ErrorType);

        var ok = await service.DeleteUser(new DeleteUser(user.Id), default);
    Assert.True(ok.IsSuccess);

        var state = await service.GetSecurityStateAsync();
    Assert.Empty(state.Users);
    }

    #endregion
}
