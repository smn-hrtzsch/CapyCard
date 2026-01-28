using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CapyCard.Services.ImportExport.Models
{
    /// <summary>
    /// Einfaches DTO für den JSON-Import (z.B. von LLMs generiert).
    /// </summary>
    public class JsonDeck
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("cards")]
        public List<JsonCard>? Cards { get; set; }

        [JsonPropertyName("subDecks")]
        public List<JsonDeck>? SubDecks { get; set; }
    }

    /// <summary>
    /// Einzelne Karte im JSON-Import.
    /// </summary>
    public class JsonCard
    {
        [JsonPropertyName("front")]
        public string? Front { get; set; }

        [JsonPropertyName("back")]
        public string? Back { get; set; }
    }
}
