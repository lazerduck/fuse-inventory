namespace Fuse.API.Entra;

public record EntraFlowTicket(string? SessionToken, Guid? LinkUserId, int? LinkSessionVersion);
