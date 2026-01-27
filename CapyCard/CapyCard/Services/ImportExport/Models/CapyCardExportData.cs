using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CapyCard.Services.ImportExport.Models
{
    /// <summary>
    /// Root-Objekt für das .capycard JSON-Format.
    /// </summary>
    public class CapyCardExportData
    {
        [JsonPropertyName("version")]
        public string Version { get; set; } = "1.0";

        [JsonPropertyName("exportDate")]
        public DateTime ExportDate { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("application")]
        public string Application { get; set; } = "CapyCard";

        [JsonPropertyName("deck")]
        public CapyCardDeckData Deck { get; set; } = new();

        [JsonPropertyName("media")]
        public Dictionary<string, string>? Media { get; set; }
    }

    /// <summary>
    /// Deck-Daten im Export-Format.
    /// </summary>
    public class CapyCardDeckData
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("isDefault")]
        public bool IsDefault { get; set; }

        [JsonPropertyName("cards")]
        public List<CapyCardCardData> Cards { get; set; } = new();

        [JsonPropertyName("subDecks")]
        public List<CapyCardDeckData> SubDecks { get; set; } = new();
    }

    /// <summary>
    /// Karten-Daten im Export-Format.
    /// </summary>
    public class CapyCardCardData
    {
        [JsonPropertyName("front")]
        public string Front { get; set; } = string.Empty;

        [JsonPropertyName("back")]
        public string Back { get; set; } = string.Empty;

        [JsonPropertyName("progress")]
        public CapyCardProgressData? Progress { get; set; }
    }

    /// <summary>
    /// Lernfortschritt-Daten im Export-Format.
    /// </summary>
    public class CapyCardProgressData
    {
        [JsonPropertyName("score")]
        public double Score { get; set; }

        [JsonPropertyName("boxIndex")]
        public int BoxIndex { get; set; }

        [JsonPropertyName("lastReviewed")]
        public DateTime LastReviewed { get; set; }
    }
}
