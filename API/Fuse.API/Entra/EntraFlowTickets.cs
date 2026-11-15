using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace Fuse.API.Entra;

public sealed class EntraFlowTickets
{
    private readonly ConcurrentDictionary<string, (EntraFlowTicket Ticket, DateTime Expiry)> tickets = new();
    public string Issue(EntraFlowTicket ticket)
    {
        var now = DateTime.UtcNow;
        foreach (var pair in tickets.Where(p => p.Value.Expiry <= now))
            tickets.TryRemove(pair.Key, out _);
        var key = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        tickets[key] = (ticket, now.AddMinutes(5));
        return key;
    }
    public EntraFlowTicket? Take(string? key) => key is not null && tickets.TryRemove(key, out var value)
        && value.Expiry > DateTime.UtcNow ? value.Ticket : null;
}
