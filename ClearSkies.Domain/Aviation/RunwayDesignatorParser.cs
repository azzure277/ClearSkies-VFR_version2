using System.Text.RegularExpressions;

namespace ClearSkies.Domain.Aviation
{
    public static class RunwayDesignatorParser
    {
        // Matches 01..36 with optional L/R/C
        private static readonly Regex Rx = new(
            @"^(?<num>0?[1-9]|[12]\d|3[0-6])(?<side>[LRC])?$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );

        public static bool TryParse(string? input, out int number, out RunwaySide side)
        {
            number = 0;
            side = RunwaySide.None;

            if (string.IsNullOrWhiteSpace(input))
                return false;

            var m = Rx.Match(input.Trim().ToUpperInvariant());
            if (!m.Success) return false;

            number = int.Parse(m.Groups["num"].Value);
            side = m.Groups["side"].Value switch
            {
                "L" => RunwaySide.Left,
                "R" => RunwaySide.Right,
                "C" => RunwaySide.Center,
                _ => RunwaySide.None
            };

            return true;
        }
    }
}
