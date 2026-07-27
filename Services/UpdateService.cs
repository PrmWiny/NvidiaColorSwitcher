using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace NvidiaColorSwitcher.Services
{
    public class UpdateInfo
    {
        public string Version { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
        public string ReleaseNotes { get; set; } = string.Empty;
    }

    public class UpdateService
    {
        private const string GitHubApiUrl = "https://api.github.com/repos/PrmWiny/Nvidia-Color-Profile-Setting/releases/latest";
        private static readonly HttpClient _httpClient = new();

        public static Version CurrentVersion
        {
            get
            {
                var ver = Assembly.GetExecutingAssembly().GetName().Version;
                return ver ?? new Version(1, 0, 2);
            }
        }

        public UpdateService()
        {
            if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
            {
                _httpClient.DefaultRequestHeaders.Add("User-Agent", "NvidiaColorSwitcher-App");
            }
        }

        /// <summary>
        /// Asynchronously checks GitHub Releases for a newer version of NvidiaColorSwitcher.
        /// </summary>
        public async Task<UpdateInfo?> CheckForUpdatesAsync()
        {
            try
            {
                using var response = await _httpClient.GetAsync(GitHubApiUrl);
                if (!response.IsSuccessStatusCode) return null;

                string json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (!root.TryGetProperty("tag_name", out var tagProp)) return null;

                string rawTag = tagProp.GetString() ?? "";
                string cleanVersion = rawTag.TrimStart('v', 'V').Trim();

                if (Version.TryParse(cleanVersion, out var remoteVersion))
                {
                    if (remoteVersion > CurrentVersion)
                    {
                        string downloadUrl = string.Empty;
                        if (root.TryGetProperty("assets", out var assetsProp) && assetsProp.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var asset in assetsProp.EnumerateArray())
                            {
                                string name = asset.GetProperty("name").GetString() ?? "";
                                if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                                {
                                    downloadUrl = asset.GetProperty("browser_download_url").GetString() ?? "";
                                    break;
                                }
                            }
                        }

                        string body = root.TryGetProperty("body", out var bodyProp) ? bodyProp.GetString() ?? "" : "";

                        return new UpdateInfo
                        {
                            Version = cleanVersion,
                            DownloadUrl = downloadUrl,
                            ReleaseNotes = body
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UpdateService] Failed to check for updates: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Downloads the update file and launches auto-replacement script.
        /// </summary>
        public async Task<bool> DownloadAndApplyUpdateAsync(string downloadUrl, Action<int>? progressCallback = null)
        {
            if (string.IsNullOrEmpty(downloadUrl)) return false;

            try
            {
                string tempExePath = Path.Combine(Path.GetTempPath(), "NvidiaColorSwitcher_Update.exe");
                
                using (var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();

                    long? totalBytes = response.Content.Headers.ContentLength;
                    using var contentStream = await response.Content.ReadAsStreamAsync();
                    using var fileStream = new FileStream(tempExePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                    byte[] buffer = new byte[8192];
                    long totalRead = 0;
                    int bytesRead;

                    while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead);
                        totalRead += bytesRead;

                        if (totalBytes.HasValue && totalBytes.Value > 0)
                        {
                            int progress = (int)((totalRead * 100) / totalBytes.Value);
                            progressCallback?.Invoke(progress);
                        }
                    }
                }

                ApplyUpdateAndRestart(tempExePath);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UpdateService] Error downloading update: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Creates batch script to safely replace executable and restart app after process exit.
        /// </summary>
        private static void ApplyUpdateAndRestart(string downloadedFilePath)
        {
            string? currentExe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(currentExe) || currentExe.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                currentExe = Process.GetCurrentProcess().MainModule?.FileName;
            }

            if (string.IsNullOrEmpty(currentExe)) return;

            string tempBatPath = Path.Combine(Path.GetTempPath(), "ncs_update_installer.bat");

            string scriptContent = $@"@echo off
timeout /t 2 /nobreak > NUL
copy /Y ""{downloadedFilePath}"" ""{currentExe}""
del /F /Q ""{downloadedFilePath}""
start """" ""{currentExe}""
del /F /Q ""%~f0""
";

            File.WriteAllText(tempBatPath, scriptContent);

            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{tempBatPath}\"",
                CreateNoWindow = true,
                UseShellExecute = false
            });

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                System.Windows.Application.Current.Shutdown();
            });
        }
    }
}
