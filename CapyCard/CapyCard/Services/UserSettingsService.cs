using System.Threading.Tasks;
using CapyCard.Data;
using CapyCard.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.EntityFrameworkCore;

namespace CapyCard.Services
{
    public interface IUserSettingsService
    {
        Task<UserSettings> LoadSettingsAsync();
        Task SaveSettingsAsync(UserSettings settings);
        UserSettings CurrentSettings { get; }
    }

    public partial class UserSettingsService : ObservableObject, IUserSettingsService
    {
        [ObservableProperty]
        private UserSettings _currentSettings;

        public UserSettingsService()
        {
            // Default initialization
            _currentSettings = new UserSettings();
        }

        public async Task<UserSettings> LoadSettingsAsync()
        {
            using (var context = new FlashcardDbContext())
            {
                var settings = await context.UserSettings.FirstOrDefaultAsync(s => s.Id == 1);
                
                if (settings == null)
                {
                    settings = new UserSettings();
                    context.UserSettings.Add(settings);
                    await context.SaveChangesAsync();
                }

                CurrentSettings = settings;
                return settings;
            }
        }

        public async Task SaveSettingsAsync(UserSettings settings)
        {
            using (var context = new FlashcardDbContext())
            {
                // Ensure Id is 1
                settings.Id = 1;

                var existing = await context.UserSettings.FirstOrDefaultAsync(s => s.Id == 1);
                if (existing != null)
                {
                    existing.ThemeColor = settings.ThemeColor;
                    existing.ThemeMode = settings.ThemeMode;
                    existing.IsZenMode = settings.IsZenMode;
                    existing.ShowEditorToolbar = settings.ShowEditorToolbar;
                    context.UserSettings.Update(existing);
                }
                else
                {
                    context.UserSettings.Add(settings);
                }

                await context.SaveChangesAsync();
                CurrentSettings = settings;
            }
        }
    }
}
