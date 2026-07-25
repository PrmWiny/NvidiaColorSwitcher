using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NvidiaColorSwitcher.Models;
using NvidiaColorSwitcher.Services;

namespace NvidiaColorSwitcher.ViewModels
{
    public class MainViewModel : ObservableObject
    {
        private readonly NvidiaService _nvidiaService;
        private readonly ProfileStorageService _storageService;

        private int _digitalVibrance = 50;
        private float _brightness = 0.0f;
        private float _contrast = 0.0f;
        private float _gamma = 1.0f;
        private string _newProfileName = string.Empty;
        private ColorProfile? _selectedProfile;
        private bool _isNvidiaAvailable;
        private string _statusMessage = "Ready";
        private bool _isLivePreviewEnabled = true;

        private int _appliedDigitalVibrance = 50;
        private float _appliedBrightness = 0.0f;
        private float _appliedContrast = 0.0f;
        private float _appliedGamma = 1.0f;

        public event EventHandler? ProfilesUpdated;

        public ObservableCollection<ColorProfile> Profiles { get; } = new();

        public string DigitalVibranceText => $"{DigitalVibrance}%";
        public string BrightnessText => $"{Brightness:+#0.0;-#0.0;0.0}%";
        public string ContrastText => $"{Contrast:+#0.0;-#0.0;0.0}%";
        public string GammaText => $"{Gamma:0.00}";

        public int DigitalVibrance
        {
            get => _digitalVibrance;
            set
            {
                if (SetProperty(ref _digitalVibrance, value))
                {
                    OnPropertyChanged(nameof(DigitalVibranceText));
                    OnSliderValueChanged();
                }
            }
        }

        public float Brightness
        {
            get => _brightness;
            set
            {
                if (SetProperty(ref _brightness, (float)Math.Round(value, 1)))
                {
                    OnPropertyChanged(nameof(BrightnessText));
                    OnSliderValueChanged();
                }
            }
        }

        public float Contrast
        {
            get => _contrast;
            set
            {
                if (SetProperty(ref _contrast, (float)Math.Round(value, 1)))
                {
                    OnPropertyChanged(nameof(ContrastText));
                    OnSliderValueChanged();
                }
            }
        }

        public float Gamma
        {
            get => _gamma;
            set
            {
                if (SetProperty(ref _gamma, (float)Math.Round(value, 2)))
                {
                    OnPropertyChanged(nameof(GammaText));
                    OnSliderValueChanged();
                }
            }
        }

        public string NewProfileName
        {
            get => _newProfileName;
            set => SetProperty(ref _newProfileName, value);
        }

        public ColorProfile? SelectedProfile
        {
            get => _selectedProfile;
            set
            {
                if (SetProperty(ref _selectedProfile, value) && value != null)
                {
                    LoadProfileIntoSliders(value);
                }
            }
        }

        public bool IsNvidiaAvailable
        {
            get => _isNvidiaAvailable;
            set => SetProperty(ref _isNvidiaAvailable, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public bool IsLivePreviewEnabled
        {
            get => _isLivePreviewEnabled;
            set => SetProperty(ref _isLivePreviewEnabled, value);
        }

        public bool IsApplyEnabled
        {
            get => true;
            set { }
        }

        public void UpdateApplyState()
        {
            OnPropertyChanged(nameof(IsApplyEnabled));
        }

        private bool _isAutoStartupEnabled;

        public bool IsAutoStartupEnabled
        {
            get => _isAutoStartupEnabled;
            set
            {
                if (SetProperty(ref _isAutoStartupEnabled, value))
                {
                    bool success = StartupService.SetAutoStartup(value);
                    if (!success)
                    {
                        StatusMessage = "Failed to update Windows Registry startup";
                    }
                    else
                    {
                        StatusMessage = value ? "Windows Auto-Startup Enabled" : "Windows Auto-Startup Disabled";
                    }
                }
            }
        }

        #region Commands

        public ICommand ApplySelectedProfileCommand { get; }
        public ICommand SaveProfileCommand { get; }
        public ICommand DeleteProfileCommand { get; }
        public ICommand ResetToDefaultCommand { get; }
        public ICommand SelectProfileCommand { get; }
        public ICommand ResetVibranceCommand { get; }
        public ICommand ResetBrightnessCommand { get; }
        public ICommand ResetContrastCommand { get; }
        public ICommand ResetGammaCommand { get; }

        #endregion

        public MainViewModel(NvidiaService nvidiaService, ProfileStorageService storageService)
        {
            _nvidiaService = nvidiaService;
            _storageService = storageService;

            ApplySelectedProfileCommand = new RelayCommand(ApplyCurrentSettings);
            SaveProfileCommand = new RelayCommand(SaveCurrentProfile);
            DeleteProfileCommand = new RelayCommand<ColorProfile>(DeleteProfile);
            ResetToDefaultCommand = new RelayCommand(ResetToDefault);
            SelectProfileCommand = new RelayCommand<ColorProfile>(SelectProfile);

            ResetVibranceCommand = new RelayCommand(ResetVibrance);
            ResetBrightnessCommand = new RelayCommand(ResetBrightness);
            ResetContrastCommand = new RelayCommand(ResetContrast);
            ResetGammaCommand = new RelayCommand(ResetGamma);

            _isAutoStartupEnabled = StartupService.IsAutoStartupEnabled();
            OnPropertyChanged(nameof(IsAutoStartupEnabled));

            CheckHardwareStatus();
            LoadProfilesFromStorage();
        }

        public void CheckHardwareStatus()
        {
            IsNvidiaAvailable = _nvidiaService.IsNvidiaAvailable();
            StatusMessage = IsNvidiaAvailable 
                ? "NVIDIA NVAPI Connected" 
                : "Standard GDI Calibration Active (NVIDIA GPU Not Detected)";
        }

        public void LoadProfilesFromStorage()
        {
            var loadedProfiles = _storageService.LoadProfiles();
            string activeId = _storageService.GetActiveProfileId();

            int? currentHardwareVibrance = _nvidiaService.GetCurrentDigitalVibrance();

            Profiles.Clear();
            foreach (var p in loadedProfiles)
            {
                p.IsActive = (p.Id == activeId);
                // If initializing default profile and hardware vibrance was detected, sync default profile to current hardware state
                if (p.Id == "default-stock" && currentHardwareVibrance.HasValue && !_storageService.HasCustomConfig())
                {
                    p.DigitalVibrance = currentHardwareVibrance.Value;
                }
                Profiles.Add(p);
            }

            var activeProfile = Profiles.FirstOrDefault(p => p.Id == activeId) ?? Profiles.FirstOrDefault();
            if (activeProfile != null)
            {
                _selectedProfile = activeProfile;
                OnPropertyChanged(nameof(SelectedProfile));
                LoadProfileIntoSliders(activeProfile);

                if (currentHardwareVibrance.HasValue && activeProfile.Id == "default-stock")
                {
                    _isLivePreviewEnabled = false;
                    DigitalVibrance = currentHardwareVibrance.Value;
                    _isLivePreviewEnabled = true;
                }
            }

            ProfilesUpdated?.Invoke(this, EventArgs.Empty);
        }

        private void OnSliderValueChanged()
        {
            UpdateApplyState();
            if (!_isLivePreviewEnabled) return;

            var activeProfile = new ColorProfile
            {
                DigitalVibrance = DigitalVibrance,
                Brightness = Brightness,
                Contrast = Contrast,
                Gamma = Gamma
            };

            _nvidiaService.ApplyProfile(activeProfile);
        }

        public void SelectProfile(ColorProfile? profile)
        {
            if (profile == null) return;

            _selectedProfile = profile;
            OnPropertyChanged(nameof(SelectedProfile));

            LoadProfileIntoSliders(profile);
        }

        private void LoadProfileIntoSliders(ColorProfile profile)
        {
            _isLivePreviewEnabled = false;

            DigitalVibrance = profile.DigitalVibrance;
            Brightness = profile.Brightness;
            Contrast = profile.Contrast;
            Gamma = profile.Gamma;
            NewProfileName = profile.Name;

            _isLivePreviewEnabled = true;

            _appliedDigitalVibrance = profile.DigitalVibrance;
            _appliedBrightness = profile.Brightness;
            _appliedContrast = profile.Contrast;
            _appliedGamma = profile.Gamma;

            UpdateApplyState();
        }

        public void ApplyProfile(ColorProfile? profile)
        {
            if (profile == null) return;

            _nvidiaService.ApplyProfile(profile);

            _appliedDigitalVibrance = profile.DigitalVibrance;
            _appliedBrightness = profile.Brightness;
            _appliedContrast = profile.Contrast;
            _appliedGamma = profile.Gamma;
            UpdateApplyState();

            foreach (var p in Profiles)
            {
                p.IsActive = (p.Id == profile.Id);
            }

            _storageService.SaveActiveProfileId(profile.Id);
            StatusMessage = $"Applied profile: '{profile.Name}'";
            ProfilesUpdated?.Invoke(this, EventArgs.Empty);
        }

        public void ApplyCurrentSettings()
        {
            var profileToApply = SelectedProfile ?? new ColorProfile
            {
                Id = Guid.NewGuid().ToString(),
                Name = string.IsNullOrWhiteSpace(NewProfileName) ? "Custom Profile" : NewProfileName.Trim(),
                DigitalVibrance = DigitalVibrance,
                Brightness = Brightness,
                Contrast = Contrast,
                Gamma = Gamma
            };

            profileToApply.DigitalVibrance = DigitalVibrance;
            profileToApply.Brightness = Brightness;
            profileToApply.Contrast = Contrast;
            profileToApply.Gamma = Gamma;

            ApplyProfile(profileToApply);
        }

        public void SaveCurrentProfile()
        {
            string name = string.IsNullOrWhiteSpace(NewProfileName) ? "Custom Profile" : NewProfileName.Trim();

            // 1. If currently selected profile is non-default and name matches or is unedited, update it!
            if (SelectedProfile != null && !SelectedProfile.IsDefault &&
                (SelectedProfile.Name.Equals(name, StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(NewProfileName)))
            {
                SelectedProfile.Name = name;
                SelectedProfile.DigitalVibrance = DigitalVibrance;
                SelectedProfile.Brightness = Brightness;
                SelectedProfile.Contrast = Contrast;
                SelectedProfile.Gamma = Gamma;

                _storageService.SaveProfiles(Profiles.ToList());
                ApplyProfile(SelectedProfile);
                StatusMessage = $"Updated profile '{name}'";
            }
            else
            {
                // 2. Check if another existing non-default profile matches this name
                var existingProfile = Profiles.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (existingProfile != null && !existingProfile.IsDefault)
                {
                    existingProfile.DigitalVibrance = DigitalVibrance;
                    existingProfile.Brightness = Brightness;
                    existingProfile.Contrast = Contrast;
                    existingProfile.Gamma = Gamma;

                    _storageService.SaveProfiles(Profiles.ToList());
                    SelectProfile(existingProfile);
                    ApplyProfile(existingProfile);
                    StatusMessage = $"Updated profile '{name}'";
                }
                else
                {
                    // 3. Create a new custom profile
                    var newProfile = new ColorProfile
                    {
                        Id = Guid.NewGuid().ToString(),
                        Name = name,
                        DigitalVibrance = DigitalVibrance,
                        Brightness = Brightness,
                        Contrast = Contrast,
                        Gamma = Gamma,
                        IsDefault = false
                    };

                    Profiles.Add(newProfile);
                    _storageService.SaveProfiles(Profiles.ToList());
                    SelectProfile(newProfile);
                    ApplyProfile(newProfile);
                    StatusMessage = $"Saved new profile '{name}'";
                }
            }

            ProfilesUpdated?.Invoke(this, EventArgs.Empty);
        }

        public void DeleteProfile(ColorProfile? profile)
        {
            if (profile == null) return;

            if (profile.IsDefault || profile.Id == "default-stock")
            {
                StatusMessage = "Cannot delete default stock profile";
                System.Windows.MessageBox.Show(
                    "The default stock profile cannot be deleted.",
                    "Delete Profile",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
                return;
            }

            var confirmResult = System.Windows.MessageBox.Show(
                $"Are you sure you want to delete profile '{profile.Name}'?",
                "Confirm Delete Profile",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (confirmResult != System.Windows.MessageBoxResult.Yes)
            {
                return;
            }

            Profiles.Remove(profile);
            _storageService.SaveProfiles(Profiles.ToList());

            StatusMessage = $"Deleted profile '{profile.Name}'";

            var defaultProfile = Profiles.FirstOrDefault(p => p.IsDefault) ?? Profiles.FirstOrDefault();
            if (defaultProfile != null)
            {
                SelectProfile(defaultProfile);
                ApplyProfile(defaultProfile);
            }

            ProfilesUpdated?.Invoke(this, EventArgs.Empty);
        }

        public void ResetToDefault()
        {
            _nvidiaService.ResetHardwareToDefaults();

            var defaultProfile = Profiles.FirstOrDefault(p => p.Id == "default-stock") ?? Profiles.FirstOrDefault(p => p.IsDefault);
            if (defaultProfile != null)
            {
                var factoryDefault = _nvidiaService.GetDefaultProfile();
                defaultProfile.DigitalVibrance = factoryDefault.DigitalVibrance;
                defaultProfile.Brightness = factoryDefault.Brightness;
                defaultProfile.Contrast = factoryDefault.Contrast;
                defaultProfile.Gamma = factoryDefault.Gamma;

                _selectedProfile = defaultProfile;
                OnPropertyChanged(nameof(SelectedProfile));
                LoadProfileIntoSliders(defaultProfile);
            }

            _storageService.SaveActiveProfileId("default-stock");
            _storageService.SaveProfiles(Profiles.ToList());

            StatusMessage = "Restored hardware & NVIDIA display settings to factory defaults";
            ProfilesUpdated?.Invoke(this, EventArgs.Empty);
        }

        public void ResetVibrance()
        {
            int defaultVal = _nvidiaService.GetDefaultProfile().DigitalVibrance;
            DigitalVibrance = defaultVal;
            StatusMessage = $"Reset Digital Vibrance to default ({defaultVal}%)";
        }

        public void ResetBrightness()
        {
            Brightness = 0.0f;
            StatusMessage = "Reset Brightness to default (0.0%)";
        }

        public void ResetContrast()
        {
            Contrast = 0.0f;
            StatusMessage = "Reset Contrast to default (0.0%)";
        }

        public void ResetGamma()
        {
            Gamma = 1.0f;
            StatusMessage = "Reset Gamma to default (1.00)";
        }
    }
}
