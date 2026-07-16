using System.Collections.Generic;
using Colossal;

namespace GridRoadGenerator.Localization
{
    /// <summary>
    /// Source de localisation générique : reçoit un dictionnaire clé -> texte traduit
    /// et l'expose au LocalizationManager du jeu.
    /// </summary>
    public class LocaleSource : IDictionarySource
    {
        private readonly Dictionary<string, string> _entries;

        public LocaleSource(Dictionary<string, string> entries)
        {
            _entries = entries;
        }

        public IEnumerable<KeyValuePair<string, string>> ReadEntries(
            IList<IDictionaryEntryError> errors,
            Dictionary<string, int> indexCounts)
        {
            return _entries;
        }

        public void Unload() { }
    }
}
