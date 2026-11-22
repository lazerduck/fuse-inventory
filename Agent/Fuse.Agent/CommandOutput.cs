using System.Diagnostics;

namespace Fuse.Agent;

internal static class CommandOutput
{
    // All callers supply a fixed executable and argument list. Output is consumed incrementally.
    internal static async Task ReadAsync(string executable, string[] arguments, Action<string> line,
        TimeSpan timeout, CancellationToken ct)
    {
        var start = new ProcessStartInfo(executable)
            { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        start.Environment["LC_ALL"] = "C";
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new IOException($"Could not start {Path.GetFileName(executable)}.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(timeout);
        var token = deadline.Token;
        try
        {
            // Drain diagnostics concurrently so a full pipe cannot stall the command.
            var drain = process.StandardError.BaseStream.CopyToAsync(Stream.Null, token);
            while (await process.StandardOutput.ReadLineAsync(token) is { } value) line(value);
            await drain;
            await process.WaitForExitAsync(token);
            if (process.ExitCode != 0)
                throw new IOException($"{Path.GetFileName(executable)} exited with code {process.ExitCode}.");
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None);
            ct.ThrowIfCancellationRequested();
            throw new TimeoutException($"{Path.GetFileName(executable)} timed out.");
        }
    }
}
