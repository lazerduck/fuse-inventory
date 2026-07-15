using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Fuse.Core.Areas.Tag;
using System.Net;
using System.Net.Sockets;

namespace Fuse.Core.Areas.Platform;

public class PlatformService : IPlatformService
{
    private readonly IFuseStore _fuseStore;
    private readonly ITagService _tagService;

    public PlatformService(IFuseStore fuseStore, ITagService tagService)
    {
        _fuseStore = fuseStore;
        _tagService = tagService;
    }

    public async Task<IReadOnlyList<Models.Platform>> GetPlatformsAsync()
        => (await _fuseStore.GetAsync()).Platforms;

    public async Task<Models.Platform?> GetPlatformByIdAsync(Guid id)
        => (await _fuseStore.GetAsync()).Platforms.FirstOrDefault(s => s.Id == id);

    public async Task<Result<Models.Platform>> CreatePlatformAsync(CreatePlatform command)
    {
        if (string.IsNullOrWhiteSpace(command.DisplayName))
            return Result<Models.Platform>.Failure("Platform display name cannot be empty.", ErrorType.Validation);

        var nodeError = ValidateNodes(command.Kind, command.Nodes);
        if (nodeError is not null)
            return Result<Models.Platform>.Failure(nodeError, ErrorType.Validation);

        var addressError = ValidateAddresses(command.IpAddresses, "Platform IP addresses");
        if (addressError is not null)
            return Result<Models.Platform>.Failure(addressError, ErrorType.Validation);

        var store = await _fuseStore.GetAsync();

        // Validate tags
        var tagIds = command.TagIds ?? new HashSet<Guid>();
        foreach (var tagId in tagIds)
        {
            if (await _tagService.GetTagByIdAsync(tagId) is null)
                return Result<Models.Platform>.Failure($"Tag with ID '{tagId}' not found.", ErrorType.Validation);
        }

        // Unique display name globally
        if (store.Platforms.Any(s => string.Equals(s.DisplayName, command.DisplayName, StringComparison.OrdinalIgnoreCase)))
            return Result<Models.Platform>.Failure($"Platform with display name '{command.DisplayName}' already exists.", ErrorType.Conflict);

        var now = DateTime.UtcNow;
        var platform = new Models.Platform(
            Id: Guid.NewGuid(),
            DisplayName: command.DisplayName,
            DnsName: command.DnsName,
            Os: command.Os,
            Kind: command.Kind,
            IpAddresses: NormalizeAddresses(command.IpAddresses),
            Notes: command.Notes,
            TagIds: tagIds,
            CreatedAt: now,
            UpdatedAt: now,
            Nodes: MapNodes(command.Nodes)
        );

        await _fuseStore.UpdateAsync(s => s with { Platforms = s.Platforms.Append(platform).ToList() });
        return Result<Models.Platform>.Success(platform);
    }

    public async Task<Result<Models.Platform>> UpdatePlatformAsync(UpdatePlatform command)
    {
        if (string.IsNullOrWhiteSpace(command.DisplayName))
            return Result<Models.Platform>.Failure("Platform display name cannot be empty.", ErrorType.Validation);

        var store = await _fuseStore.GetAsync();
        var existing = store.Platforms.FirstOrDefault(s => s.Id == command.Id);
        if (existing is null)
            return Result<Models.Platform>.Failure($"Platform with ID '{command.Id}' not found.", ErrorType.NotFound);

        if (command.Kind != PlatformKind.Cluster && existing.Nodes is { Count: > 0 } && command.Nodes is null)
            return Result<Models.Platform>.Failure("Remove all platform nodes before changing a cluster to another kind.", ErrorType.Validation);

        var nodeError = ValidateNodes(command.Kind, command.Nodes);
        if (nodeError is not null)
            return Result<Models.Platform>.Failure(nodeError, ErrorType.Validation);

        var addressError = ValidateAddresses(command.IpAddresses, "Platform IP addresses");
        if (addressError is not null)
            return Result<Models.Platform>.Failure(addressError, ErrorType.Validation);

        var tagIds = command.TagIds ?? new HashSet<Guid>();
        foreach (var tagId in tagIds)
        {
            if (await _tagService.GetTagByIdAsync(tagId) is null)
                return Result<Models.Platform>.Failure($"Tag with ID '{tagId}' not found.", ErrorType.Validation);
        }

        // Unique display name globally (excluding current platform)
        if (store.Platforms.Any(s => s.Id != command.Id && string.Equals(s.DisplayName, command.DisplayName, StringComparison.OrdinalIgnoreCase)))
            return Result<Models.Platform>.Failure($"Platform with display name '{command.DisplayName}' already exists.", ErrorType.Conflict);

        var updated = existing with
        {
            DisplayName = command.DisplayName,
            DnsName = command.DnsName,
            Os = command.Os,
            Kind = command.Kind,
            IpAddresses = NormalizeAddresses(command.IpAddresses),
            Notes = command.Notes,
            TagIds = tagIds,
            UpdatedAt = DateTime.UtcNow,
            Nodes = MapNodes(command.Nodes, existing.Nodes)
        };

        await _fuseStore.UpdateAsync(s => s with { Platforms = s.Platforms.Select(x => x.Id == command.Id ? updated : x).ToList() });
        return Result<Models.Platform>.Success(updated);
    }

    public async Task<Result> DeletePlatformAsync(DeletePlatform command)
    {
        var store = await _fuseStore.GetAsync();
        if (!store.Platforms.Any(s => s.Id == command.Id))
            return Result.Failure($"Platform with ID '{command.Id}' not found.", ErrorType.NotFound);

        await _fuseStore.UpdateAsync(s => s with { Platforms = s.Platforms.Where(x => x.Id != command.Id).ToList() });
        return Result.Success();
    }

    private static string? ValidateNodes(PlatformKind? kind, IReadOnlyList<PlatformNodeInput>? nodes)
    {
        if (nodes is not { Count: > 0 }) return null;
        if (kind != PlatformKind.Cluster) return "Platform nodes can only be specified for cluster platforms.";
        if (nodes.Any(n => string.IsNullOrWhiteSpace(n.DisplayName))) return "Platform node display name cannot be empty.";
        if (nodes.GroupBy(n => n.DisplayName.Trim(), StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
            return "Platform node display names must be unique within a cluster.";
        foreach (var node in nodes)
        {
            var addressError = ValidateAddresses(node.IpAddresses, $"IP addresses for node '{node.DisplayName}'");
            if (addressError is not null) return addressError;
        }
        return null;
    }

    private static string? ValidateAddresses(IReadOnlyList<string>? addresses, string field)
    {
        var invalid = addresses?.FirstOrDefault(address => !IsValidIpAddress(address));
        return invalid is null ? null : $"{field} contains an invalid IPv4 or IPv6 address: '{invalid}'.";
    }

    private static bool IsValidIpAddress(string? value)
    {
        var address = value?.Trim();
        if (string.IsNullOrEmpty(address)) return false;
        if (address.Contains(':'))
            return IPAddress.TryParse(address, out var ipv6) && ipv6.AddressFamily == AddressFamily.InterNetworkV6;

        var segments = address.Split('.');
        return segments.Length == 4 && segments.All(segment =>
            byte.TryParse(segment, out var octet) && segment == octet.ToString());
    }

    private static IReadOnlyList<string> NormalizeAddresses(IReadOnlyList<string>? addresses)
        => addresses?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => IPAddress.Parse(x.Trim()).ToString()).Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? [];

    private static IReadOnlyList<PlatformNode>? MapNodes(IReadOnlyList<PlatformNodeInput>? nodes, IReadOnlyList<PlatformNode>? existing = null)
    {
        if (nodes is null) return null;
        var existingIds = existing?.Select(n => n.Id).ToHashSet() ?? [];
        return nodes.Select(n => new PlatformNode(
            n.Id is Guid id && existingIds.Contains(id) ? id : Guid.NewGuid(),
            n.DisplayName.Trim(), n.DnsName, n.Os, NormalizeAddresses(n.IpAddresses), n.Notes)).ToList();
    }
}
