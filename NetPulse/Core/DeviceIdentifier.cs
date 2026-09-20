using System.IO;
using Microsoft.Win32;

namespace NetPulse.Core;

/// <summary>
/// Har bir kompyuterni yagona foydalanuvchi sifatida identifikatsiyalash uchun
/// barqaror va doimiy DeviceId taqdim etadi.
/// </summary>
public static class DeviceIdentifier
{
    private static string? _cachedDeviceId;
    private static readonly object _syncRoot = new();

    public static string GetDeviceId()
    {
        if (!string.IsNullOrEmpty(_cachedDeviceId))
        {
            return _cachedDeviceId;
        }

        lock (_syncRoot)
        {
            if (!string.IsNullOrEmpty(_cachedDeviceId))
            {
                return _cachedDeviceId;
            }

            // 1. Windows MachineGuid ni HKLM dan o'qishga harakat qilamiz
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
                using var subKey = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
                var guid = subKey?.GetValue("MachineGuid")?.ToString();
                if (!string.IsNullOrWhiteSpace(guid))
                {
                    _cachedDeviceId = guid.Trim().ToLowerInvariant();
                    return _cachedDeviceId;
                }
            }
            catch
            {
                // Registrdan o'qishda xatolik bo'lsa, zaxira variantiga o'tamiz
            }

            // 2. Registr mavjud bo'lmasa, %LocalAppData%\NetPulse\device.id faylidan o'qiymiz yoki yangi UUID yaratamiz
            try
            {
                var appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NetPulse");
                Directory.CreateDirectory(appDataFolder);

                var deviceFile = Path.Combine(appDataFolder, "device.id");
                if (File.Exists(deviceFile))
                {
                    var savedId = File.ReadAllText(deviceFile).Trim();
                    if (!string.IsNullOrWhiteSpace(savedId))
                    {
                        _cachedDeviceId = savedId.ToLowerInvariant();
                        return _cachedDeviceId;
                    }
                }

                var newId = Guid.NewGuid().ToString("D").ToLowerInvariant();
                File.WriteAllText(deviceFile, newId);
                _cachedDeviceId = newId;
                return _cachedDeviceId;
            }
            catch
            {
                // Fallback sifatida kompyuter nomi va hash
                _cachedDeviceId = $"machine-{Environment.MachineName.ToLowerInvariant()}";
                return _cachedDeviceId;
            }
        }
    }
}
