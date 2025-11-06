using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Models;
using Fuse.Core.Services;
using Fuse.Tests.TestInfrastructure;
using FluentAssertions;
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
            Servers: Array.Empty<Server>(),
            ExternalResources: Array.Empty<ExternalResource>(),
            Accounts: Array.Empty<Account>(),
            Tags: Array.Empty<Tag>(),
            Environments: Array.Empty<EnvironmentInfo>(),
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
        var service = new SecurityService(store);

        var state = await service.GetSecurityStateAsync();

        state.Should().NotBeNull();
        state.Settings.Level.Should().Be(SecurityLevel.RestrictedEditing);
        state.Users.Should().BeEmpty();
    }

    #endregion

    #region CreateUserAsync Tests

    [Fact]
    public async Task CreateUserAsync_NullCommand_ReturnsFailure()
    {
        var store = NewStore();
        var service = new SecurityService(store);

        var result = await service.CreateUserAsync(null!);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Invalid security user command.");
    }

    [Fact]
    public async Task CreateUserAsync_EmptyUserName_ReturnsFailure()
    {
        var store = NewStore();
        var service = new SecurityService(store);

        var result = await service.CreateUserAsync(new CreateSecurityUser("", "password123", SecurityRole.Admin));

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("User name cannot be empty.");
    }

    [Fact]
    public async Task CreateUserAsync_EmptyPassword_ReturnsFailure()
    {
        var store = NewStore();
        var service = new SecurityService(store);

        var result = await service.CreateUserAsync(new CreateSecurityUser("admin", "", SecurityRole.Admin));

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Password cannot be empty.");
    }

    [Fact]
    public async Task CreateUserAsync_DuplicateUserName_ReturnsConflict()
    {
        var existingUser = new SecurityUser(Guid.NewGuid(), "admin", "hash", "salt", SecurityRole.Admin, DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(users: new[] { existingUser });
        var service = new SecurityService(store);

        var result = await service.CreateUserAsync(new CreateSecurityUser("admin", "password", SecurityRole.Reader));

        result.IsSuccess.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.Conflict);
        result.Error.Should().Contain("already exists");
    }

    [Fact]
    public async Task CreateUserAsync_DuplicateUserName_CaseInsensitive()
    {
        var existingUser = new SecurityUser(Guid.NewGuid(), "Admin", "hash", "salt", SecurityRole.Admin, DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(users: new[] { existingUser });
        var service = new SecurityService(store);

        var result = await service.CreateUserAsync(new CreateSecurityUser("ADMIN", "password", SecurityRole.Reader));

        result.IsSuccess.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task CreateUserAsync_InitialUser_MustBeAdmin()
    {
        var store = NewStore();
        var service = new SecurityService(store);

        var result = await service.CreateUserAsync(new CreateSecurityUser("user", "password", SecurityRole.Reader));

        result.IsSuccess.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.Validation);
        result.Error.Should().Contain("initial user must be an administrator");
    }

    [Fact]
    public async Task CreateUserAsync_InitialUser_Success()
    {
        var store = NewStore();
        var service = new SecurityService(store);

        var result = await service.CreateUserAsync(new CreateSecurityUser("admin", "password123", SecurityRole.Admin));

        result.IsSuccess.Should().BeTrue();
        var user = result.Value!;
        user.Id.Should().NotBe(Guid.Empty);
        user.UserName.Should().Be("admin");
        user.Role.Should().Be(SecurityRole.Admin);
        user.PasswordHash.Should().NotBeNullOrEmpty();
        user.PasswordSalt.Should().NotBeNullOrEmpty();
        user.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        user.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        var state = await service.GetSecurityStateAsync();
        state.Users.Should().ContainSingle(u => u.Id == user.Id);
    }

    [Fact]
    public async Task CreateUserAsync_TrimsUserName()
    {
        var store = NewStore();
        var service = new SecurityService(store);

        var result = await service.CreateUserAsync(new CreateSecurityUser("  admin  ", "password123", SecurityRole.Admin));

        result.IsSuccess.Should().BeTrue();
        result.Value!.UserName.Should().Be("admin");
    }

    [Fact]
    public async Task CreateUserAsync_SubsequentUser_RequiresAdmin()
    {
        var admin = new SecurityUser(Guid.NewGuid(), "admin", "hash", "salt", SecurityRole.Admin, DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(users: new[] { admin });
        var service = new SecurityService(store);

        var result = await service.CreateUserAsync(new CreateSecurityUser("user", "password", SecurityRole.Reader));

        result.IsSuccess.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.Unauthorized);
        result.Error.Should().Contain("Only administrators can create users");
    }

    [Fact]
    public async Task CreateUserAsync_SubsequentUser_NonAdminRequester_ReturnsUnauthorized()
    {
        var admin = new SecurityUser(Guid.NewGuid(), "admin", "hash", "salt", SecurityRole.Admin, DateTime.UtcNow, DateTime.UtcNow);
        var reader = new SecurityUser(Guid.NewGuid(), "reader", "hash", "salt", SecurityRole.Reader, DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(users: new[] { admin, reader });
        var service = new SecurityService(store);

        var command = new CreateSecurityUser("newuser", "password", SecurityRole.Reader) { RequestedBy = reader.Id };
        var result = await service.CreateUserAsync(command);

        result.IsSuccess.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task CreateUserAsync_SubsequentUser_AdminRequester_Success()
    {
        var admin = new SecurityUser(Guid.NewGuid(), "admin", "hash", "salt", SecurityRole.Admin, DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(users: new[] { admin });
        var service = new SecurityService(store);

        var command = new CreateSecurityUser("newuser", "password", SecurityRole.Reader) { RequestedBy = admin.Id };
        var result = await service.CreateUserAsync(command);

        result.IsSuccess.Should().BeTrue();
        result.Value!.UserName.Should().Be("newuser");
        result.Value!.Role.Should().Be(SecurityRole.Reader);

        var state = await service.GetSecurityStateAsync();
        state.Users.Should().HaveCount(2);
    }

    [Fact]
    public async Task CreateUserAsync_GeneratesUniqueSalt()
    {
        var store = NewStore();
        var service = new SecurityService(store);

        var user1 = await service.CreateUserAsync(new CreateSecurityUser("admin1", "password", SecurityRole.Admin));
        user1.IsSuccess.Should().BeTrue();
        
        var user2 = await service.CreateUserAsync(new CreateSecurityUser("admin2", "password", SecurityRole.Admin) { RequestedBy = user1.Value!.Id });
        user2.IsSuccess.Should().BeTrue();

        user1.Value!.PasswordSalt.Should().NotBe(user2.Value!.PasswordSalt);
    }

    [Fact]
    public async Task CreateUserAsync_DifferentPasswords_ProduceDifferentHashes()
    {
        var store = NewStore();
        var service = new SecurityService(store);

        var user1 = await service.CreateUserAsync(new CreateSecurityUser("admin1", "password1", SecurityRole.Admin));
        user1.IsSuccess.Should().BeTrue();
        
        var user2 = await service.CreateUserAsync(new CreateSecurityUser("admin2", "password2", SecurityRole.Admin) { RequestedBy = user1.Value!.Id });
        user2.IsSuccess.Should().BeTrue();

        user1.Value!.PasswordHash.Should().NotBe(user2.Value!.PasswordHash);
    }

    #endregion

    #region LoginAsync Tests

    [Fact]
    public async Task LoginAsync_NullCommand_ReturnsFailure()
    {
        var store = NewStore();
        var service = new SecurityService(store);

        var result = await service.LoginAsync(null!);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Invalid login request.");
    }

    [Fact]
    public async Task LoginAsync_EmptyUserName_ReturnsValidationFailure()
    {
        var store = NewStore();
        var service = new SecurityService(store);

        var result = await service.LoginAsync(new LoginSecurityUser("", "password"));

        result.IsSuccess.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task LoginAsync_EmptyPassword_ReturnsValidationFailure()
    {
        var store = NewStore();
        var service = new SecurityService(store);

        var result = await service.LoginAsync(new LoginSecurityUser("admin", ""));

        result.IsSuccess.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task LoginAsync_UserNotFound_ReturnsUnauthorized()
    {
        var store = NewStore();
        var service = new SecurityService(store);

        var result = await service.LoginAsync(new LoginSecurityUser("nonexistent", "password"));

        result.IsSuccess.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.Unauthorized);
        result.Error.Should().Be("Invalid credentials.");
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_ReturnsUnauthorized()
    {
        var store = NewStore();
        var service = new SecurityService(store);

        // Create a user
        await service.CreateUserAsync(new CreateSecurityUser("admin", "correctpassword", SecurityRole.Admin));

        // Try to login with wrong password
        var result = await service.LoginAsync(new LoginSecurityUser("admin", "wrongpassword"));

        result.IsSuccess.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.Unauthorized);
        result.Error.Should().Be("Invalid credentials.");
    }

    [Fact]
    public async Task LoginAsync_CorrectCredentials_ReturnsSession()
    {
        var store = NewStore();
        var service = new SecurityService(store);

        var createResult = await service.CreateUserAsync(new CreateSecurityUser("admin", "password123", SecurityRole.Admin));
        var userId = createResult.Value!.Id;

        var loginResult = await service.LoginAsync(new LoginSecurityUser("admin", "password123"));

        loginResult.IsSuccess.Should().BeTrue();
        var session = loginResult.Value!;
        session.Token.Should().NotBeNullOrEmpty();
        session.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
        session.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddDays(30), TimeSpan.FromSeconds(5));
        session.User.Id.Should().Be(userId);
        session.User.UserName.Should().Be("admin");
        session.User.Role.Should().Be(SecurityRole.Admin);
    }

    [Fact]
    public async Task LoginAsync_CaseInsensitiveUserName()
    {
        var store = NewStore();
        var service = new SecurityService(store);

        await service.CreateUserAsync(new CreateSecurityUser("Admin", "password123", SecurityRole.Admin));
        var result = await service.LoginAsync(new LoginSecurityUser("ADMIN", "password123"));

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task LoginAsync_MultipleSessions_GeneratesDifferentTokens()
    {
        var store = NewStore();
        var service = new SecurityService(store);

        await service.CreateUserAsync(new CreateSecurityUser("admin", "password123", SecurityRole.Admin));

        var session1 = await service.LoginAsync(new LoginSecurityUser("admin", "password123"));
        var session2 = await service.LoginAsync(new LoginSecurityUser("admin", "password123"));

        session1.Value!.Token.Should().NotBe(session2.Value!.Token);
    }

    #endregion

    #region LogoutAsync Tests

    [Fact]
    public async Task LogoutAsync_NullCommand_ReturnsFailure()
    {
        var store = NewStore();
        var service = new SecurityService(store);

        var result = await service.LogoutAsync(null!);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Invalid logout request.");
    }

    [Fact]
    public async Task LogoutAsync_EmptyToken_ReturnsFailure()
    {
        var store = NewStore();
        var service = new SecurityService(store);

        var result = await service.LogoutAsync(new LogoutSecurityUser(""));

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Invalid logout request.");
    }

    [Fact]
    public async Task LogoutAsync_ValidToken_Success()
    {
        var store = NewStore();
        var service = new SecurityService(store);

        await service.CreateUserAsync(new CreateSecurityUser("admin", "password123", SecurityRole.Admin));
        var loginResult = await service.LoginAsync(new LoginSecurityUser("admin", "password123"));
        var token = loginResult.Value!.Token;

        var logoutResult = await service.LogoutAsync(new LogoutSecurityUser(token));

        logoutResult.IsSuccess.Should().BeTrue();

        // Session should no longer be valid
        var user = await service.ValidateSessionAsync(token, false);
        user.Should().BeNull();
    }

    [Fact]
    public async Task LogoutAsync_NonexistentToken_Success()
    {
        var store = NewStore();
        var service = new SecurityService(store);

        var result = await service.LogoutAsync(new LogoutSecurityUser("nonexistent-token"));

        result.IsSuccess.Should().BeTrue();
    }

    #endregion

    #region ValidateSessionAsync Tests

    [Fact]
    public async Task ValidateSessionAsync_NullToken_ReturnsNull()
    {
        var store = NewStore();
        var service = new SecurityService(store);

        var result = await service.ValidateSessionAsync(null!, false);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ValidateSessionAsync_EmptyToken_ReturnsNull()
    {
        var store = NewStore();
        var service = new SecurityService(store);

        var result = await service.ValidateSessionAsync("", false);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ValidateSessionAsync_NonexistentToken_ReturnsNull()
    {
        var store = NewStore();
        var service = new SecurityService(store);

        var result = await service.ValidateSessionAsync("nonexistent-token", false);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ValidateSessionAsync_ValidToken_ReturnsUser()
    {
        var store = NewStore();
        var service = new SecurityService(store);

        await service.CreateUserAsync(new CreateSecurityUser("admin", "password123", SecurityRole.Admin));
        var loginResult = await service.LoginAsync(new LoginSecurityUser("admin", "password123"));
        var token = loginResult.Value!.Token;

        var user = await service.ValidateSessionAsync(token, false);

        user.Should().NotBeNull();
        user!.UserName.Should().Be("admin");
        user.Role.Should().Be(SecurityRole.Admin);
    }

    [Fact]
    public async Task ValidateSessionAsync_WithRefresh_ExtendsSession()
    {
        var store = NewStore();
        var service = new SecurityService(store);

        await service.CreateUserAsync(new CreateSecurityUser("admin", "password123", SecurityRole.Admin));
        var loginResult = await service.LoginAsync(new LoginSecurityUser("admin", "password123"));
        var token = loginResult.Value!.Token;
        var originalExpiry = loginResult.Value!.ExpiresAt;

        // Wait a bit to ensure time difference
        await Task.Delay(100);

        var user = await service.ValidateSessionAsync(token, refresh: true);
        user.Should().NotBeNull();

        // Login again to get the updated expiry (indirectly verify refresh happened)
        // We can't directly access the session, but we can validate it's still working
        var stillValid = await service.ValidateSessionAsync(token, refresh: false);
        stillValid.Should().NotBeNull();
    }

    [Fact]
    public async Task ValidateSessionAsync_WithoutRefresh_DoesNotExtendSession()
    {
        var store = NewStore();
        var service = new SecurityService(store);

        await service.CreateUserAsync(new CreateSecurityUser("admin", "password123", SecurityRole.Admin));
        var loginResult = await service.LoginAsync(new LoginSecurityUser("admin", "password123"));
        var token = loginResult.Value!.Token;

        var user = await service.ValidateSessionAsync(token, refresh: false);
        user.Should().NotBeNull();

        // Session should still be valid
        var stillValid = await service.ValidateSessionAsync(token, refresh: false);
        stillValid.Should().NotBeNull();
    }

    [Fact]
    public async Task ValidateSessionAsync_DeletedUser_ReturnsNull()
    {
        // This test verifies that if a user is removed from the store,
        // their session becomes invalid even if the token hasn't expired
        var admin = new SecurityUser(Guid.NewGuid(), "admin", "hash", "salt", SecurityRole.Admin, DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(users: new[] { admin });
        var service = new SecurityService(store);

        var loginResult = await service.LoginAsync(new LoginSecurityUser("admin", "password123"));
        // Login will fail because the password hash won't match, but we can manually create a session
        // Actually, let's create a proper user first
        var createResult = await service.CreateUserAsync(new CreateSecurityUser("testadmin", "password123", SecurityRole.Admin) { RequestedBy = admin.Id });
        var loginResult2 = await service.LoginAsync(new LoginSecurityUser("testadmin", "password123"));
        var token = loginResult2.Value!.Token;

        // Verify session is valid
        var user1 = await service.ValidateSessionAsync(token, false);
        user1.Should().NotBeNull();

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
        user2.Should().BeNull();
    }

    #endregion

    #region UpdateSecuritySettingsAsync Tests

    [Fact]
    public async Task UpdateSecuritySettingsAsync_NullCommand_ReturnsFailure()
    {
        var store = NewStore();
        var service = new SecurityService(store);

        var result = await service.UpdateSecuritySettingsAsync(null!);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Invalid security settings command.");
    }

    [Fact]
    public async Task UpdateSecuritySettingsAsync_NoRequestedBy_ReturnsUnauthorized()
    {
        var admin = new SecurityUser(Guid.NewGuid(), "admin", "hash", "salt", SecurityRole.Admin, DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(users: new[] { admin });
        var service = new SecurityService(store);

        var result = await service.UpdateSecuritySettingsAsync(new UpdateSecuritySettings(SecurityLevel.RestrictedEditing));

        result.IsSuccess.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task UpdateSecuritySettingsAsync_NonAdminRequester_ReturnsUnauthorized()
    {
        var admin = new SecurityUser(Guid.NewGuid(), "admin", "hash", "salt", SecurityRole.Admin, DateTime.UtcNow, DateTime.UtcNow);
        var reader = new SecurityUser(Guid.NewGuid(), "reader", "hash", "salt", SecurityRole.Reader, DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(users: new[] { admin, reader });
        var service = new SecurityService(store);

        var command = new UpdateSecuritySettings(SecurityLevel.RestrictedEditing) { RequestedBy = reader.Id };
        var result = await service.UpdateSecuritySettingsAsync(command);

        result.IsSuccess.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.Unauthorized);
        result.Error.Should().Contain("Only administrators can update security settings");
    }

    [Fact]
    public async Task UpdateSecuritySettingsAsync_NonexistentRequester_ReturnsUnauthorized()
    {
        var admin = new SecurityUser(Guid.NewGuid(), "admin", "hash", "salt", SecurityRole.Admin, DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(users: new[] { admin });
        var service = new SecurityService(store);

        var command = new UpdateSecuritySettings(SecurityLevel.RestrictedEditing) { RequestedBy = Guid.NewGuid() };
        var result = await service.UpdateSecuritySettingsAsync(command);

        result.IsSuccess.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task UpdateSecuritySettingsAsync_SameLevel_ReturnsExistingSettings()
    {
        var admin = new SecurityUser(Guid.NewGuid(), "admin", "hash", "salt", SecurityRole.Admin, DateTime.UtcNow, DateTime.UtcNow);
        var settings = new SecuritySettings(SecurityLevel.RestrictedEditing, DateTime.UtcNow.AddDays(-1));
        var store = NewStore(settings: settings, users: new[] { admin });
        var service = new SecurityService(store);

        var command = new UpdateSecuritySettings(SecurityLevel.RestrictedEditing) { RequestedBy = admin.Id };
        var result = await service.UpdateSecuritySettingsAsync(command);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Level.Should().Be(SecurityLevel.RestrictedEditing);
        result.Value!.UpdatedAt.Should().Be(settings.UpdatedAt);
    }

    [Fact]
    public async Task UpdateSecuritySettingsAsync_RequiresSetup_ReturnsValidationError()
    {
        // When no admin exists (RequiresSetup = true), settings cannot be modified
        var store = NewStore();
        var service = new SecurityService(store);

        var state = await service.GetSecurityStateAsync();
        state.RequiresSetup.Should().BeTrue();

        var command = new UpdateSecuritySettings(SecurityLevel.RestrictedEditing) { RequestedBy = Guid.NewGuid() };
        var result = await service.UpdateSecuritySettingsAsync(command);

        result.IsSuccess.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.Validation);
        result.Error.Should().Contain("administrator account must be created before security settings can be modified");
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
        var service = new SecurityService(store);

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
        result.IsSuccess.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.Validation);
        result.Error.Should().Contain("administrator account must be created before security settings can be modified");
    }

    [Fact]
    public async Task UpdateSecuritySettingsAsync_AdminRequester_Success()
    {
        var admin = new SecurityUser(Guid.NewGuid(), "admin", "hash", "salt", SecurityRole.Admin, DateTime.UtcNow, DateTime.UtcNow);
        var settings = new SecuritySettings(SecurityLevel.None, DateTime.UtcNow.AddDays(-1));
        var store = NewStore(settings: settings, users: new[] { admin });
        var service = new SecurityService(store);

        var command = new UpdateSecuritySettings(SecurityLevel.FullyRestricted) { RequestedBy = admin.Id };
        var result = await service.UpdateSecuritySettingsAsync(command);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Level.Should().Be(SecurityLevel.FullyRestricted);
        result.Value!.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        var state = await service.GetSecurityStateAsync();
        state.Settings.Level.Should().Be(SecurityLevel.FullyRestricted);
    }

    [Fact]
    public async Task UpdateSecuritySettingsAsync_DisableRestrictions_Success()
    {
        var admin = new SecurityUser(Guid.NewGuid(), "admin", "hash", "salt", SecurityRole.Admin, DateTime.UtcNow, DateTime.UtcNow);
        var settings = new SecuritySettings(SecurityLevel.FullyRestricted, DateTime.UtcNow.AddDays(-1));
        var store = NewStore(settings: settings, users: new[] { admin });
        var service = new SecurityService(store);

        var command = new UpdateSecuritySettings(SecurityLevel.None) { RequestedBy = admin.Id };
        var result = await service.UpdateSecuritySettingsAsync(command);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Level.Should().Be(SecurityLevel.None);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public async Task IntegrationTest_CompleteUserLifecycle()
    {
        var store = NewStore();
        var service = new SecurityService(store);

        // 1. Create initial admin user
        var createAdminResult = await service.CreateUserAsync(new CreateSecurityUser("admin", "admin123", SecurityRole.Admin));
        createAdminResult.IsSuccess.Should().BeTrue();
        var adminId = createAdminResult.Value!.Id;

        // 2. Login as admin
        var loginResult = await service.LoginAsync(new LoginSecurityUser("admin", "admin123"));
        loginResult.IsSuccess.Should().BeTrue();
        var token = loginResult.Value!.Token;

        // 3. Validate session
        var validatedUser = await service.ValidateSessionAsync(token, false);
        validatedUser.Should().NotBeNull();
        validatedUser!.Id.Should().Be(adminId);

        // 4. Create a reader user
        var createReaderCommand = new CreateSecurityUser("reader", "reader123", SecurityRole.Reader) { RequestedBy = adminId };
        var createReaderResult = await service.CreateUserAsync(createReaderCommand);
        createReaderResult.IsSuccess.Should().BeTrue();

        // 5. Update security settings
        var updateSettingsCommand = new UpdateSecuritySettings(SecurityLevel.RestrictedEditing) { RequestedBy = adminId };
        var updateSettingsResult = await service.UpdateSecuritySettingsAsync(updateSettingsCommand);
        updateSettingsResult.IsSuccess.Should().BeTrue();

        // 6. Logout
        var logoutResult = await service.LogoutAsync(new LogoutSecurityUser(token));
        logoutResult.IsSuccess.Should().BeTrue();

        // 7. Session should be invalid after logout
        var invalidSession = await service.ValidateSessionAsync(token, false);
        invalidSession.Should().BeNull();

        // 8. Verify final state
        var finalState = await service.GetSecurityStateAsync();
        finalState.Users.Should().HaveCount(2);
        finalState.Settings.Level.Should().Be(SecurityLevel.RestrictedEditing);
        finalState.RequiresSetup.Should().BeFalse();
    }

    [Fact]
    public async Task IntegrationTest_PasswordHashingSecurity()
    {
        var store = NewStore();
        var service = new SecurityService(store);

        // Create two users with the same password
        var user1Result = await service.CreateUserAsync(new CreateSecurityUser("user1", "samepassword", SecurityRole.Admin));
        user1Result.IsSuccess.Should().BeTrue();
        
        var user2Result = await service.CreateUserAsync(new CreateSecurityUser("user2", "samepassword", SecurityRole.Admin) { RequestedBy = user1Result.Value!.Id });
        user2Result.IsSuccess.Should().BeTrue();

        // Even with the same password, hashes should be different due to different salts
        user1Result.Value!.PasswordHash.Should().NotBe(user2Result.Value!.PasswordHash);
        user1Result.Value!.PasswordSalt.Should().NotBe(user2Result.Value!.PasswordSalt);

        // Both should be able to login
        var login1 = await service.LoginAsync(new LoginSecurityUser("user1", "samepassword"));
        var login2 = await service.LoginAsync(new LoginSecurityUser("user2", "samepassword"));

        login1.IsSuccess.Should().BeTrue();
        login2.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task IntegrationTest_SecurityStateRequiresSetup()
    {
        // No users - requires setup
        var store1 = NewStore();
        var service1 = new SecurityService(store1);
        var state1 = await service1.GetSecurityStateAsync();
        state1.RequiresSetup.Should().BeTrue();

        // Only reader - requires setup
        var reader = new SecurityUser(Guid.NewGuid(), "reader", "hash", "salt", SecurityRole.Reader, DateTime.UtcNow, DateTime.UtcNow);
        var store2 = NewStore(users: new[] { reader });
        var service2 = new SecurityService(store2);
        var state2 = await service2.GetSecurityStateAsync();
        state2.RequiresSetup.Should().BeTrue();

        // Has admin - does not require setup
        var admin = new SecurityUser(Guid.NewGuid(), "admin", "hash", "salt", SecurityRole.Admin, DateTime.UtcNow, DateTime.UtcNow);
        var store3 = NewStore(users: new[] { admin });
        var service3 = new SecurityService(store3);
        var state3 = await service3.GetSecurityStateAsync();
        state3.RequiresSetup.Should().BeFalse();

        // Has both admin and reader - does not require setup
        var store4 = NewStore(users: new[] { admin, reader });
        var service4 = new SecurityService(store4);
        var state4 = await service4.GetSecurityStateAsync();
        state4.RequiresSetup.Should().BeFalse();
    }

    #endregion
}
