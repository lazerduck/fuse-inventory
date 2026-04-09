using Fuse.Core.Models;

namespace Fuse.Tests.Helpers;

public static class SecurityContextHelper
{
    public static SecurityContext Get => new(SecurityPosture.Unrestricted, 
            Array.Empty<FuseRole>(),
            Array.Empty<FuseUser>(),
            Array.Empty<FuseApiKey>(),
            Array.Empty<Session>()
        );
}