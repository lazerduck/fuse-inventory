using Microsoft.Data.SqlClient;
using Xunit;

namespace Fuse.Tests.Services;

public class SqlAuthenticationProviderTests
{
    [Theory]
    [InlineData(SqlAuthenticationMethod.ActiveDirectoryDefault)]
    [InlineData(SqlAuthenticationMethod.ActiveDirectoryManagedIdentity)]
    [InlineData(SqlAuthenticationMethod.ActiveDirectoryServicePrincipal)]
    public void EntraConnectionStringAuthentication_HasProvider(SqlAuthenticationMethod method)
    {
        // These modes were built into SqlClient 6 and must remain available after upgrading.
        // Resolving the provider requires no Azure credentials or network connection.
        var provider = SqlAuthenticationProvider.GetProvider(method);

        Assert.NotNull(provider);
        Assert.True(provider.IsSupported(method));
    }
}
