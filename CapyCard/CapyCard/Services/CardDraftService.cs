using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CapyCard.Models;

namespace CapyCard.Services
{
    public interface ICardDraftService
    {
        Task<CardDraft?> LoadDraftAsync(int deckId);
        Task SaveDraftAsync(int deckId, string front, string back);
        Task ClearDraftAsync(int deckId);
    }

    public class CardDraftService : ICardDraftService
    {
        private readonly SemaphoreSlim _gate = new(1, 1);
        private readonly Dictionary<int, CardDraft> _drafts = new();
        private bool _isLoaded;
        private string? _draftsPath;

        public async Task<CardDraft?> LoadDraftAsync(int deckId)
        {
            await EnsureLoadedAsync();

            await _gate.WaitAsync();
            try
            {
                return _drafts.TryGetValue(deckId, out var draft) ? draft : null;
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task SaveDraftAsync(int deckId, string front, string back)
        {
            await EnsureLoadedAsync();

            await _gate.WaitAsync();
            try
            {
                _drafts[deckId] = new CardDraft
                {
                    DeckId = deckId,
                    Front = front,
                    Back = back,
                    LastUpdatedUtc = DateTime.UtcNow
                };

                await PersistLockedAsync();
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task ClearDraftAsync(int deckId)
        {
            await EnsureLoadedAsync();

            await _gate.WaitAsync();
            try
            {
                if (_drafts.Remove(deckId))
                {
                    await PersistLockedAsync();
                }
            }
            finally
            {
                _gate.Release();
            }
        }

        private async Task EnsureLoadedAsync()
        {
            if (_isLoaded)
            {
                return;
            }

            await _gate.WaitAsync();
            try
            {
                if (_isLoaded)
                {
                    return;
                }

                if (OperatingSystem.IsBrowser())
                {
                    _isLoaded = true;
                    return;
                }

                var path = DraftsPath;
                if (File.Exists(path))
                {
                    try
                    {
                        var json = await File.ReadAllTextAsync(path);
                        var drafts = JsonSerializer.Deserialize<List<CardDraft>>(json);
                        if (drafts != null)
                        {
                            foreach (var draft in drafts)
                            {
                                _drafts[draft.DeckId] = draft;
                            }
                        }
                    }
                    catch
                    {
                        _drafts.Clear();
                    }
                }

                _isLoaded = true;
            }
            finally
            {
                _gate.Release();
            }
        }

        private async Task PersistLockedAsync()
        {
            if (OperatingSystem.IsBrowser())
            {
                return;
            }

            try
            {
                var drafts = _drafts.Values
                    .OrderBy(d => d.DeckId)
                    .ToList();
                var json = JsonSerializer.Serialize(drafts);
                await File.WriteAllTextAsync(DraftsPath, json);
            }
            catch
            {
                // Ignore persistence errors
            }
        }

        private string DraftsPath => _draftsPath ??= GetDraftsPath();

        private static string GetDraftsPath()
        {
            var folder = Environment.SpecialFolder.LocalApplicationData;
            var basePath = Environment.GetFolderPath(folder);
            var appFolder = Path.Combine(basePath, "CapyCard");
            Directory.CreateDirectory(appFolder);
            return Path.Combine(appFolder, "card-drafts.json");
        }
    }
}
