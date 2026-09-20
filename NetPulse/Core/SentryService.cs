using System.Diagnostics;
using System.Reflection;
using Sentry;

namespace NetPulse.Core;

public static class SentryService
{
    private const string SentryDsn = "https://31e57479844034b261f201325883a38e@o4510963491536896.ingest.us.sentry.io/4512118384033792";
    private static IDisposable? _sentryClient;
    private static bool _isInitialized;

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

                // Faqatgina xatolik statusiga ega bo'lgan trace (transaction)lar yuborilsin
                options.SetBeforeSendTransaction((transaction, hint) =>
                {
                    bool isError = (transaction.Status != null && transaction.Status != SpanStatus.Ok)
                        || transaction.Spans.Any(s => s.Status != null && s.Status != SpanStatus.Ok)
                        || transaction.Tags.ContainsKey("error")
                        || transaction.Data.ContainsKey("error");

                    return isError ? transaction : null;
                });
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

    private static Timer? _heartbeatTimer;

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
