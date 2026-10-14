using Fuse.Core.Models;
namespace Fuse.Core.Commands;

public record BulkAccountRow(Guid Id, CreateAccount Account);
public record BulkCreateAccounts(IReadOnlyList<BulkAccountRow> Rows, bool Preview = true, bool SkipExisting = false);
public record BulkAssignGrants(IReadOnlyList<Guid> AccountIds, IReadOnlyList<Grant> Grants, bool Preview = true);
public record BulkAccountResult(Guid Id, string Status, string? Error = null);
public record BulkAccountResponse(bool Applied, IReadOnlyList<BulkAccountResult> Results);
