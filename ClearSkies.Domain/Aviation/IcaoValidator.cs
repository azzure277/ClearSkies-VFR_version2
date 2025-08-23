using System.Text.RegularExpressions;

namespace ClearSkies.Domain.Aviation
{
    public static class IcaoValidator
    {
        // Simple rule: 4 letters (US stations usually K***, but keep generic)
        static readonly Regex _rx = new(@"^[A-Za-z]{4}$", RegexOptions.Compiled);
        public static bool IsValid(string? icao) => !string.IsNullOrWhiteSpace(icao) && _rx.IsMatch(icao!);
    }
}
