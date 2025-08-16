// ClearSkies.Api/Options/WeatherOptions.cs
using System.ComponentModel.DataAnnotations;

namespace ClearSkies.Api.Options
{
    /// <summary>
    /// Weather-related config you can tweak without recompiling.
    /// </summary>
    public sealed class WeatherOptions
    {
        /// <summary>
        /// Consider a METAR stale after this many minutes.
        /// Example: 30 means older than 30 minutes => IsStale = true.
        /// </summary>
        [Range(1, 720, ErrorMessage = "FreshMinutes must be between 1 and 720.")]
        public int FreshMinutes { get; set; } = 30; // Example default value
    }
}
