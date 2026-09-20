using System.Net.NetworkInformation;

namespace NetPulse.Models;

public readonly struct MacAddress : IEquatable<MacAddress>
{
    private readonly PhysicalAddress? _value;

    public static MacAddress None => new(PhysicalAddress.None);

    public PhysicalAddress Value => _value ?? PhysicalAddress.None;

    public MacAddress(PhysicalAddress address)
    {
        _value = address ?? PhysicalAddress.None;
    }

    public static MacAddress Parse(string address)
    {
        var cleaned = address.Replace(":", "").Replace("-", "");
        return new MacAddress(PhysicalAddress.Parse(cleaned));
    }

    public static bool TryParse(string? address, out MacAddress result)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            result = None;
            return false;
        }

        try
        {
            var cleaned = address.Replace(":", "").Replace("-", "");
            if (PhysicalAddress.TryParse(cleaned, out var parsed))
            {
                result = new MacAddress(parsed);
                return true;
            }
        }
        catch
        {
            // Ignored
        }

        result = None;
        return false;
    }

    public override string ToString()
    {
        var bytes = Value.GetAddressBytes();
        if (bytes == null || bytes.Length == 0)
            return "Noma'lum";

        return string.Join(":", bytes.Select(b => b.ToString("X2")));
    }

    public bool Equals(MacAddress other) => Value.Equals(other.Value);

    public override bool Equals(object? obj) => obj is MacAddress other && Equals(other);

    public override int GetHashCode() => Value.GetHashCode();

    public static bool operator ==(MacAddress left, MacAddress right) => left.Equals(right);

    public static bool operator !=(MacAddress left, MacAddress right) => !left.Equals(right);
}

public readonly record struct ProcessExecutionResult(int ExitCode, string Output, string Error)
{
    public bool Success => ExitCode == 0;
}

public readonly record struct PingResult(int AvgLatencyMs, int PacketLossPercent)
{
    public bool Success => AvgLatencyMs >= 0;
}

public readonly record struct DnsResolutionResult(bool Success, int LatencyMs);

public readonly record struct DiagnosticSuiteResult(DiagnosticResult[] Results, int HealthScore);
