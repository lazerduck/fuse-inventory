using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Responses;

namespace Fuse.Core.Interfaces;

public interface ISqlIntegrationService
{
    Task<IReadOnlyList<SqlIntegrationResponse>> GetSqlIntegrationsAsync();
    Task<SqlIntegrationResponse?> GetSqlIntegrationByIdAsync(Guid id);
    Task<Result<SqlIntegrationResponse>> CreateSqlIntegrationAsync(CreateSqlIntegration command, CancellationToken ct = default);
    Task<Result<SqlIntegrationResponse>> UpdateSqlIntegrationAsync(UpdateSqlIntegration command, CancellationToken ct = default);
    Task<Result> DeleteSqlIntegrationAsync(DeleteSqlIntegration command);
    Task<Result<SqlConnectionTestResult>> TestConnectionAsync(TestSqlConnection command, CancellationToken ct = default);
    Task<Result<SqlIntegrationPermissionsOverviewResponse>> GetPermissionsOverviewAsync(Guid integrationId, CancellationToken ct = default);
}
