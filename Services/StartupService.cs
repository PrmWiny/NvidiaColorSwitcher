using System;
using Microsoft.Win32;

namespace NvidiaColorSwitcher.Services
{
    /// <summary>
    /// Helper service for managing Windows Startup registry keys (HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run).
    /// </summary>
    public static class StartupService
    {
        private const string RunRegistryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "NvidiaColorSwitcher";

        /// <summary>
        /// Checks if the app is currently registered in Windows HKCU Startup Registry.
        /// </summary>
        public static bool IsAutoStartupEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, false);
                var value = key?.GetValue(AppName) as string;
                return !string.IsNullOrEmpty(value);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Enables or disables Windows Auto-Startup for NvidiaColorSwitcher.
        /// </summary>
        public static bool SetAutoStartup(bool enable)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, true);
                if (key == null) return false;

                if (enable)
                {
                    string? exePath = Environment.ProcessPath;
                    if (string.IsNullOrEmpty(exePath) || exePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                    {
                        exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                    }

                    if (!string.IsNullOrEmpty(exePath))
                    {
                        key.SetValue(AppName, $"\"{exePath}\" --autostart");
                    }
                }
                else
                {
                    if (key.GetValue(AppName) != null)
                    {
                        key.DeleteValue(AppName, false);
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[StartupService] Error setting registry startup: {ex.Message}");
                return false;
            }
        }
    }
}
