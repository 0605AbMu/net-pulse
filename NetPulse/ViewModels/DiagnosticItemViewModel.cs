using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetPulse.Core;
using NetPulse.Models;

namespace NetPulse.ViewModels;

public partial class DiagnosticItemViewModel : ObservableObject
{
    [ObservableProperty]
    private DiagnosticCheckId _checkId;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanShowRepairButton))]
    [NotifyPropertyChangedFor(nameof(HasWarning))]
    [NotifyPropertyChangedFor(nameof(HasNoWarning))]
    [NotifyPropertyChangedFor(nameof(IsNormal))]
    [NotifyPropertyChangedFor(nameof(ShowRecommendation))]
    private DiagnosticStatus _status = DiagnosticStatus.Pending;

    [ObservableProperty]
    private string _details = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowRecommendation))]
    private string _recommendation = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanShowRepairButton))]
    private bool _canAutoRepair;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanShowRepairButton))]
    private RepairActionId? _repairActionId;

    [ObservableProperty]
    private bool _isRepairing;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanShowRepairButton))]
    private bool _isRepaired;

    [ObservableProperty]
    private string? _repairMessage;

    public bool CanShowRepairButton => CanAutoRepair && Status != DiagnosticStatus.Success && !IsRepaired;

    public bool HasWarning => Status == DiagnosticStatus.Warning || Status == DiagnosticStatus.Danger;

    public bool HasNoWarning => !HasWarning;

    public bool IsNormal => Status == DiagnosticStatus.Success;

    public bool ShowRecommendation => HasWarning && !string.IsNullOrWhiteSpace(Recommendation);

    public Func<DiagnosticItemViewModel, Task>? OnRepairRequested { get; set; }

    [RelayCommand]
    private async Task RepairAsync()
    {
        if (!RepairActionId.HasValue || IsRepairing) return;

        IsRepairing = true;
        RepairMessage = Localization.LocalizationService.Get("RepairMessageRepairing");

        try
        {
            if (OnRepairRequested != null)
            {
                await OnRepairRequested(this);
            }
            else
            {
                var result = await RepairEngine.ExecuteActionAsync(RepairActionId.Value);
                if (result.Success)
                {
                    Status = DiagnosticStatus.Success;
                    IsRepaired = true;
                    RepairMessage = Localization.LocalizationService.Get("RepairedSuccess");
                }
                else
                {
                    RepairMessage = result.Message;
                }
            }
        }
        catch (Exception ex)
        {
            RepairMessage = $"Xatolik: {ex.Message}";
        }
        finally
        {
            IsRepairing = false;
        }
    }

    public void UpdateFromResult(DiagnosticResult res)
    {
        Title = res.Title;
        Description = res.Description;
        Status = res.Status;
        Details = res.Details;
        Recommendation = res.Recommendation;
        DetailsKey = res.DetailsKey;
        DetailsArgs = res.DetailsArgs;
        RecommendationKey = res.RecommendationKey;
        RecommendationArgs = res.RecommendationArgs;
        CanAutoRepair = res.CanAutoRepair;
        RepairActionId = res.RepairActionId;
        OnPropertyChanged(nameof(CanShowRepairButton));
        OnPropertyChanged(nameof(HasWarning));
        OnPropertyChanged(nameof(HasNoWarning));
        OnPropertyChanged(nameof(IsNormal));
        OnPropertyChanged(nameof(ShowRecommendation));
    }

    public string? DetailsKey { get; set; }
    public object[]? DetailsArgs { get; set; }
    public string? RecommendationKey { get; set; }
    public object[]? RecommendationArgs { get; set; }

    public void RefreshLocalization()
    {
        if (!string.IsNullOrEmpty(DetailsKey))
        {
            Details = Localization.LocalizationService.Get(DetailsKey, DetailsArgs ?? Array.Empty<object>());
        }
        if (!string.IsNullOrEmpty(RecommendationKey))
        {
            Recommendation = Localization.LocalizationService.Get(RecommendationKey, RecommendationArgs ?? Array.Empty<object>());
        }
    }
}
