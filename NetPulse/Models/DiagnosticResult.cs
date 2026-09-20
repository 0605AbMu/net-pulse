namespace NetPulse.Models;

public enum DiagnosticStatus
{
    Pending,
    Running,
    Success,    // Green - Good condition
    Warning,    // Yellow/Orange - Suboptimal condition (5-6 Mbps bottleneck likely)
    Danger,     // Red - Severe issue
    Info        // Blue - Informational
}

public class DiagnosticResult
{
    public DiagnosticCheckId CheckId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DiagnosticStatus Status { get; set; } = DiagnosticStatus.Pending;
    public string Details { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    public string? DetailsKey { get; set; }
    public object[]? DetailsArgs { get; set; }
    public string? RecommendationKey { get; set; }
    public object[]? RecommendationArgs { get; set; }
    public bool CanAutoRepair { get; set; }
    public RepairActionId? RepairActionId { get; set; }

    public static DiagnosticResult CreatePending(DiagnosticCheckId id, string title, string description) => new()
    {
        CheckId = id,
        Title = title,
        Description = description,
        Status = DiagnosticStatus.Pending,
        Details = "Tekshirish kutilmoqda..."
    };
}
