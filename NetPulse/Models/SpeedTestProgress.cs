namespace NetPulse.Models;

public class SpeedTestProgress
{
    public double CurrentMbps { get; set; }
    public double PeakMbps { get; set; }
    public double TotalMb { get; set; }
    public int PingMs { get; set; }
    public int Percent { get; set; }
    public string StatusText { get; set; } = string.Empty;
}
