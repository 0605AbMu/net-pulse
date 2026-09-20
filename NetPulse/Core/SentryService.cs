using System.Diagnostics;
using System.Reflection;
using NetPulse.Models;
using Sentry;

namespace NetPulse.Core;

public static class SentryService
{
    private const string SentryDsn = "https://31e57479844034b261f201325883a38e@o4510963491536896.ingest.us.sentry.io/4512118384033792";
    private static IDisposable? _sentryClient;
    private static bool _isInitialized;
    private static Timer? _heartbeatTimer;

    public static string DeviceId => DeviceIdentifier.GetDeviceId();

    public static void Initialize()
    {
        if (_isInitialized) return;

        try
        {
            var deviceId = DeviceId;
            var appVersion = AppVersionHelper.Version;

            _sentryClient = SentrySdk.Init(options =>
            {
                options.Dsn = SentryDsn;
                options.Release = $"NetPulse@{appVersion}";
                options.Environment = Debugger.IsAttached ? "development" : "production";
                options.EnableLogs = true;
                options.TracesSampleRate = 1.0;
                options.AutoSessionTracking = true;
            });

            // Har bir kompyuterni alohida user sifatida belgilaymiz
            SentrySdk.ConfigureScope(scope =>
            {
                scope.User = new SentryUser
                {
                    Id = deviceId,
                    Username = Environment.MachineName
                };
                scope.SetTag("device_id", deviceId);
                scope.SetTag("machine_name", Environment.MachineName);
                scope.SetTag("os", Environment.OSVersion.VersionString);
                scope.SetTag("is_admin", AdminHelper.IsAdministrator().ToString());
            });

            _isInitialized = true;

            // 1. Dastur ishga tushganda device_id bo'yicha unikal mijoz metrikasini jo'natish
            TrackClientActive("app_launch");

            // 2. Dastur ochiq turganda har 1 soatda mijoz faolligini qayd etib boruvchi taymer
            _heartbeatTimer = new Timer(_ =>
            {
                TrackClientActive("heartbeat");
            }, null, TimeSpan.FromHours(1), TimeSpan.FromHours(1));
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"[Sentry] Initializatsiyada xatolik: {ex.Message}");
        }
    }

    /// <summary>
    /// Sentry Scope'da "Network" kontekstini dinamik yangilaydi.
    /// Xatolik yoki tranzaksiya yuz berganda kompyuterning tarmoq holati to'liq ko'rinadi.
    /// </summary>
    public static void SetNetworkContext(NetworkInfo? netInfo)
    {
        if (netInfo == null) return;

        try
        {
            SentrySdk.ConfigureScope(scope =>
            {
                scope.SetTag("connection_type", netInfo.ConnectionTypeDisplay);
                scope.SetTag("is_connected", netInfo.IsConnected.ToString());

                if (!string.IsNullOrWhiteSpace(netInfo.Ssid))
                    scope.SetTag("wifi_ssid", netInfo.Ssid);

                if (!string.IsNullOrWhiteSpace(netInfo.AdapterName))
                    scope.SetTag("adapter_name", netInfo.AdapterName);

                var isWifi = netInfo.ConnectionType == NetworkConnectionType.WiFi;

                var networkContext = new Dictionary<string, object?>
                {
                    ["adapter_name"] = netInfo.AdapterName,
                    ["interface_description"] = netInfo.InterfaceDescription,
                    ["connection_type"] = netInfo.ConnectionTypeDisplay,
                    ["is_connected"] = netInfo.IsConnected,
                    ["is_wifi"] = isWifi,
                    ["ssid"] = netInfo.Ssid,
                    ["bssid"] = netInfo.Bssid?.ToString(),
                    ["signal_percentage"] = netInfo.SignalPercentage,
                    ["band"] = netInfo.BandDisplay,
                    ["radio_type"] = netInfo.RadioTypeDisplay,
                    ["channel"] = netInfo.Channel,
                    ["receive_rate_mbps"] = netInfo.ReceiveRateMbps,
                    ["transmit_rate_mbps"] = netInfo.TransmitRateMbps,
                    ["ip_address"] = netInfo.IpAddress?.ToString(),
                    ["default_gateway"] = netInfo.DefaultGateway?.ToString(),
                    ["dns_servers"] = string.Join(", ", netInfo.DnsServers.Select(d => d.ToString())),
                    ["driver_version"] = netInfo.DriverVersion?.ToString(),
                    ["driver_date"] = netInfo.DriverDate?.ToString("yyyy-MM-dd"),
                    ["mac_address"] = netInfo.MacAddress?.ToString(),
                    ["device_id"] = DeviceId
                };

                scope.Contexts["network"] = networkContext;
            });
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"[Sentry] SetNetworkContext xatosi: {ex.Message}");
        }
    }

    /// <summary>
    /// Qurilmaning device_id si bo'yicha unikal mijozlar soni va faolligini Sentry metrikalariga yuboradi.
    /// Sentry Metrics konsolida "client.active" yoki "client.count" bo'yicha
    /// count_unique(device_id) funksiyasi orqali jami unikal mijozlar soni ko'rinadi.
    /// </summary>
    public static void TrackClientActive(string source = "app_launch")
    {
        try
        {
            var deviceId = DeviceId;
            var appVersion = AppVersionHelper.Version;

            var tags = new List<KeyValuePair<string, object>>
            {
                new("device_id", deviceId),
                new("source", source),
                new("version", appVersion),
                new("machine_name", Environment.MachineName),
                new("os", Environment.OSVersion.VersionString),
                new("is_admin", AdminHelper.IsAdministrator().ToString())
            };

            // 1. Asosiy mijoz faolligi hisoblagichi (Sentry da count_unique(device_id) orqali unikal clientlar sanaladi)
            SentrySdk.Metrics.EmitCounter("client.active", 1, tags);

            // 2. Har bir unikal qurilma uchun "client.device" hisoblagichi
            SentrySdk.Metrics.EmitCounter("client.device", 1, new List<KeyValuePair<string, object>>
            {
                new("device_id", deviceId),
                new("version", appVersion)
            });

            // 3. Jami mijozlar hisoblagichi (oddiy va tezkor agregatsiya uchun)
            SentrySdk.Metrics.EmitCounter("client.count", 1, new List<KeyValuePair<string, object>>
            {
                new("device_id", deviceId)
            });
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"[Sentry] Client metrikasini yuborishda xatolik: {ex.Message}");
        }
    }

    /// <summary>
    /// Foydalanuvchilarning internet tezligi va ping kechikishi taqsimotini Sentry metrikalariga yuboradi.
    /// Sentry da o'rtacha (avg), p75, p95 va max tezliklar tahlili uchun ishlatiladi.
    /// </summary>
    public static void TrackSpeedTestResult(double downloadMbps, int pingMs, double? peakSpeedMbps = null)
    {
        try
        {
            var tags = new List<KeyValuePair<string, object>>
            {
                new("device_id", DeviceId),
                new("version", AppVersionHelper.Version)
            };

            // 1. Yuklab olish tezligi taqsimoti (Distribution)
            SentrySdk.Metrics.EmitDistribution("speedtest.download_mbps", downloadMbps, MeasurementUnit.None, tags);

            // 2. Ping kechikishi taqsimoti (Distribution)
            if (pingMs > 0)
            {
                SentrySdk.Metrics.EmitDistribution("speedtest.ping_ms", pingMs, MeasurementUnit.Duration.Millisecond, tags);
            }

            // 3. Cho'qqi (Peak) tezlik ko'rsatkichi (Gauge)
            if (peakSpeedMbps.HasValue && peakSpeedMbps.Value > 0)
            {
                SentrySdk.Metrics.EmitGauge("speedtest.peak_mbps", peakSpeedMbps.Value, MeasurementUnit.None, tags);
            }
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"[Sentry] SpeedTest metrikasini yuborishda xatolik: {ex.Message}");
        }
    }

    /// <summary>
    /// Tarmoq salomatlik bali (0-100) va muammolar soni statistikasini Sentry metrikalariga yuboradi.
    /// </summary>
    public static void TrackHealthScore(int score, int totalChecks = 9, int issuesCount = 0)
    {
        try
        {
            var tags = new List<KeyValuePair<string, object>>
            {
                new("device_id", DeviceId),
                new("version", AppVersionHelper.Version),
                new("score_tier", score >= 80 ? "good" : (score >= 50 ? "fair" : "poor"))
            };

            SentrySdk.Metrics.EmitDistribution("network.health_score", score, MeasurementUnit.None, tags);
            SentrySdk.Metrics.EmitGauge("network.health_score_latest", score, MeasurementUnit.None, tags);

            if (issuesCount > 0)
            {
                SentrySdk.Metrics.EmitDistribution("diagnostic.issues_count", issuesCount, MeasurementUnit.None, tags);
            }
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"[Sentry] HealthScore metrikasini yuborishda xatolik: {ex.Message}");
        }
    }

    /// <summary>
    /// Diagnostika davomida aniqlangan muammolar (Warning/Danger) statistikasini Sentry ga hisoblagich sifatida yuboradi.
    /// Sentry Metrics konsolida qaysi tarmoq xatolari eng ko'p uchrayotganini ko'rsatadi.
    /// </summary>
    public static void TrackDiagnosticIssue(string checkId, DiagnosticStatus status, string? issueTitle = null)
    {
        try
        {
            var severity = status switch
            {
                DiagnosticStatus.Danger => "critical",
                DiagnosticStatus.Warning => "warning",
                _ => "info"
            };

            var tags = new List<KeyValuePair<string, object>>
            {
                new("check_id", checkId),
                new("status", status.ToString()),
                new("severity", severity),
                new("device_id", DeviceId),
                new("version", AppVersionHelper.Version)
            };

            if (!string.IsNullOrWhiteSpace(issueTitle))
            {
                tags.Add(new KeyValuePair<string, object>("title", issueTitle));
            }

            SentrySdk.Metrics.EmitCounter("diagnostic.issue_detected", 1, tags);
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"[Sentry] DiagnosticIssue metrikasini yuborishda xatolik: {ex.Message}");
        }
    }

    /// <summary>
    /// Har bir ta'mirlash (repair) amali natijasi, muvaffaqiyati va sarflangan vaqtini Sentry metrikalariga yuboradi.
    /// </summary>
    public static void TrackRepairResult(RepairActionId actionId, bool success, long durationMs, string? errorMessage = null)
    {
        try
        {
            var tags = new List<KeyValuePair<string, object>>
            {
                new("action_id", actionId.ToString()),
                new("status", success ? "success" : "failed"),
                new("is_admin", AdminHelper.IsAdministrator().ToString()),
                new("device_id", DeviceId),
                new("version", AppVersionHelper.Version)
            };

            // Ta'mirlash amallari soni (muvaffaqiyatli vs muvaffaqiyatsiz)
            SentrySdk.Metrics.EmitCounter("repair.executed", 1, tags);

            // Ta'mirlash davomiyligi (ms)
            if (durationMs > 0)
            {
                SentrySdk.Metrics.EmitDistribution("repair.duration_ms", durationMs, MeasurementUnit.Duration.Millisecond, tags);
            }

            if (!success && !string.IsNullOrWhiteSpace(errorMessage))
            {
                AppLogger.LogError($"[Repair Failed - {actionId}] {errorMessage}");
            }
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"[Sentry] RepairResult metrikasini yuborishda xatolik: {ex.Message}");
        }
    }

    /// <summary>
    /// Wi-Fi signal sifati va chastota diapazonini Sentry ga yuboradi.
    /// </summary>
    public static void TrackWiFiSignal(int percentage, string? band = null)
    {
        try
        {
            var tags = new List<KeyValuePair<string, object>>
            {
                new("device_id", DeviceId),
                new("version", AppVersionHelper.Version)
            };

            if (!string.IsNullOrWhiteSpace(band))
            {
                tags.Add(new KeyValuePair<string, object>("wifi_band", band));
            }

            SentrySdk.Metrics.EmitGauge("wifi.signal_percentage", percentage, MeasurementUnit.None, tags);
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"[Sentry] WiFiSignal metrikasini yuborishda xatolik: {ex.Message}");
        }
    }

    /// <summary>
    /// Barcha feature lar uchun foydalanishlar sonini Sentry metrikalariga yuboradi.
    /// Kompyuterning yagona ID si tag sifatida biriktiriladi.
    /// </summary>
    public static void TrackFeatureUsage(string featureName, Dictionary<string, object>? customTags = null)
    {
        try
        {
            var tags = new List<KeyValuePair<string, object>>
            {
                new("feature", featureName),
                new("device_id", DeviceId)
            };

            if (customTags != null)
            {
                foreach (var kvp in customTags)
                {
                    tags.Add(new KeyValuePair<string, object>(kvp.Key, kvp.Value));
                }
            }

            // Umumiy feature.usage va konkret feature hisoblagichini oshiramiz
            SentrySdk.Metrics.EmitCounter("feature.usage", 1, tags);
            SentrySdk.Metrics.EmitCounter($"feature.{featureName}", 1, tags);
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"[Sentry] Metrika yuborishda xatolik ({featureName}): {ex.Message}");
        }
    }

    /// <summary>
    /// Ilova yopilayotganda Sentry buferini yuvish (flush) va resurslarni bo'shatish.
    /// </summary>
    public static void Shutdown()
    {
        try
        {
            _heartbeatTimer?.Dispose();
            _heartbeatTimer = null;

            SentrySdk.Flush(TimeSpan.FromSeconds(2));
            _sentryClient?.Dispose();
            _sentryClient = null;
            _isInitialized = false;
        }
        catch
        {
            // Ignore on exit
        }
    }
}
