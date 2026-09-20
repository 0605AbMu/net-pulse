using System.Reflection;

namespace NetPulse.Core;

/// <summary>
/// Dasturning haqiqiy versiyasini (GitHub Actions CI da yig'ilgan yoki mahalliy)
/// assembly metama'lumotlaridan dinamik aniqlovchi yordamchi klass.
/// </summary>
public static class AppVersionHelper
{
    private static string? _cachedVersion;
    private static string? _cachedDisplayVersion;

    /// <summary>
    /// Toza semantik versiya (masalan: "1.0.1" yoki "1.1.0").
    /// Telemetriya, loglar va Sentry uchun ishlatiladi.
    /// </summary>
    public static string Version => _cachedVersion ??= ResolveVersion();

    /// <summary>
    /// Interfeysda ko'rsatiladigan formatlangan versiya (masalan: "v1.0.1").
    /// MainWindow sarlavha va status panellarida foydalaniladi.
    /// </summary>
    public static string DisplayVersion => _cachedDisplayVersion ??= ResolveDisplayVersion();

    private static string ResolveVersion()
    {
        try
        {
            var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();

            // 1. CI orqali o'rnatiladigan InformationalVersion (masalan -p:InformationalVersion="1.0.1")
            var infoAttr = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            if (!string.IsNullOrWhiteSpace(infoAttr?.InformationalVersion))
            {
                var ver = infoAttr.InformationalVersion;
                // Agar .NET SDK git commit hash qo'shgan bo'lsa (masalan: "1.0.1+662530..."), uni ajratib olamiz
                var plusIndex = ver.IndexOf('+');
                if (plusIndex >= 0)
                {
                    ver = ver[..plusIndex];
                }
                ver = ver.Trim();
                if (!string.IsNullOrWhiteSpace(ver))
                {
                    return ver;
                }
            }

            // 2. FileVersion tekshirish
            var fileAttr = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>();
            if (!string.IsNullOrWhiteSpace(fileAttr?.Version))
            {
                var fileVer = fileAttr.Version.Trim();
                if (System.Version.TryParse(fileVer, out var parsedFv))
                {
                    return parsedFv.Revision > 0 ? parsedFv.ToString() : parsedFv.ToString(3);
                }
                return fileVer;
            }

            // 3. Standart AssemblyVersion
            var asmVer = assembly.GetName().Version;
            if (asmVer != null)
            {
                return (asmVer.Build > 0 || asmVer.Revision > 0)
                    ? (asmVer.Revision > 0 ? asmVer.ToString() : asmVer.ToString(3))
                    : asmVer.ToString(2);
            }
        }
        catch (Exception ex)
        {
            AppLogger.LogError("[AppVersionHelper] Versiyani aniqlashda xatolik", ex);
        }

        return "1.0.0";
    }

    private static string ResolveDisplayVersion()
    {
        var ver = Version.Trim();
        return ver.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? ver : $"v{ver}";
    }
}
