using System.Collections.Generic;
using System.Linq;
using GridRoadGenerator.Localization;
using Xunit;

namespace GridRoadGenerator.Tests
{
    public class TipTranslationsTests
    {
        // Mêmes langues que Translations.All (Localization/Translations.cs, non compilé ici :
        // il dépend des types du jeu).
        private static readonly string[] ModLocales =
        {
            "en-US", "fr-FR", "de-DE", "es-ES", "it-IT", "pl-PL", "pt-BR", "ru-RU", "ja-JP", "ko-KR",
            "zh-HANS", "zh-HANT", "pt-PT", "uk-UA", "th-TH", "vi-VN", "nl-NL", "ca-ES", "gl-ES", "eu-ES",
            "oc-FR", "cy-GB", "fy-NL", "br-FR", "sco-GB", "sc-IT", "cs-CZ", "da-DK", "nb-NO", "sv-SE",
            "fi-FI", "hu-HU", "hr-HR", "ro-RO", "el-GR", "tr-TR", "id-ID", "fil-PH", "hi-IN", "ar-SA",
            "fa-IR", "ab-GE",
        };

        [Fact]
        public void EveryModLanguageHasEveryTooltip()
        {
            foreach (string locale in ModLocales)
            {
                Assert.True(TipTranslations.All.ContainsKey(locale), $"{locale} : aucune description d'infobulle");
                string[] texts = TipTranslations.All[locale];
                Assert.True(texts.Length == TipTranslations.Keys.Length,
                    $"{locale} : {texts.Length} descriptions au lieu de {TipTranslations.Keys.Length}");
                for (int i = 0; i < texts.Length; i++)
                {
                    Assert.False(string.IsNullOrWhiteSpace(texts[i]), $"{locale} : description vide pour {TipTranslations.Keys[i]}");
                }
            }
            Assert.Equal(ModLocales.Length, TipTranslations.All.Count);
        }

        [Fact]
        public void EveryModLanguageHasTheTreeTexts()
        {
            string[] english = TreeTranslations.All["en-US"];
            foreach (string locale in ModLocales)
            {
                Assert.True(TreeTranslations.All.ContainsKey(locale), $"{locale} : textes Árvore absents");
                string[] texts = TreeTranslations.All[locale];
                Assert.Equal(TreeTranslations.Keys.Length, texts.Length);
                Assert.All(texts, text => Assert.False(string.IsNullOrWhiteSpace(text)));
                if (locale != "en-US" && locale != "sco-GB")
                {
                    // Descriptions traduites (les libellés courts peuvent coïncider, ex. « Tree »).
                    for (int i = 4; i < texts.Length; i++) Assert.NotEqual(english[i], texts[i]);
                }
            }
            Assert.Equal(ModLocales.Length, TreeTranslations.All.Count);
        }

        [Fact]
        public void EveryModLanguageHasTheOrganicTexts()
        {
            string[] english = OrganicTranslations.All["en-US"];
            foreach (string locale in ModLocales)
            {
                Assert.True(OrganicTranslations.All.ContainsKey(locale), $"{locale} : textes Orgânico absents");
                string[] texts = OrganicTranslations.All[locale];
                Assert.Equal(OrganicTranslations.Keys.Length, texts.Length);
                Assert.All(texts, text => Assert.False(string.IsNullOrWhiteSpace(text)));
                if (locale != "en-US" && locale != "sco-GB")
                {
                    for (int i = 5; i < texts.Length; i++) Assert.NotEqual(english[i], texts[i]);
                }
            }
            Assert.Equal(ModLocales.Length, OrganicTranslations.All.Count);
        }

        [Fact]
        public void EveryModLanguageHasTheContourTexts()
        {
            string[] english = ContourTranslations.All["en-US"];
            foreach (string locale in ModLocales)
            {
                Assert.True(ContourTranslations.All.ContainsKey(locale), $"{locale} : textes Relevo absents");
                string[] texts = ContourTranslations.All[locale];
                Assert.Equal(ContourTranslations.Keys.Length, texts.Length);
                Assert.All(texts, text => Assert.False(string.IsNullOrWhiteSpace(text)));
                if (locale != "en-US" && locale != "sco-GB")
                {
                    for (int i = 3; i < texts.Length; i++) Assert.NotEqual(english[i], texts[i]);
                }
            }
            Assert.Equal(ModLocales.Length, ContourTranslations.All.Count);
        }

        [Fact]
        public void EveryModLanguageHasTheToolsTexts()
        {
            string[] english = ToolsTranslations.All["en-US"];
            // Descriptions (Tip.*) et bulles : jamais laissées en anglais.
            var described = new List<int>();
            for (int i = 0; i < ToolsTranslations.Keys.Length; i++)
            {
                if (ToolsTranslations.Keys[i].Contains(".Tip.") || ToolsTranslations.Keys[i].Contains(".Tooltip.")) described.Add(i);
            }
            foreach (string locale in ModLocales)
            {
                Assert.True(ToolsTranslations.All.ContainsKey(locale), $"{locale} : textes des outils absents");
                string[] texts = ToolsTranslations.All[locale];
                Assert.Equal(ToolsTranslations.Keys.Length, texts.Length);
                Assert.All(texts, text => Assert.False(string.IsNullOrWhiteSpace(text)));
                if (locale != "en-US")
                {
                    foreach (int i in described) Assert.NotEqual(english[i], texts[i]);
                }
            }
            Assert.Equal(ModLocales.Length, ToolsTranslations.All.Count);
        }

        [Fact]
        public void EveryModLanguageHasTheRoundaboutTexts()
        {
            string[] english = RoundaboutTranslations.All["en-US"];
            foreach (string locale in ModLocales)
            {
                Assert.True(RoundaboutTranslations.All.ContainsKey(locale), $"{locale} : textes Rotunda absents");
                string[] texts = RoundaboutTranslations.All[locale];
                Assert.Equal(RoundaboutTranslations.Keys.Length, texts.Length);
                Assert.All(texts, text => Assert.False(string.IsNullOrWhiteSpace(text)));
                if (locale != "en-US") Assert.NotEqual(english[1], texts[1]);
            }
            Assert.Equal(ModLocales.Length, RoundaboutTranslations.All.Count);
        }

        [Fact]
        public void EveryModLanguageHasTheFreeAreaTexts()
        {
            string[] english = FreeAreaTranslations.All["en-US"];
            foreach (string locale in ModLocales)
            {
                Assert.True(FreeAreaTranslations.All.ContainsKey(locale), $"{locale} : textes Área livre absents");
                string[] texts = FreeAreaTranslations.All[locale];
                Assert.Equal(FreeAreaTranslations.Keys.Length, texts.Length);
                Assert.All(texts, text => Assert.False(string.IsNullOrWhiteSpace(text)));
                if (locale != "en-US")
                {
                    // Libellés courts (0-2, 9-11, 16-18) : peuvent coïncider avec l'anglais (ex. scots).
                    for (int i = 3; i < texts.Length; i++)
                    {
                        if ((i >= 9 && i <= 11) || (i >= 16 && i <= 18)) continue;
                        Assert.NotEqual(english[i], texts[i]);
                    }
                }
            }
            Assert.Equal(ModLocales.Length, FreeAreaTranslations.All.Count);
        }

        [Fact]
        public void AddToWritesPrefixedKeys()
        {
            var entries = new Dictionary<string, string>();
            TipTranslations.AddTo(entries, "pt-PT");
            Assert.Equal(TipTranslations.Keys.Length, entries.Count);
            Assert.All(entries.Keys, key => Assert.StartsWith("GridRoadGenerator.UI.Tip.", key));
            Assert.Contains("GridRoadGenerator.UI.Tip.Generate", entries.Keys);
        }

        [Fact]
        public void TranslationsAreNotLeftInEnglish()
        {
            // Une ligne recopiée en anglais par erreur (décalage d'une ligne compris) se voit ici.
            string[] english = TipTranslations.All["en-US"];
            foreach (var pair in TipTranslations.All.Where(p => p.Key != "en-US" && p.Key != "sco-GB"))
            {
                for (int i = 0; i < english.Length; i++)
                {
                    Assert.NotEqual(english[i], pair.Value[i]);
                }
            }
        }
    }
}
