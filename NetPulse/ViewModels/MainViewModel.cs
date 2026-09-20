using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetPulse.Core;
using NetPulse.Localization;
using NetPulse.Models;
using Sentry;

namespace NetPulse.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly DiagnosticEngine _diagnosticEngine;

    public string AppVersion => AppVersionHelper.DisplayVersion;

    [ObservableProperty]
    private NetworkInfo _currentNetwork = new();

    [ObservableProperty]
    private int _healthScore = 0;

    [ObservableProperty]
    private string _healthStatusText = LocalizationService.Get("StatusUnchecked");

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private bool _isAdmin;

    [ObservableProperty]
    private string _statusMessage = LocalizationService.Get("StatusReady");

    [ObservableProperty]
    private string _currentLanguage = "uz";

    [ObservableProperty]
    private int _selectedTabIndex = 0;

    // Dynamic DNS properties
    [ObservableProperty]
    private string _dnsStatusText = string.Empty;

    [ObservableProperty]
    private string _dnsStatusBadgeColor = "#F59E0B";

    [ObservableProperty]
    private string _dnsStatusBadgeBg = "#261C08";

    // Speed test standalone properties
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowSpeedResults))]
    private bool _isTestingSpeed;

    [ObservableProperty]
    private double _currentSpeedMbps;

    [ObservableProperty]
    private double _peakSpeedMbps;

    [ObservableProperty]
    private int _pingLatencyMs;

    [ObservableProperty]
    private double _totalDownloadedMb;

    [ObservableProperty]
    private int _progressPercentage;

    [ObservableProperty]
    private double _gaugeAngle = -120.0;

    [ObservableProperty]
    private string _speedQualityText = LocalizationService.Get("SpeedQualityNotTested");

    [ObservableProperty]
    private string _speedQualityColor = "#94A3B8";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowSpeedResults))]
    private bool _isTestCompleted;

    public bool ShowSpeedResults => IsTestCompleted && !IsTestingSpeed;

    [ObservableProperty]
    private string _speedTestStatus = LocalizationService.Get("SpeedTest_StatusReady");

    [ObservableProperty]
    private string _speedTestServerName = LocalizationService.Get("SpeedTest_ServerName");

    // Repair log
    [ObservableProperty]
    private string _repairLog = string.Empty;

    // Filter and counts
    [ObservableProperty]
    private DiagnosticFilter _selectedDiagnosticFilter = DiagnosticFilter.All;

    [ObservableProperty]
    private int _totalChecksCount = 9;

    [ObservableProperty]
    private int _issuesCount;

    [ObservableProperty]
    private int _successCount;

    [ObservableProperty]
    private double _scanProgressPercent;

    [ObservableProperty]
    private string _currentScanStepText = string.Empty;

    public ObservableCollection<DiagnosticItemViewModel> DiagnosticItems { get; } = new();
    public ObservableCollection<DiagnosticItemViewModel> FilteredDiagnosticItems { get; } = new();
    public ObservableCollection<RepairAction> RepairActions { get; } = new();
    public ObservableCollection<RepairAction> RecommendedRepairActions { get; } = new();
    public ObservableCollection<RepairAction> DnsRepairActions { get; } = new();
    public ObservableCollection<RepairAction> AdvancedRepairActions { get; } = new();

    public MainViewModel()
    {
        _diagnosticEngine = new DiagnosticEngine();
        IsAdmin = AdminHelper.IsAdministrator();

        // Initialize checks
        foreach (var check in _diagnosticEngine.GetRegisteredChecks())
        {
            var item = new DiagnosticItemViewModel
            {
                CheckId = check.Id,
                Title = check.Title,
                Description = check.Description,
                Status = DiagnosticStatus.Pending,
                Details = LocalizationService.Get("StatusBadgePending"),
                OnRepairRequested = HandleItemRepairAsync
            };
            DiagnosticItems.Add(item);
        }

        TotalChecksCount = DiagnosticItems.Count;
        UpdateCounts();
        ApplyFilter();

        // Initialize repair actions using concrete RepairCategory
        foreach (var action in RepairEngine.GetAvailableActions())
        {
            RepairActions.Add(action);
            switch (action.Category)
            {
                case RepairCategory.Recommended:
                    RecommendedRepairActions.Add(action);
                    break;
                case RepairCategory.Dns:
                    DnsRepairActions.Add(action);
                    break;
                case RepairCategory.Advanced:
                    AdvancedRepairActions.Add(action);
                    break;
            }
        }

        // Load initial network state
        _ = RefreshNetworkInfoAsync();

        // Subscribe to localization changes to keep dynamic texts synchronized
        LocalizationService.Instance.LanguageChanged += (s, e) =>
        {
            CurrentLanguage = LocalizationService.Instance.CurrentLanguage;
            UpdateHealthStatusText();
            UpdateSpeedQuality(CurrentSpeedMbps);
            StatusMessage = LocalizationService.Get("StatusReady");

            foreach (var item in DiagnosticItems)
            {
                var check = _diagnosticEngine.GetRegisteredChecks().FirstOrDefault(c => c.Id == item.CheckId);
                if (check != null)
                {
                    item.Title = check.Title;
                    item.Description = check.Description;
                }
                item.RefreshLocalization();
            }

            UpdateDnsStatus();
            SpeedTestServerName = LocalizationService.Get("SpeedTest_ServerName");
            if (!IsTestCompleted && !IsTestingSpeed)
            {
                SpeedTestStatus = LocalizationService.Get("SpeedTest_StatusReady");
            }
            UpdateCounts();
            ApplyFilter();
        };
    }

    [RelayCommand]
    public void SetLanguage(string lang)
    {
        SentryService.TrackFeatureUsage("set_language", new() { ["language"] = lang });
        LocalizationService.Instance.CurrentLanguage = lang;
    }

    public void UpdateCounts()
    {
        TotalChecksCount = DiagnosticItems.Count;
        IssuesCount = DiagnosticItems.Count(i => i.Status is DiagnosticStatus.Warning or DiagnosticStatus.Danger);
        SuccessCount = DiagnosticItems.Count(i => i.Status is DiagnosticStatus.Success);
    }

    public void ApplyFilter()
    {
        FilteredDiagnosticItems.Clear();
        foreach (var item in DiagnosticItems)
        {
            bool matches = SelectedDiagnosticFilter switch
            {
                DiagnosticFilter.Issues => item.Status is DiagnosticStatus.Warning or DiagnosticStatus.Danger,
                DiagnosticFilter.Success => item.Status is DiagnosticStatus.Success,
                _ => true
            };
            if (matches)
            {
                FilteredDiagnosticItems.Add(item);
            }
        }
    }

    [RelayCommand]
    public void SetFilter(string filter)
    {
        if (Enum.TryParse<DiagnosticFilter>(filter, true, out var parsed))
        {
            SelectedDiagnosticFilter = parsed;
            ApplyFilter();
            SentryService.TrackFeatureUsage("filter_diagnostics", new() { ["filter"] = filter });
        }
    }

    [RelayCommand]
    public void SetDiagnosticFilter(DiagnosticFilter filter)
    {
        SelectedDiagnosticFilter = filter;
        ApplyFilter();
        SentryService.TrackFeatureUsage("filter_diagnostics", new() { ["filter"] = filter.ToString() });
    }

    [RelayCommand]
    public void NavigateTo(int index)
    {
        SelectedTabIndex = index;
        SentryService.TrackFeatureUsage("navigate_tab", new() { ["tab_index"] = index });
    }

    [RelayCommand]
    public async Task RefreshNetworkInfoAsync()
    {
        SentryService.TrackFeatureUsage("refresh_network");
        try
        {
            CurrentNetwork = await NetworkHelper.GetActiveNetworkInfoAsync();
            SentryService.SetNetworkContext(CurrentNetwork);
            if (CurrentNetwork.ConnectionType == NetworkConnectionType.WiFi && CurrentNetwork.SignalPercentage > 0)
            {
                SentryService.TrackWiFiSignal(CurrentNetwork.SignalPercentage, CurrentNetwork.BandDisplay);
            }
            UpdateDnsStatus();
            AppLogger.Log($"[NetPulse] Network state updated: {CurrentNetwork.AdapterName} ({CurrentNetwork.ConnectionTypeDisplay})");
        }
        catch (Exception ex)
        {
            AppLogger.LogError("[NetPulse Network Refresh Error]", ex);
        }
    }

    [RelayCommand]
    public void OpenDnsOptimizations()
    {
        SelectedTabIndex = 2;
        SentryService.TrackFeatureUsage("navigate_dns_optimizations");
    }
    
    [RelayCommand]
    public void OpenDiagnostics(DiagnosticFilter filter = DiagnosticFilter.All)
    {
        SelectedTabIndex = 1;
        SetDiagnosticFilter(filter);
        SentryService.TrackFeatureUsage("open_diagnostics", new() { ["filter"] = filter.ToString() });
    }

    public void UpdateDnsStatus()
    {
        var isCloudflare = CurrentNetwork.DnsServers.Any(d => 
            d.ToString() == "1.1.1.1" || d.ToString() == "1.0.0.1");
        var isGoogle = CurrentNetwork.DnsServers.Any(d => 
            d.ToString() == "8.8.8.8" || d.ToString() == "8.8.4.4");

        if (isCloudflare)
        {
            DnsStatusText = LocalizationService.Get("DnsActiveCloudflare");
            DnsStatusBadgeColor = "#10B981";
            DnsStatusBadgeBg = "#142E25";
        }
        else if (isGoogle)
        {
            DnsStatusText = LocalizationService.Get("DnsActiveGoogle");
            DnsStatusBadgeColor = "#10B981";
            DnsStatusBadgeBg = "#142E25";
        }
        else
        {
            DnsStatusText = LocalizationService.Get("DnsActiveCustom");
            DnsStatusBadgeColor = "#F59E0B";
            DnsStatusBadgeBg = "#261C08";
        }
    }

    [RelayCommand]
    public async Task StartDiagnosticsAsync()
    {
        if (IsScanning) return;

        SentryService.TrackFeatureUsage("diagnostics_scan");
        var transaction = SentrySdk.StartTransaction("diagnostics_scan", "diagnostics.run");
        SentrySdk.ConfigureScope(scope => scope.Transaction = transaction);

        IsScanning = true;
        SelectedDiagnosticFilter = DiagnosticFilter.All;
        ScanProgressPercent = 0;
        CurrentScanStepText = LocalizationService.Get("StatusScanningText");
        StatusMessage = LocalizationService.Get("StatusScanningAdapter");
        HealthStatusText = LocalizationService.Get("StatusScanningText");

        AppLogger.Log("[NetPulse] Starting network diagnostics...");

        try
        {
            // 1. Refresh network adapter info first
            CurrentNetwork = await NetworkHelper.GetActiveNetworkInfoAsync();
            SentryService.SetNetworkContext(CurrentNetwork);

            // Reset diagnostic items status
            foreach (var item in DiagnosticItems)
            {
                item.Status = DiagnosticStatus.Pending;
                item.Details = LocalizationService.Get("StatusBadgePending");
                item.IsRepaired = false;
                item.RepairMessage = null;
            }
            UpdateCounts();
            ApplyFilter();

            var results = new List<DiagnosticResult>();
            var checks = _diagnosticEngine.GetRegisteredChecks();
            int total = checks.Count;
            int stepIndex = 0;

            foreach (var check in checks)
            {
                stepIndex++;
                ScanProgressPercent = (double)stepIndex / total * 100.0;
                CurrentScanStepText = $"{stepIndex}/{total}: {check.Title}";

                var itemVm = DiagnosticItems.FirstOrDefault(i => i.CheckId == check.Id);
                if (itemVm != null)
                {
                    itemVm.Status = DiagnosticStatus.Running;
                    itemVm.Details = LocalizationService.Get("StatusBadgeRunning");
                }

                StatusMessage = LocalizationService.Get("StatusScanningStep", check.Title, stepIndex, total);
                AppLogger.Log($"[NetPulse] Running check {stepIndex}/{total}: {check.Id} ({check.Title})...");
                SentryService.TrackFeatureUsage("diagnostic_check", new() { ["check_id"] = check.Id });

                var checkSpan = transaction.StartChild($"check.{check.Id}", check.Title);
                DiagnosticResult res;
                try
                {
                    res = await check.RunCheckAsync(CurrentNetwork);
                    checkSpan.Finish(res.Status == DiagnosticStatus.Danger ? SpanStatus.InternalError : SpanStatus.Ok);
                    AppLogger.Log($"[NetPulse] Check {check.Id} result: {res.Status} | {res.Details}");
                }
                catch (Exception checkEx)
                {
                    checkSpan.Finish(SpanStatus.InternalError);
                    transaction.Status = SpanStatus.InternalError;
                    transaction.SetTag("error", $"check_error_{check.Id}");
                    AppLogger.LogError($"[NetPulse Check Error - {check.Id}]", checkEx);
                    AppendRepairLog($"❌ [{check.Title} tekshiruvida xatolik]: {checkEx.Message}");

                    res = new DiagnosticResult
                    {
                        CheckId = check.Id,
                        Title = check.Title,
                        Description = check.Description,
                        Status = DiagnosticStatus.Warning,
                        Details = checkEx.Message,
                        Recommendation = LocalizationService.Get("StatusReady")
                    };
                }

                // Agar tekshiruvda muammo (Warning/Danger) topilsa, Sentry ga metrika yuboramiz
                if (res.Status == DiagnosticStatus.Warning || res.Status == DiagnosticStatus.Danger)
                {
                    SentryService.TrackDiagnosticIssue(check.Id.ToString(), res.Status, res.Title);
                }

                results.Add(res);

                if (itemVm != null)
                {
                    itemVm.UpdateFromResult(res);
                }

                UpdateCounts();
                ApplyFilter();
            }

            HealthScore = DiagnosticEngine.CalculateHealthScore(results);
            int issuesCount = results.Count(r => r.Status == DiagnosticStatus.Warning || r.Status == DiagnosticStatus.Danger);
            SentryService.TrackHealthScore(HealthScore, results.Count, issuesCount);

            UpdateHealthStatusText();
            UpdateCounts();
            ApplyFilter();

            StatusMessage = LocalizationService.Get("StatusScanCompleted", HealthStatusText, HealthScore);
            AppLogger.Log($"[NetPulse] Diagnostics completed. Score: {HealthScore}/100 ({HealthStatusText})");

            if (transaction.Status == null)
            {
                transaction.Finish(SpanStatus.Ok);
            }
            else
            {
                transaction.Finish();
            }
        }
        catch (Exception ex)
        {
            transaction.Finish(SpanStatus.InternalError);
            AppLogger.LogError("\n[NetPulse DIAGNOSTICS FATAL ERROR]", ex);
            AppendRepairLog($"\n❌ [DIAGNOSTIKA XATOLIK]: {ex.Message}\n{ex.StackTrace}");
            StatusMessage = LocalizationService.Get("StatusScanError", ex.Message);
        }
        finally
        {
            IsScanning = false;
            CurrentScanStepText = string.Empty;
        }
    }

    private void UpdateHealthStatusText()
    {
        if (HealthScore >= 90) HealthStatusText = LocalizationService.Get("StatusHealthExcellent");
        else if (HealthScore >= 70) HealthStatusText = LocalizationService.Get("StatusHealthGood");
        else if (HealthScore >= 50) HealthStatusText = LocalizationService.Get("StatusHealthFair");
        else HealthStatusText = LocalizationService.Get("StatusHealthCritical");
    }

    private async Task HandleItemRepairAsync(DiagnosticItemViewModel item)
    {
        if (!item.RepairActionId.HasValue) return;

        SentryService.TrackFeatureUsage("item_repair", new()
        {
            ["check_id"] = item.CheckId,
            ["action_id"] = item.RepairActionId.Value.ToString()
        });

        var sw = Stopwatch.StartNew();
        var result = await RepairEngine.ExecuteActionAsync(item.RepairActionId.Value, CurrentNetwork);
        sw.Stop();
        SentryService.TrackRepairResult(item.RepairActionId.Value, result.Success, sw.ElapsedMilliseconds, result.Message);

        if (result.Success)
        {
            var check = _diagnosticEngine.GetRegisteredChecks().FirstOrDefault(c => c.Id == item.CheckId);
            if (check != null && CurrentNetwork != null)
            {
                var recheck = await check.RunCheckAsync(CurrentNetwork);
                item.UpdateFromResult(recheck);
            }
            else
            {
                item.Status = DiagnosticStatus.Success;
            }

            item.IsRepaired = true;
            item.RepairMessage = LocalizationService.Get("RepairedSuccess");
            AppendRepairLog($"✅ [{item.Title}]: {result.Message}");

            // Recalculate health
            var currentResults = DiagnosticItems.Select(i => new DiagnosticResult { Status = i.Status }).ToList();
            HealthScore = DiagnosticEngine.CalculateHealthScore(currentResults);
            UpdateHealthStatusText();
            UpdateCounts();
            ApplyFilter();
        }
        else
        {
            item.RepairMessage = result.Message;
            AppendRepairLog($"❌ [{item.Title}]: {result.Message}");
        }
    }

    [RelayCommand]
    public async Task QuickOptimizeAsync()
    {
        SentryService.TrackFeatureUsage("quick_optimize");
        var transaction = SentrySdk.StartTransaction("quick_optimize", "repair.optimize_all");
        SentrySdk.ConfigureScope(scope => scope.Transaction = transaction);

        StatusMessage = LocalizationService.Get("StatusOptimizingAll");
        AppendRepairLog("\n--- ⚡ TEZKOR OPTIMIZATSIYA BOSHLANDI ---");

        try
        {
            var sw = Stopwatch.StartNew();
            var res = await RepairEngine.OptimizeAllAsync(CurrentNetwork);
            sw.Stop();
            SentryService.TrackRepairResult(RepairActionId.OptimizeAll, res.Success, sw.ElapsedMilliseconds, res.Message);

            AppendRepairLog(res.Message);

            if (!res.Success)
            {
                transaction.Status = SpanStatus.InternalError;
                transaction.SetTag("error", "quick_optimize_partial");
            }
            transaction.Finish(transaction.Status ?? SpanStatus.Ok);

            StatusMessage = res.Success 
                ? LocalizationService.Get("StatusOptimizedSuccess")
                : LocalizationService.Get("StatusOptimizedPartial");

            // Re-run diagnostics to reflect improvements
            await StartDiagnosticsAsync();
        }
        catch (Exception ex)
        {
            transaction.Finish(SpanStatus.InternalError);
            AppLogger.LogError("[QuickOptimize Error]", ex);
            throw;
        }
    }

    [RelayCommand]
    public async Task ExecuteRepairActionAsync(RepairAction action)
    {
        if (action == null) return;

        SentryService.TrackFeatureUsage("repair_action", new() { ["action_id"] = action.Id.ToString() });
        var transaction = SentrySdk.StartTransaction($"repair_{action.Id}", "repair.action");
        SentrySdk.ConfigureScope(scope => scope.Transaction = transaction);

        StatusMessage = LocalizationService.Get("StatusActionExecuting", action.Title);
        AppendRepairLog($"\n🔧 [{action.Title}] bajarilmoqda...");

        try
        {
            var sw = Stopwatch.StartNew();
            var res = await RepairEngine.ExecuteActionAsync(action.Id, CurrentNetwork);
            sw.Stop();
            SentryService.TrackRepairResult(action.Id, res.Success, sw.ElapsedMilliseconds, res.Message);

            if (res.Success)
            {
                transaction.Finish(SpanStatus.Ok);
                AppendRepairLog($"✅ Natija: {res.Message}");
                StatusMessage = LocalizationService.Get("StatusActionSuccess", action.Title);

                // If an item in diagnostics matched this, update it
                var matchingItem = DiagnosticItems.FirstOrDefault(i => i.RepairActionId == action.Id);
                if (matchingItem != null)
                {
                    matchingItem.Status = DiagnosticStatus.Success;
                    matchingItem.IsRepaired = true;
                    matchingItem.RepairMessage = LocalizationService.Get("RepairedSuccess");
                    UpdateCounts();
                    ApplyFilter();
                }

                await RefreshNetworkInfoAsync();

                if (action.Id == RepairActionId.OptimizeAll)
                {
                    await StartDiagnosticsAsync();
                }
            }
            else
            {
                transaction.Status = SpanStatus.InternalError;
                transaction.SetTag("error", "repair_action_failed");
                transaction.Finish();
                AppendRepairLog($"❌ Xatolik: {res.Message}");
                StatusMessage = LocalizationService.Get("StatusActionError", res.Message);
            }
        }
        catch (Exception ex)
        {
            transaction.Finish(SpanStatus.InternalError);
            AppLogger.LogError($"[Repair Action Error - {action.Id}]", ex);
        }
    }

    [RelayCommand]
    public async Task RunStandaloneSpeedTestAsync()
    {
        if (IsTestingSpeed) return;

        SentryService.TrackFeatureUsage("speed_test");
        var transaction = SentrySdk.StartTransaction("speed_test", "network.speed_test");
        SentrySdk.ConfigureScope(scope => scope.Transaction = transaction);

        IsTestingSpeed = true;
        IsTestCompleted = false; // Hidden until test completes
        CurrentSpeedMbps = 0;
        PeakSpeedMbps = 0;
        TotalDownloadedMb = 0;
        GaugeAngle = -120.0;
        ProgressPercentage = 0;

        var detailedProgress = new Progress<SpeedTestProgress>(p =>
        {
            CurrentSpeedMbps = p.CurrentMbps;
            PeakSpeedMbps = p.PeakMbps;
            TotalDownloadedMb = p.TotalMb;
            PingLatencyMs = p.PingMs;
            ProgressPercentage = p.Percent;
            GaugeAngle = CalculateGaugeAngle(p.CurrentMbps);
        });

        try
        {
            var finalSpeed = await NetworkHelper.TestDownloadSpeedMbpsAsync(detailedProgress: detailedProgress, defaultGateway: CurrentNetwork.DefaultGateway);
            CurrentSpeedMbps = finalSpeed;
            GaugeAngle = CalculateGaugeAngle(finalSpeed);
            ProgressPercentage = 100;
            UpdateSpeedQuality(finalSpeed);
            IsTestCompleted = true; // Show status pill and measurements now!

            // Sentry ga tezlik, ping va Wi-Fi metrikalarini yuborish
            SentryService.TrackSpeedTestResult(finalSpeed, PingLatencyMs, PeakSpeedMbps);
            if (CurrentNetwork.ConnectionType == NetworkConnectionType.WiFi && CurrentNetwork.SignalPercentage > 0)
            {
                SentryService.TrackWiFiSignal(CurrentNetwork.SignalPercentage, CurrentNetwork.BandDisplay);
            }

            transaction.Finish(SpanStatus.Ok);
        }
        catch (Exception ex)
        {
            transaction.Finish(SpanStatus.InternalError);
            AppLogger.LogError("[SpeedTest Error]", ex);
            SpeedTestStatus = LocalizationService.Get("StatusActionError", ex.Message);
        }
        finally
        {
            IsTestingSpeed = false;
        }
    }

    private void UpdateSpeedQuality(double speed)
    {
        if (speed >= 100)
        {
            SpeedQualityText = LocalizationService.Get("SpeedQualityExcellent");
            SpeedQualityColor = "#10B981";
        }
        else if (speed >= 50)
        {
            SpeedQualityText = LocalizationService.Get("SpeedQualityGood");
            SpeedQualityColor = "#38BDF8";
        }
        else if (speed >= 20)
        {
            SpeedQualityText = LocalizationService.Get("SpeedQualityFair");
            SpeedQualityColor = "#F59E0B";
        }
        else if (speed > 0)
        {
            SpeedQualityText = LocalizationService.Get("SpeedQualitySlow");
            SpeedQualityColor = "#EF4444";
        }
        else
        {
            SpeedQualityText = LocalizationService.Get("SpeedQualityNotTested");
            SpeedQualityColor = "#94A3B8";
        }
    }

    private static double CalculateGaugeAngle(double speed)
    {
        if (speed <= 0) return -120.0;
        double normalized = Math.Sqrt(Math.Min(speed, 250.0)) / Math.Sqrt(250.0);
        return Math.Clamp(-120.0 + (normalized * 240.0), -120.0, 120.0);
    }

    [RelayCommand]
    public void RestartAsAdmin()
    {
        SentryService.TrackFeatureUsage("restart_as_admin");
        AdminHelper.RestartAsAdministrator();
    }

    [RelayCommand]
    public void CopyToClipboard(object? obj)
    {
        if (obj == null) return;
        var text = obj.ToString();
        if (string.IsNullOrWhiteSpace(text)) return;
        SentryService.TrackFeatureUsage("copy_to_clipboard");
        try
        {
            System.Windows.Clipboard.SetText(text);
            StatusMessage = LocalizationService.Get("StatusCopied", text);
            AppLogger.Log($"[NetPulse] Copied to clipboard: {text}");
        }
        catch (Exception ex)
        {
            AppLogger.LogError("[NetPulse Clipboard Error]", ex);
            StatusMessage = LocalizationService.Get("StatusCopyError", ex.Message);
        }
    }

    [RelayCommand]
    public void ClearRepairLog()
    {
        SentryService.TrackFeatureUsage("repair_log_action", new() { ["action"] = "clear" });
        RepairLog = string.Empty;
        StatusMessage = LocalizationService.Get("StatusLogCleared");
    }

    [RelayCommand]
    public void CopyRepairLog()
    {
        if (string.IsNullOrWhiteSpace(RepairLog)) return;
        SentryService.TrackFeatureUsage("repair_log_action", new() { ["action"] = "copy" });
        try
        {
            System.Windows.Clipboard.SetText(RepairLog);
            StatusMessage = LocalizationService.Get("StatusLogCopied");
        }
        catch (Exception ex)
        {
            StatusMessage = LocalizationService.Get("StatusCopyError", ex.Message);
        }
    }

    private void AppendRepairLog(string text)
    {
        RepairLog += $"[{DateTime.Now:HH:mm:ss}] {text}\n";
    }
}
