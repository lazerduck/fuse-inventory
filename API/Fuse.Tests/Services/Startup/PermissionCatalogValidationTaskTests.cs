using Fuse.Core.Areas.Security;
using Fuse.Core.Services.Startup;
using Xunit;

namespace Fuse.Tests.Services.Startup;

public class PermissionCatalogValidationTaskTests
{
    [Fact]
    public async Task RunAsync_AcceptsMatchingCatalogs()
    {
        var task = new PermissionCatalogValidationTask([new ValidCatalog()]);

        await task.RunAsync();

        Assert.Equal(5, task.Order);
    }

    [Theory]
    [InlineData(typeof(EmptyCatalog), "declares no descriptors")]
    [InlineData(typeof(DuplicateCatalog), "duplicate descriptor keys")]
    [InlineData(typeof(MissingCatalog), "missing descriptors")]
    [InlineData(typeof(UnknownCatalog), "not declared as constants")]
    public async Task RunAsync_RejectsInvalidCatalog(Type catalogType, string expectedMessage)
    {
        var catalog = (AreaPermissions)Activator.CreateInstance(catalogType)!;
        var task = new PermissionCatalogValidationTask([catalog]);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => task.RunAsync());

        Assert.Contains(expectedMessage, exception.Message);
    }

    [Fact]
    public async Task RunAsync_RejectsKeysRepeatedAcrossCatalogs_CaseInsensitively()
    {
        var task = new PermissionCatalogValidationTask([new ValidCatalog(), new OverlappingCatalog()]);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => task.RunAsync());

        Assert.Contains("Duplicate permission key across catalogs", exception.Message);
    }

    public sealed class ValidCatalog : AreaPermissions
    {
        public const string ReadKey = "test:read";
        public override string AreaName => "Test";
        public override IReadOnlyList<PermissionDescriptor> GetPermissionDescriptors() => [new(ReadKey)];
    }

    public sealed class EmptyCatalog : AreaPermissions
    {
        public const string ReadKey = "empty:read";
        public override string AreaName => "Empty";
        public override IReadOnlyList<PermissionDescriptor> GetPermissionDescriptors() => [];
    }

    public sealed class DuplicateCatalog : AreaPermissions
    {
        public const string ReadKey = "duplicate:read";
        public override string AreaName => "Duplicate";
        public override IReadOnlyList<PermissionDescriptor> GetPermissionDescriptors() => [new(ReadKey), new("DUPLICATE:READ")];
    }

    public sealed class MissingCatalog : AreaPermissions
    {
        public const string ReadKey = "missing:read";
        public const string WriteKey = "missing:write";
        public override string AreaName => "Missing";
        public override IReadOnlyList<PermissionDescriptor> GetPermissionDescriptors() => [new(ReadKey)];
    }

    public sealed class UnknownCatalog : AreaPermissions
    {
        public const string ReadKey = "unknown:read";
        public override string AreaName => "Unknown";
        public override IReadOnlyList<PermissionDescriptor> GetPermissionDescriptors() => [new(ReadKey), new("unknown:write")];
    }

    public sealed class OverlappingCatalog : AreaPermissions
    {
        public const string ReadKey = "TEST:READ";
        public override string AreaName => "Overlap";
        public override IReadOnlyList<PermissionDescriptor> GetPermissionDescriptors() => [new(ReadKey)];
    }
}
