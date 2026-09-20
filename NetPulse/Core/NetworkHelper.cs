using System.Diagnostics;
using System.IO;
using System.Management;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using NetPulse.Models;

namespace NetPulse.Core;

public static class NetworkHelper
{
    private static readonly HttpClient HttpClient;

    static NetworkHelper()
    {
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            ConnectTimeout = TimeSpan.FromSeconds(8)
        };
        HttpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(20)
        };
        HttpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36 NetPulse/1.0");
        HttpClient.DefaultRequestHeaders.Add("Accept", "*/*");
    }

    public static WiFiRadioStandard ParseRadioStandard(string? val)
    {
        if (string.IsNullOrWhiteSpace(val)) return WiFiRadioStandard.Unknown;
        var lower = val.ToLowerInvariant();
        if (lower.Contains("802.11be")) return WiFiRadioStandard.WiFi7_80211be;
        if (lower.Contains("802.11ax")) return WiFiRadioStandard.WiFi6_80211ax;
        if (lower.Contains("802.11ac")) return WiFiRadioStandard.WiFi5_80211ac;
        if (lower.Contains("802.11n")) return WiFiRadioStandard.WiFi4_80211n;
        if (lower.Contains("802.11g")) return WiFiRadioStandard.Legacy80211g;
        if (lower.Contains("802.11b")) return WiFiRadioStandard.Legacy80211b;
        return WiFiRadioStandard.Unknown;
    }

    public static WiFiFrequencyBand DetermineBand(int channel, string? radioType = null)
    {
        if (channel > 14)
        {
            if (channel > 180) return WiFiFrequencyBand.Band6GHz;
            return WiFiFrequencyBand.Band5GHz;
        }
        if (channel > 0)
        {
            return WiFiFrequencyBand.Band2_4GHz;
        }
        if (!string.IsNullOrEmpty(radioType))
        {
            var lower = radioType.ToLowerInvariant();
            if (lower.Contains("802.11ac") || lower.Contains("802.11ax") || lower.Contains("802.11a"))
                return WiFiFrequencyBand.Band5GHz;
            if (lower.Contains("802.11b") || lower.Contains("802.11g"))
                return WiFiFrequencyBand.Band2_4GHz;
        }
        return WiFiFrequencyBand.Unknown;
    }

    public static async Task<NetworkInfo> GetActiveNetworkInfoAsync()
    {
        var info = new NetworkInfo();

        try
        {
            // 1. Try to get Wi-Fi interface details via netsh
            var proc = await AdminHelper.RunCommandAsync("netsh.exe", "wlan show interfaces");
            if (proc.ExitCode == 0 && proc.Output.Contains("State") && proc.Output.Contains("connected"))
            {
                info.ConnectionType = NetworkConnectionType.WiFi;
                info.IsConnected = true;

                foreach (var line in proc.Output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var parts = line.Split(':', 2);
                    if (parts.Length < 2) continue;

                    var key = parts[0].Trim();
                    var val = parts[1].Trim();

                    switch (key)
                    {
                        case "Name":
                            info.AdapterName = val;
                            break;
                        case "Description":
                            info.InterfaceDescription = val;
                            break;
                        case "SSID":
                            info.Ssid = val;
                            break;
                        case "BSSID":
                            if (MacAddress.TryParse(val, out var bssid))
                                info.Bssid = bssid;
                            break;
                        case "Radio type":
                            info.RadioType = ParseRadioStandard(val);
                            if (info.Band == WiFiFrequencyBand.Unknown)
                                info.Band = DetermineBand(info.Channel, val);
                            break;
                        case "Channel":
                            if (int.TryParse(val, out var ch))
                            {
                                info.Channel = ch;
                                info.Band = DetermineBand(ch);
                            }
                            break;
                        case "Receive rate (Mbps)":
                            if (double.TryParse(val, System.Globalization.CultureInfo.InvariantCulture, out var rx))
                                info.ReceiveRateMbps = rx;
                            break;
                        case "Transmit rate (Mbps)":
                            if (double.TryParse(val, System.Globalization.CultureInfo.InvariantCulture, out var tx))
                                info.TransmitRateMbps = tx;
                            break;
                        case "Signal":
                            var match = Regex.Match(val, @"\d+");
                            if (match.Success && int.TryParse(match.Value, out var sig))
                                info.SignalPercentage = sig;
                            break;
                    }
                }
            }
        }
        catch { }

        // 2. Query Network Interfaces for IP, Gateway, DNS, MAC
        try
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces();

            NetworkInterface? activeNi = null;
            if (info.IsConnected && !string.IsNullOrEmpty(info.AdapterName))
            {
                foreach (var ni in interfaces)
                {
                    if (ni.OperationalStatus == OperationalStatus.Up &&
                        (ni.Name.Equals(info.AdapterName, StringComparison.OrdinalIgnoreCase) ||
                         ni.Description.Contains(info.InterfaceDescription, StringComparison.OrdinalIgnoreCase)))
                    {
                        activeNi = ni;
                        break;
                    }
                }
            }

            if (activeNi == null)
            {
                foreach (var ni in interfaces)
                {
                    if (ni.OperationalStatus == OperationalStatus.Up &&
                        ni.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                        ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                    {
                        var ipProps = ni.GetIPProperties();
                        if (ipProps.GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork))
                        {
                            activeNi = ni;
                            break;
                        }
                    }
                }
            }

            if (activeNi != null)
            {
                if (!info.IsConnected)
                {
                    info.AdapterName = activeNi.Name;
                    info.InterfaceDescription = activeNi.Description;
                    info.ConnectionType = activeNi.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 
                        ? NetworkConnectionType.WiFi 
                        : NetworkConnectionType.Ethernet;
                    info.IsConnected = true;
                    info.ReceiveRateMbps = activeNi.Speed / 1_000_000.0;
                    info.TransmitRateMbps = activeNi.Speed / 1_000_000.0;
                }

                info.MacAddress = new MacAddress(activeNi.GetPhysicalAddress());

                var ipProps = activeNi.GetIPProperties();
                var ip = ipProps.UnicastAddresses.FirstOrDefault(u => u.Address.AddressFamily == AddressFamily.InterNetwork);
                if (ip != null) info.IpAddress = ip.Address;

                var gw = ipProps.GatewayAddresses.FirstOrDefault(g => g.Address.AddressFamily == AddressFamily.InterNetwork);
                if (gw != null) info.DefaultGateway = gw.Address;

                info.DnsServers = ipProps.DnsAddresses
                    .Where(d => d.AddressFamily == AddressFamily.InterNetwork)
                    .ToArray();
            }
        }
        catch { }

        // 3. Query WMI for Driver Version & Date
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT DeviceName, DriverVersion, DriverDate FROM Win32_PnPSignedDriver WHERE DeviceName LIKE '%Wi-Fi%' OR DeviceName LIKE '%Wireless%' OR DeviceName LIKE '%802.11%' OR DeviceName LIKE '%Ethernet%' OR DeviceName LIKE '%Network%' OR DeviceName LIKE '%Realtek%' OR DeviceName LIKE '%Intel%' OR DeviceName LIKE '%Broadcom%' OR DeviceName LIKE '%Qualcomm%' OR DeviceName LIKE '%MediaTek%'");

            ManagementBaseObject? bestMatch = null;
            int bestPriority = int.MaxValue;

            static bool IsVirtualAdapter(string name)
            {
                var lower = name.ToLowerInvariant();
                return lower.Contains("virtual") ||
                       lower.Contains("direct") ||
                       lower.Contains("bluetooth") ||
                       lower.Contains("loopback") ||
                       lower.Contains("tap-") ||
                       lower.Contains("vpn") ||
                       lower.Contains("hyper-v") ||
                       lower.Contains("vethernet") ||
                       lower.Contains("pacer") ||
                       lower.Contains("pseudo");
            }

            foreach (var obj in searcher.Get())
            {
                var devName = obj["DeviceName"]?.ToString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(devName)) continue;

                var isVirt = IsVirtualAdapter(devName);

                // Priority 1: Exact or strong match with active InterfaceDescription (physical)
                if (!string.IsNullOrEmpty(info.InterfaceDescription) && !isVirt &&
                    (devName.Equals(info.InterfaceDescription, StringComparison.OrdinalIgnoreCase) ||
                     devName.Contains(info.InterfaceDescription, StringComparison.OrdinalIgnoreCase) ||
                     info.InterfaceDescription.Contains(devName, StringComparison.OrdinalIgnoreCase)))
                {
                    bestMatch = obj;
                    bestPriority = 1;
                    break;
                }

                // Priority 2: Non-virtual adapter matching Wi-Fi/Wireless
                if (!isVirt && (devName.Contains("Wi-Fi", StringComparison.OrdinalIgnoreCase) ||
                               devName.Contains("Wireless", StringComparison.OrdinalIgnoreCase) ||
                               devName.Contains("802.11", StringComparison.OrdinalIgnoreCase)))
                {
                    if (bestPriority > 2)
                    {
                        bestMatch = obj;
                        bestPriority = 2;
                    }
                }

                // Priority 3: Non-virtual general network card
                if (!isVirt && bestPriority > 3)
                {
                    bestMatch = obj;
                    bestPriority = 3;
                }
            }

            if (bestMatch != null)
            {
                if (Version.TryParse(bestMatch["DriverVersion"]?.ToString(), out var parsedVer))
                {
                    info.DriverVersion = parsedVer;
                }
                var rawDate = bestMatch["DriverDate"]?.ToString() ?? string.Empty;
                if (rawDate.Length >= 8 &&
                    int.TryParse(rawDate.Substring(0, 4), out var year) &&
                    int.TryParse(rawDate.Substring(4, 2), out var month) &&
                    int.TryParse(rawDate.Substring(6, 2), out var day))
                {
                    info.DriverDate = new DateOnly(year, month, day);
                }
            }
        }
        catch { }

        return info;
    }

    public static async Task<PingResult> PingAsync(string host, int count = 4, int timeoutMs = 1500)
    {
        long totalRoundtrip = 0;
        int successfulPings = 0;
        int failed = 0;

        for (int i = 0; i < count; i++)
        {
            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(host, timeoutMs);
                if (reply.Status == IPStatus.Success)
                {
                    totalRoundtrip += reply.RoundtripTime;
                    successfulPings++;
                }
                else
                {
                    failed++;
                }
            }
            catch
            {
                failed++;
            }
        }

        var avg = successfulPings > 0 ? (int)(totalRoundtrip / successfulPings) : -1;
        var loss = (int)((failed / (double)count) * 100);
        return new PingResult(avg, loss);
    }

    public static async Task<DnsResolutionResult> CheckDnsResolutionAsync(string domain = "google.com")
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var addresses = await System.Net.Dns.GetHostAddressesAsync(domain);
            sw.Stop();
            return new DnsResolutionResult(addresses.Length > 0, (int)sw.ElapsedMilliseconds);
        }
        catch
        {
            sw.Stop();
            return new DnsResolutionResult(false, -1);
        }
    }

    public static async Task<TcpAutoTuningLevel> GetTcpAutoTuningLevelAsync()
    {
        var proc = await AdminHelper.RunCommandAsync("netsh.exe", "interface tcp show global");
        if (proc.ExitCode != 0) return TcpAutoTuningLevel.Unknown;

        var match = Regex.Match(proc.Output, @"Receive Window Auto-Tuning Level\s*:\s*([a-zA-Z]+)", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return match.Groups[1].Value.ToLowerInvariant() switch
            {
                "normal" => TcpAutoTuningLevel.Normal,
                "disabled" => TcpAutoTuningLevel.Disabled,
                "highlyrestricted" => TcpAutoTuningLevel.HighlyRestricted,
                "restricted" => TcpAutoTuningLevel.Restricted,
                "experimental" => TcpAutoTuningLevel.Experimental,
                _ => TcpAutoTuningLevel.Unknown
            };
        }

        return TcpAutoTuningLevel.Unknown;
    }

    public static int? GetNetworkThrottlingIndex()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile");
            if (key != null)
            {
                var val = key.GetValue("NetworkThrottlingIndex");
                if (val is int intVal) return intVal;
                if (val != null && int.TryParse(val.ToString(), out var parsed)) return parsed;
            }
        }
        catch { }
        return null;
    }

    public static async Task<int> GetNearbyNetworksCountOnSameChannelAsync(int currentChannel, MacAddress? ownBssid = null, string? ownSsid = null)
    {
        if (currentChannel <= 0) return 0;
        try
        {
            var proc = await AdminHelper.RunCommandAsync("netsh.exe", "wlan show networks mode=bssid");
            if (proc.ExitCode != 0) return 0;

            var lines = proc.Output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            int count = 0;
            string currentNetworkSsid = string.Empty;
            bool isCurrentNetworkOwn = false;

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("SSID ", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = trimmed.Split(':', 2);
                    if (parts.Length > 1)
                    {
                        currentNetworkSsid = parts[1].Trim();
                        isCurrentNetworkOwn = !string.IsNullOrEmpty(ownSsid) &&
                            string.Equals(currentNetworkSsid, ownSsid, StringComparison.OrdinalIgnoreCase);
                    }
                }
                else if (trimmed.StartsWith("BSSID ", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = trimmed.Split(':', 2);
                    if (parts.Length > 1)
                    {
                        var bssidStr = parts[1].Trim();
                        if (ownBssid.HasValue && MacAddress.TryParse(bssidStr, out var parsedBssid) && parsedBssid == ownBssid.Value)
                        {
                            isCurrentNetworkOwn = true;
                        }
                    }
                }
                else if (trimmed.StartsWith("Channel", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = trimmed.Split(':', 2);
                    if (parts.Length > 1 && int.TryParse(parts[1].Trim(), out var ch) && ch == currentChannel)
                    {
                        if (!isCurrentNetworkOwn)
                        {
                            count++;
                        }
                    }
                }
            }

            return count;
        }
        catch
        {
            return 0;
        }
    }

    public static async Task<double> TestDownloadSpeedMbpsAsync(
        IProgress<double>? progress = null, 
        IProgress<SpeedTestProgress>? detailedProgress = null,
        IPAddress? defaultGateway = null,
        CancellationToken cancellationToken = default)
    {
        const int streamCount = 2;
        const int testDurationMs = 5500;

        // 1. Initial Quick Ping check (real ping to Gateway, 1.1.1.1, or 8.8.8.8)
        int initialPing = 0;
        try
        {
            var pingTargets = new List<string>();
            if (defaultGateway != null) pingTargets.Add(defaultGateway.ToString());
            pingTargets.Add("1.1.1.1");
            pingTargets.Add("8.8.8.8");

            foreach (var target in pingTargets)
            {
                var pingResult = await PingAsync(target, count: 2, timeoutMs: 600);
                if (pingResult.AvgLatencyMs > 0)
                {
                    initialPing = pingResult.AvgLatencyMs;
                    break;
                }
            }

            // Fallback: If ICMP is blocked, measure TCP connection latency to Cloudflare safely
            if (initialPing <= 0)
            {
                try
                {
                    var swPing = Stopwatch.StartNew();
                    using var tcpClient = new TcpClient();
                    using var pingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    pingCts.CancelAfter(800);
                    await tcpClient.ConnectAsync("1.1.1.1", 80, pingCts.Token);
                    if (tcpClient.Connected)
                    {
                        swPing.Stop();
                        initialPing = Math.Max(1, (int)swPing.ElapsedMilliseconds);
                    }
                }
                catch
                {
                    // Ignore timeouts or unreachability safely without orphaned tasks
                }
            }
        }
        catch { }

        var connectingStatus = Localization.LocalizationService.Get("SpeedTest_StatusConnecting", initialPing);

        detailedProgress?.Report(new SpeedTestProgress
        {
            CurrentMbps = 0.0,
            PeakMbps = 0.0,
            TotalMb = 0.0,
            PingMs = initialPing,
            Percent = 5,
            StatusText = connectingStatus
        });

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        long totalBytesRead = 0;
        var overallSw = Stopwatch.StartNew();

        var downloadUrl = "https://speed.cloudflare.com/__down?bytes=50000000";

        // Multi-stream worker tasks
        var downloadTasks = Enumerable.Range(0, streamCount).Select(async _ =>
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
                using var response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                if (!response.IsSuccessStatusCode) return;

                using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
                var buffer = new byte[64 * 1024];
                int read;
                while (!cts.IsCancellationRequested && (read = await stream.ReadAsync(buffer, 0, buffer.Length, cts.Token)) > 0)
                {
                    Interlocked.Add(ref totalBytesRead, read);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception) { }
        }).ToList();

        // 2. Sliding window measurement with Exponential Moving Average (EMA)
        var history = new Queue<(long TimestampMs, long Bytes)>();
        var speedSamples = new List<double>();

        double displayedSpeed = 0.0;
        double peakSpeed = 0.0;

        history.Enqueue((0, 0));

        while (overallSw.ElapsedMilliseconds < testDurationMs)
        {
            if (cancellationToken.IsCancellationRequested) break;

            await Task.Delay(80, CancellationToken.None);

            long nowMs = overallSw.ElapsedMilliseconds;
            long nowBytes = Interlocked.Read(ref totalBytesRead);

            history.Enqueue((nowMs, nowBytes));

            // Purge history older than 800 ms
            while (history.Count > 1 && (nowMs - history.Peek().TimestampMs) > 800)
            {
                history.Dequeue();
            }

            var oldest = history.Peek();
            double windowSec = (nowMs - oldest.TimestampMs) / 1000.0;
            long windowBytes = nowBytes - oldest.Bytes;

            if (windowSec > 0.25 && windowBytes > 0)
            {
                double instantSpeed = (windowBytes * 8.0) / windowSec / 1_000_000.0;

                // Smooth exponential climb
                if (displayedSpeed < 0.1)
                {
                    displayedSpeed = instantSpeed * 0.4;
                }
                else
                {
                    displayedSpeed = (displayedSpeed * 0.75) + (instantSpeed * 0.25);
                }

                if (nowMs > 1200) // Collect samples after warm-up
                {
                    speedSamples.Add(displayedSpeed);
                }

                peakSpeed = Math.Max(peakSpeed, displayedSpeed);
                double totalMb = nowBytes / 1_000_000.0;
                int percent = Math.Min(95, (int)((nowMs / (double)testDurationMs) * 100));

                var currentRounded = Math.Round(displayedSpeed, 1);
                progress?.Report(currentRounded);

                var measuringStatus = Localization.LocalizationService.Get("SpeedTest_StatusMeasuring", currentRounded, totalMb);

                detailedProgress?.Report(new SpeedTestProgress
                {
                    CurrentMbps = currentRounded,
                    PeakMbps = Math.Round(peakSpeed, 1),
                    TotalMb = Math.Round(totalMb, 1),
                    PingMs = initialPing,
                    Percent = percent,
                    StatusText = measuringStatus
                });
            }
        }

        // Stop download workers
        cts.Cancel();
        try { await Task.WhenAll(downloadTasks); } catch { }

        // 3. Final stable speed calculation (Median of plateau)
        double finalSpeed;
        if (speedSamples.Count >= 5)
        {
            var sorted = speedSamples.OrderBy(s => s).ToList();
            int trim = Math.Max(1, sorted.Count / 5);
            var plateau = sorted.Skip(trim).Take(Math.Max(1, sorted.Count - (trim * 2))).ToList();
            finalSpeed = plateau.Count > 0 ? plateau.Average() : sorted.Average();
        }
        else
        {
            finalSpeed = peakSpeed > 0 ? peakSpeed * 0.85 : 0.0;
        }

        var finalRounded = Math.Round(finalSpeed, 1);
        double finalTotalMb = Interlocked.Read(ref totalBytesRead) / 1_000_000.0;

        var completedStatus = Localization.LocalizationService.Get("SpeedTest_StatusCompleted", finalRounded);

        progress?.Report(finalRounded);
        detailedProgress?.Report(new SpeedTestProgress
        {
            CurrentMbps = finalRounded,
            PeakMbps = Math.Round(Math.Max(peakSpeed, finalRounded), 1),
            TotalMb = Math.Round(finalTotalMb, 1),
            PingMs = initialPing,
            Percent = 100,
            StatusText = completedStatus
        });

        return finalRounded;
    }
}
