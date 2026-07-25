using System;
using System.Text.Json.Serialization;

namespace NvidiaColorSwitcher.Models
{
    /// <summary>
    /// Represents a color calibration profile containing display adjustments for Digital Vibrance, Brightness, Contrast, and Gamma.
    /// </summary>
    public class ColorProfile
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        public string Name { get; set; } = "Custom Profile";
        
        /// <summary>
        /// Digital Vibrance percentage range: 0% to 100% (NVIDIA Default: 50%)
        /// </summary>
        public int DigitalVibrance { get; set; } = 50;

        /// <summary>
        /// Brightness percentage offset range: -100% to +100% (Default: 0.0)
        /// </summary>
        public float Brightness { get; set; } = 0.0f;

        /// <summary>
        /// Contrast percentage offset range: -100% to +100% (Default: 0.0)
        /// </summary>
        public float Contrast { get; set; } = 0.0f;

        /// <summary>
        /// Gamma exponent range: 0.5 to 2.8 (Default: 1.0)
        /// </summary>
        public float Gamma { get; set; } = 1.0f;

        /// <summary>
        /// Indicates if this is a built-in system default profile.
        /// </summary>
        public bool IsDefault { get; set; } = false;

        /// <summary>
        /// Transient property indicating if this profile is currently active on the monitor.
        /// </summary>
        [JsonIgnore]
        public bool IsActive { get; set; } = false;

        public ColorProfile Clone()
        {
            return new ColorProfile
            {
                Id = this.Id,
                Name = this.Name,
                DigitalVibrance = this.DigitalVibrance,
                Brightness = this.Brightness,
                Contrast = this.Contrast,
                Gamma = this.Gamma,
                IsDefault = this.IsDefault,
                IsActive = this.IsActive
            };
        }
    }
}
