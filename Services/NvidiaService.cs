using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using NvidiaColorSwitcher.Models;
using NvAPIWrapper.Display;
using NvAPIWrapper;

namespace NvidiaColorSwitcher.Services
{
    /// <summary>
    /// Service for interacting with NVIDIA hardware via NVAPI and Windows GDI Display Calibration.
    /// Provides real-time controls for Digital Vibrance, Brightness, Contrast, and Gamma.
    /// </summary>
    public class NvidiaService
    {
        #region Win32 P/Invoke Declarations

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct RAMP
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
            public ushort[] Red;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
            public ushort[] Green;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
            public ushort[] Blue;
        }

        [DllImport("gdi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CreateDC(string? lpszDriver, string lpszDevice, string? lpszOutput, IntPtr devMode);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool SetDeviceGammaRamp(IntPtr hDC, ref RAMP lpRamp);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        #endregion

        private bool _isNvApiInitialized = false;

        public NvidiaService()
        {
            InitializeNvApi();
        }

        private void InitializeNvApi()
        {
            try
            {
                NVIDIA.Initialize();
                _isNvApiInitialized = true;
            }
            catch (Exception ex)
            {
                _isNvApiInitialized = false;
                Debug.WriteLine($"[NvidiaService] NVAPI Initialization failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks if NVIDIA NVAPI driver and hardware are accessible.
        /// </summary>
        public bool IsNvidiaAvailable()
        {
            if (!_isNvApiInitialized)
            {
                InitializeNvApi();
            }

            if (!_isNvApiInitialized) return false;

            try
            {
                var displays = Display.GetDisplays();
                return displays != null && displays.Length > 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets the current Digital Vibrance percentage (0% to 100%) from the primary NVIDIA display.
        /// Returns null if NVIDIA API is unavailable or reading fails.
        /// </summary>
        public int? GetCurrentDigitalVibrance()
        {
            if (!_isNvApiInitialized)
            {
                InitializeNvApi();
            }

            if (!_isNvApiInitialized) return null;

            try
            {
                var displays = Display.GetDisplays();
                if (displays != null && displays.Length > 0)
                {
                    foreach (var display in displays)
                    {
                        var dvc = display.DigitalVibranceControl;
                        if (dvc != null)
                        {
                            return dvc.CurrentLevel;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NvidiaService] Error reading Digital Vibrance: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Gets the standard default factory color profile.
        /// Queries NVAPI for hardware default level if available.
        /// </summary>
        public ColorProfile GetDefaultProfile()
        {
            int defaultVibrance = 50;
            if (!_isNvApiInitialized)
            {
                InitializeNvApi();
            }

            if (_isNvApiInitialized)
            {
                try
                {
                    var displays = Display.GetDisplays();
                    if (displays != null && displays.Length > 0 && displays[0].DigitalVibranceControl != null)
                    {
                        defaultVibrance = displays[0].DigitalVibranceControl.DefaultLevel;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[NvidiaService] Error reading default vibrance level: {ex.Message}");
                }
            }

            return new ColorProfile
            {
                Id = "default-stock",
                Name = "Default / Stock",
                DigitalVibrance = defaultVibrance,
                Brightness = 50.0f,
                Contrast = 50.0f,
                Gamma = 1.0f,
                IsDefault = true
            };
        }

        /// <summary>
        /// Applies the specified color profile to the primary display and NVIDIA hardware in real-time.
        /// </summary>
        public void ApplyProfile(ColorProfile profile)
        {
            if (profile == null) return;

            // 1. Apply Digital Vibrance via NVAPI
            ApplyDigitalVibrance(profile.DigitalVibrance);

            // 2. Apply Brightness, Contrast, and Gamma via Windows GDI Gamma Ramp
            ApplyGammaRamp(profile.Brightness, profile.Contrast, profile.Gamma);
        }

        /// <summary>
        /// Sets Digital Vibrance percentage (0% to 100%) on all active NVIDIA displays.
        /// </summary>
        public void ApplyDigitalVibrance(int vibrancePercentage)
        {
            if (!_isNvApiInitialized)
            {
                InitializeNvApi();
            }

            if (!_isNvApiInitialized) return;

            try
            {
                int clampedVibrance = Math.Clamp(vibrancePercentage, 0, 100);
                var displays = Display.GetDisplays();
                if (displays == null) return;

                foreach (var display in displays)
                {
                    try
                    {
                        var dvc = display.DigitalVibranceControl;
                        if (dvc != null)
                        {
                            dvc.CurrentLevel = clampedVibrance;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[NvidiaService] Failed to set DVC on display '{display?.Name}': {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NvidiaService] Error applying Digital Vibrance: {ex.Message}");
            }
        }

        /// <summary>
        /// Calculates and applies a custom Hardware Gamma Ramp for Brightness, Contrast, and Gamma.
        /// </summary>
        public void ApplyGammaRamp(float brightness, float contrast, float gamma)
        {
            try
            {
                RAMP ramp = CalculateRamp(brightness, contrast, gamma);

                // Try applying to primary display device and registered NVIDIA displays
                bool applied = false;
                if (_isNvApiInitialized)
                {
                    try
                    {
                        var displays = Display.GetDisplays();
                        if (displays != null)
                        {
                            foreach (var display in displays)
                            {
                                string devName = display.Name;
                                if (!string.IsNullOrEmpty(devName))
                                {
                                    IntPtr hdc = CreateDC("DISPLAY", devName, null, IntPtr.Zero);
                                    if (hdc != IntPtr.Zero)
                                    {
                                        SetDeviceGammaRamp(hdc, ref ramp);
                                        DeleteDC(hdc);
                                        applied = true;
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[NvidiaService] Exception while iterating displays for gamma ramp: {ex.Message}");
                    }
                }

                // Fallback for primary screen if not applied via NVAPI enumeration
                if (!applied)
                {
                    IntPtr primaryHdc = CreateDC("DISPLAY", @"\\.\DISPLAY1", null, IntPtr.Zero);
                    if (primaryHdc != IntPtr.Zero)
                    {
                        SetDeviceGammaRamp(primaryHdc, ref ramp);
                        DeleteDC(primaryHdc);
                    }
                    else
                    {
                        IntPtr screenHdc = GetDC(IntPtr.Zero);
                        if (screenHdc != IntPtr.Zero)
                        {
                            SetDeviceGammaRamp(screenHdc, ref ramp);
                            ReleaseDC(IntPtr.Zero, screenHdc);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NvidiaService] Error applying Gamma Ramp: {ex.Message}");
            }
        }

        /// <summary>
        /// Generates a 256-element RGB RAMP based on Brightness (-100 to 100), Contrast (-100 to 100), and Gamma (0.5 to 2.8).
        /// </summary>
        public static RAMP CalculateRamp(float brightness, float contrast, float gamma)
        {
            RAMP ramp = new RAMP
            {
                Red = new ushort[256],
                Green = new ushort[256],
                Blue = new ushort[256]
            };

            float effBrightness = brightness - 50.0f;
            float brightnessOffset = effBrightness / 200.0f;
            float effContrast = contrast - 50.0f;
            float contrastFactor = (100.0f + effContrast) / 100.0f;
            double gammaExponent = 1.0 / Math.Max(0.1, (double)gamma);

            for (int i = 0; i < 256; i++)
            {
                double baseInput = i / 255.0;
                
                // Brightness adjustment
                double val = baseInput + brightnessOffset;
                
                // Contrast adjustment around 0.5 center point
                val = (val - 0.5) * contrastFactor + 0.5;
                val = Math.Clamp(val, 0.0, 1.0);

                // Gamma curve adjustment
                val = Math.Pow(val, gammaExponent);
                val = Math.Clamp(val, 0.0, 1.0);

                ushort ushortVal = (ushort)(val * 65535.0);

                ramp.Red[i] = ushortVal;
                ramp.Green[i] = ushortVal;
                ramp.Blue[i] = ushortVal;
            }

            return ramp;
        }

        /// <summary>
        /// Resets all connected NVIDIA displays and hardware gamma ramps directly to factory defaults.
        /// </summary>
        public void ResetHardwareToDefaults()
        {
            if (!_isNvApiInitialized)
            {
                InitializeNvApi();
            }

            if (_isNvApiInitialized)
            {
                try
                {
                    var displays = Display.GetDisplays();
                    if (displays != null)
                    {
                        foreach (var display in displays)
                        {
                            try
                            {
                                var dvc = display.DigitalVibranceControl;
                                if (dvc != null)
                                {
                                    dvc.CurrentLevel = dvc.DefaultLevel;
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"[NvidiaService] Error resetting DVC on display '{display?.Name}': {ex.Message}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[NvidiaService] Error iterating displays during hardware reset: {ex.Message}");
                }
            }

            // Restore standard linear 1:1 identity ramp for Brightness 50, Contrast 50, Gamma 1.0
            ApplyGammaRamp(50.0f, 50.0f, 1.0f);
        }

        /// <summary>
        /// Resets the primary display to default NVIDIA values.
        /// </summary>
        public void ResetToDefault()
        {
            ResetHardwareToDefaults();
        }

        ~NvidiaService()
        {
            if (_isNvApiInitialized)
            {
                try
                {
                    NVIDIA.Unload();
                }
                catch
                {
                    // Ignore unload exceptions on app shutdown
                }
            }
        }
    }
}
