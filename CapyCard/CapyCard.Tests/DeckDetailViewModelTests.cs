using CapyCard.Data;
using CapyCard.Models;
using CapyCard.ViewModels;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace CapyCard.Tests
{
    public class DeckDetailViewModelTests : IDisposable
    {
        private readonly string _tempDbPath;

        public DeckDetailViewModelTests()
        {
            _tempDbPath = Path.GetTempFileName();
            FlashcardDbContext.OverrideDbPath = _tempDbPath;

            using var context = new FlashcardDbContext();
            context.Database.EnsureCreated();
        }

        public void Dispose()
        {
            FlashcardDbContext.OverrideDbPath = null;
            if (File.Exists(_tempDbPath))
            {
                try { File.Delete(_tempDbPath); } catch { }
            }
        }

        [Fact]
        public async Task OpenSubDeckSelectionCommand_ShouldSetIsSubDeckSelectionVisible_WhenDeckHasSubDecks()
        {
            // Arrange
            using (var context = new FlashcardDbContext())
            {
                var rootDeck = new Deck { Name = "Root" };
                context.Decks.Add(rootDeck);
                await context.SaveChangesAsync();

                var subDeck1 = new Deck { Name = "Sub1", ParentDeckId = rootDeck.Id };
                var subDeck2 = new Deck { Name = "Sub2", ParentDeckId = rootDeck.Id };
                context.Decks.AddRange(subDeck1, subDeck2);
                await context.SaveChangesAsync(); // Save decks first to get IDs

                // Add some cards so "HasCards" logic works properly if involved
                context.Cards.Add(new Card { Front = "F1", Back = "B1", DeckId = subDeck1.Id });
                
                await context.SaveChangesAsync();
            }

            var vm = new DeckDetailViewModel();
            
            // Load the root deck
            Deck rootDeckLoaded;
            using (var context = new FlashcardDbContext())
            {
                rootDeckLoaded = await context.Decks.FirstAsync(d => d.ParentDeckId == null);
            }
            
            await vm.LoadDeck(rootDeckLoaded);

            // Verify initial state
            Assert.False(vm.IsSubDeckSelectionVisible);

            // Act
            if (vm.OpenSubDeckSelectionCommand.CanExecute(null))
            {
                await vm.OpenSubDeckSelectionCommand.ExecuteAsync(null);
            }

            // Assert
            Assert.True(vm.IsSubDeckSelectionVisible, "IsSubDeckSelectionVisible should be true after command execution");
            Assert.Equal(2, vm.SubDeckSelectionList.Count);
        }
    }
}
