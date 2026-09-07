using AeroTech.Ordering.ReferenceData.AirInfo.Wire;

namespace AeroTech.Ordering.ReferenceData.Syncing
{
    public static class DisplayNameSelector
    {
        public static string Pick(IEnumerable<DisplayNameDto>? displayNames, string primaryLanguage)
        {
            if (displayNames is null)
                return string.Empty;

            var candidates = displayNames.Where(name => !string.IsNullOrWhiteSpace(name.Value)).ToList();

            var primary = candidates.FirstOrDefault(name =>
                string.Equals(name.Language, primaryLanguage, StringComparison.OrdinalIgnoreCase));

            return (primary ?? candidates.FirstOrDefault())?.Value ?? string.Empty;
        }
    }
}
