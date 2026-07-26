using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using NvidiaColorSwitcher.Models;

namespace NvidiaColorSwitcher.Services
{
    public class AppConfig
    {
        public string ActiveProfileId { get; set; } = "default-stock";
        public bool ApplyOnStartup { get; set; } = false;
    }

    /// <summary>
    /// Service for persisting and managing user color profiles in %AppData%/NvidiaColorSwitcher/profiles.json.
    /// </summary>
    public class ProfileStorageService
    {
        private readonly string _folderPath;
        private readonly string _profilesFilePath;
        private readonly string _configFilePath;
        private readonly JsonSerializerOptions _jsonOptions;

        public ProfileStorageService()
        {
            _folderPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "NvidiaColorSwitcher"
            );
            _profilesFilePath = Path.Combine(_folderPath, "profiles.json");
            _configFilePath = Path.Combine(_folderPath, "config.json");

            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true
            };

            EnsureDirectoryExists();
        }

        private void EnsureDirectoryExists()
        {
            if (!Directory.Exists(_folderPath))
            {
                Directory.CreateDirectory(_folderPath);
            }
        }

        /// <summary>
        /// Loads saved color profiles from profiles.json. If the file does not exist, initializes default profiles.
        /// </summary>
        public List<ColorProfile> LoadProfiles()
        {
            EnsureDirectoryExists();

            if (!File.Exists(_profilesFilePath))
            {
                var defaults = GetInitialDefaultProfiles();
                SaveProfiles(defaults);
                return defaults;
            }

            try
            {
                string json = File.ReadAllText(_profilesFilePath);
                var profiles = JsonSerializer.Deserialize<List<ColorProfile>>(json, _jsonOptions);
                if (profiles == null || profiles.Count == 0)
                {
                    var defaults = GetInitialDefaultProfiles();
                    SaveProfiles(defaults);
                    return defaults;
                }
                return profiles;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ProfileStorageService] Failed to load profiles: {ex.Message}");
                var defaults = GetInitialDefaultProfiles();
                return defaults;
            }
        }

        /// <summary>
        /// Saves all color profiles to profiles.json.
        /// </summary>
        public void SaveProfiles(List<ColorProfile> profiles)
        {
            EnsureDirectoryExists();
            try
            {
                string json = JsonSerializer.Serialize(profiles, _jsonOptions);
                File.WriteAllText(_profilesFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ProfileStorageService] Failed to save profiles: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks if a custom config file exists.
        /// </summary>
        public bool HasCustomConfig()
        {
            return File.Exists(_configFilePath);
        }

        /// <summary>
        /// Checks if auto-applying active profile on startup is enabled.
        /// </summary>
        public bool GetApplyOnStartup()
        {
            EnsureDirectoryExists();
            if (!File.Exists(_configFilePath))
            {
                return false;
            }

            try
            {
                string json = File.ReadAllText(_configFilePath);
                var config = JsonSerializer.Deserialize<AppConfig>(json, _jsonOptions);
                return config?.ApplyOnStartup ?? false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets the active profile ID stored in config.json.
        /// </summary>
        public string GetActiveProfileId()
        {
            EnsureDirectoryExists();
            if (!File.Exists(_configFilePath))
            {
                return "default-stock";
            }

            try
            {
                string json = File.ReadAllText(_configFilePath);
                var config = JsonSerializer.Deserialize<AppConfig>(json, _jsonOptions);
                return config?.ActiveProfileId ?? "default-stock";
            }
            catch
            {
                return "default-stock";
            }
        }

        /// <summary>
        /// Saves the active profile ID to config.json.
        /// </summary>
        public void SaveActiveProfileId(string profileId)
        {
            EnsureDirectoryExists();
            try
            {
                bool currentApply = GetApplyOnStartup();
                var config = new AppConfig { ActiveProfileId = profileId, ApplyOnStartup = currentApply };
                string json = JsonSerializer.Serialize(config, _jsonOptions);
                File.WriteAllText(_configFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ProfileStorageService] Failed to save active profile ID: {ex.Message}");
            }
        }

        /// <summary>
        /// Generates the initial default profile set.
        /// </summary>
        public List<ColorProfile> GetInitialDefaultProfiles()
        {
            return new List<ColorProfile>
            {
                new ColorProfile
                {
                    Id = "default-stock",
                    Name = "Default / Stock",
                    DigitalVibrance = 50,
                    Brightness = 50.0f,
                    Contrast = 50.0f,
                    Gamma = 1.0f,
                    IsDefault = true
                }
            };
        }
    }
}
