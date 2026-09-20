using CommunityToolkit.Mvvm.ComponentModel;
using NetPulse.Localization;

namespace NetPulse.Models;

public class RepairActionResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool RequiresRestart { get; set; }

    public static RepairActionResult Ok(string message, bool requiresRestart = false) => new()
    {
        Success = true,
        Message = message,
        RequiresRestart = requiresRestart
    };

    public static RepairActionResult Fail(string message) => new()
    {
        Success = false,
        Message = message,
        RequiresRestart = false
    };
}

public partial class RepairAction : ObservableObject
{
    public RepairActionId Id { get; init; }
    public RepairCategory Category { get; init; } = RepairCategory.Advanced;
    public bool RequiresAdmin { get; init; } = true;
    public bool IsRecommended { get; init; }

    public string Title => LocalizationService.Get($"Repair_{Id}_Title");
    public string Description => LocalizationService.Get($"Repair_{Id}_Desc");
    public string Impact => LocalizationService.Get($"Repair_{Id}_Impact");

    public RepairAction()
    {
        LocalizationService.Instance.LanguageChanged += (s, e) =>
        {
            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(Description));
            OnPropertyChanged(nameof(Impact));
        };
    }
}
