using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using CapyCard.Services.ImportExport.Formats;
using CapyCard.Services.ImportExport.Models;

namespace CapyCard.Tests
{
    public class JsonFormatHandlerTests
    {
        private readonly JsonFormatHandler _handler;

        public JsonFormatHandlerTests()
        {
            _handler = new JsonFormatHandler();
        }

        [Fact]
        public async Task AnalyzeAsync_IgnoresInvalidCardsAndEmptySubDecks()
        {
            await using var stream = CreateJsonStream(SampleJson);

            var preview = await _handler.AnalyzeAsync(stream, "sample.json");

            Assert.True(preview.Success);
            Assert.Equal(4, preview.CardCount);
            Assert.Equal(2, preview.SubDeckCount);
        }

        [Fact]
        public void NormalizeDeckForSingleLevel_FlattensNestedSubDeckCards_AndAddsWarning()
        {
            var deck = JsonSerializer.Deserialize<JsonDeck>(SampleJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            Assert.NotNull(deck);

            var method = typeof(JsonFormatHandler).GetMethod("NormalizeDeckForSingleLevel", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method);

            var warnings = new List<string>();
            method!.Invoke(_handler, new object[] { deck!, 0, warnings });

            Assert.NotNull(deck!.SubDecks);
            Assert.Equal(2, deck.SubDecks!.Count);
            Assert.DoesNotContain(deck.SubDecks, sd => string.Equals(sd.Name, "Leer", StringComparison.Ordinal));

            var kapitel2 = deck.SubDecks.First(sd => string.Equals(sd.Name, "Kapitel 2", StringComparison.Ordinal));
            Assert.NotNull(kapitel2.Cards);
            Assert.Equal(2, kapitel2.Cards!.Count);
            Assert.Empty(kapitel2.SubDecks ?? new List<JsonDeck>());

            Assert.Contains(warnings, warning => warning.Contains("ein SubDeck-Level", StringComparison.OrdinalIgnoreCase));
        }

        private static MemoryStream CreateJsonStream(string json)
        {
            return new MemoryStream(Encoding.UTF8.GetBytes(json));
        }

        private const string SampleJson = """
        {
          "name": "Testdeck",
          "cards": [
            { "front": "Root Card", "back": "Back" },
            { "name": "Malformed Card", "cards": [], "subDecks": [] },
            { "front": " ", "back": "Should be ignored" }
          ],
          "subDecks": [
            {
              "name": "Kapitel 1",
              "cards": [
                { "front": "K1 Card", "back": "Back" }
              ],
              "subDecks": []
            },
            {
              "name": "Leer",
              "cards": [],
              "subDecks": []
            },
            {
              "name": "Kapitel 2",
              "cards": [
                { "front": "K2 Card", "back": "Back" }
              ],
              "subDecks": [
                {
                  "name": "Nested",
                  "cards": [
                    { "front": "Nested Card", "back": "Back" }
                  ],
                  "subDecks": []
                }
              ]
            }
          ]
        }
        """;
    }
}
