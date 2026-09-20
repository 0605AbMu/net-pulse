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
            var appVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

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
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"[Sentry] Initializatsiyada xatolik: {ex.Message}");
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
