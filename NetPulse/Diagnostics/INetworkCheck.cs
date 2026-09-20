using NetPulse.Models;

namespace NetPulse.Diagnostics;

public interface INetworkCheck
{
    DiagnosticCheckId Id { get; }
    string Title { get; }
    string Description { get; }
    Task<DiagnosticResult> RunCheckAsync(NetworkInfo networkInfo);
}
