using System.Reflection;
using Fuse.Core.Areas.Security;
using Fuse.Core.Interfaces;

namespace Fuse.Core.Services.Startup;

/// <summary>
/// Validates permission catalogs at startup to catch descriptor/key drift early.
/// Fails fast if keys and descriptors are inconsistent.
/// </summary>
public class PermissionCatalogValidationTask : IStartupTask
{
    private readonly IEnumerable<AreaPermissions> _permissionCatalogs;

    public PermissionCatalogValidationTask(IEnumerable<AreaPermissions> permissionCatalogs)
    {
        _permissionCatalogs = permissionCatalogs;
    }

    public int Order => 5;

    public Task RunAsync(CancellationToken ct = default)
    {
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var catalog in _permissionCatalogs)
        {
            var declaredKeys = GetDeclaredPermissionKeys(catalog.GetType());
            var descriptorKeys = catalog.GetPermissionDescriptors()
                .Select(d => d.Key?.Trim())
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .Select(k => k!)
                .ToList();

            if (descriptorKeys.Count == 0)
                throw new InvalidOperationException($"Permission catalog '{catalog.GetType().Name}' declares no descriptors.");

            var duplicateDescriptorKeys = descriptorKeys
                .GroupBy(k => k, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .OrderBy(k => k)
                .ToList();

            if (duplicateDescriptorKeys.Count > 0)
                throw new InvalidOperationException(
                    $"Permission catalog '{catalog.GetType().Name}' has duplicate descriptor keys: {string.Join(", ", duplicateDescriptorKeys)}");

            var missingDescriptors = declaredKeys
                .Where(k => !descriptorKeys.Contains(k, StringComparer.OrdinalIgnoreCase))
                .OrderBy(k => k)
                .ToList();

            if (missingDescriptors.Count > 0)
                throw new InvalidOperationException(
                    $"Permission catalog '{catalog.GetType().Name}' is missing descriptors for declared keys: {string.Join(", ", missingDescriptors)}");

            var unknownDescriptors = descriptorKeys
                .Where(k => !declaredKeys.Contains(k, StringComparer.OrdinalIgnoreCase))
                .OrderBy(k => k)
                .ToList();

            if (unknownDescriptors.Count > 0)
                throw new InvalidOperationException(
                    $"Permission catalog '{catalog.GetType().Name}' has descriptor keys not declared as constants: {string.Join(", ", unknownDescriptors)}");

            foreach (var key in descriptorKeys)
            {
                if (!seenKeys.Add(key))
                    throw new InvalidOperationException($"Duplicate permission key across catalogs: {key}");
            }
        }

        return Task.CompletedTask;
    }

    private static IReadOnlyList<string> GetDeclaredPermissionKeys(Type catalogType)
    {
        return catalogType
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(f => f.IsLiteral && !f.IsInitOnly)
            .Where(f => f.FieldType == typeof(string) && f.Name.EndsWith("Key", StringComparison.Ordinal))
            .Select(f => f.GetRawConstantValue() as string)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(v => v)
            .ToList();
    }
}
