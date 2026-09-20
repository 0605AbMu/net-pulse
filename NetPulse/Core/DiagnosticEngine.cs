using NetPulse.Diagnostics;
using NetPulse.Models;

namespace NetPulse.Core;

public class DiagnosticEngine
{
    private readonly INetworkCheck[] _checks;

    public event Action<INetworkCheck, DiagnosticStatus>? CheckStatusChanged;
    public event Action<DiagnosticResult>? CheckCompleted;

    public DiagnosticEngine()
    {
        _checks =
        [
            new WiFiSignalCheck(),
            new PowerManagementCheck(),
            new TcpAutoTuningCheck(),
            new NetworkThrottlingCheck(),
            new DnsCheck(),
            new WinsockCheck(),
            new ChannelInterferenceCheck(),
            new DriverCheck(),
            new SpeedTestCheck()
        ];
    }

    public IReadOnlyList<INetworkCheck> GetRegisteredChecks() => _checks;

    public async Task<DiagnosticSuiteResult> RunAllChecksAsync(
        NetworkInfo info, 
        CancellationToken cancellationToken = default)
    {
        var results = new List<DiagnosticResult>(_checks.Length);

        foreach (var check in _checks)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            CheckStatusChanged?.Invoke(check, DiagnosticStatus.Running);

            try
            {
                var result = await check.RunCheckAsync(info);
                results.Add(result);
                CheckCompleted?.Invoke(result);
            }
            catch (Exception ex)
            {
                var failResult = new DiagnosticResult
                {
                    CheckId = check.Id,
                    Title = check.Title,
                    Description = check.Description,
                    Status = DiagnosticStatus.Warning,
                    Details = $"Tekshirishda xatolik: {ex.Message}",
                    Recommendation = "Qaytadan tekshirib ko'ring."
                };
                results.Add(failResult);
                CheckCompleted?.Invoke(failResult);
            }
        }

        var score = CalculateHealthScore(results);
        return new DiagnosticSuiteResult(results.ToArray(), score);
    }

    public static int CalculateHealthScore(IEnumerable<DiagnosticResult> results)
    {
        int score = 100;
        foreach (var r in results)
        {
            if (r.Status == DiagnosticStatus.Danger) score -= 25;
            else if (r.Status == DiagnosticStatus.Warning) score -= 12;
        }
        return Math.Clamp(score, 0, 100);
    }
}
