# AGENTS.md - CapyCard Codebase Guide

This document provides essential information for AI coding agents working on the CapyCard codebase.

## Project Overview

CapyCard is a cross-platform flashcard learning application built with:
- **.NET 9.0** (SDK 9.0.307, see `global.json`)
- **Avalonia UI 11.3.9** (cross-platform UI framework)
- **CommunityToolkit.Mvvm 8.4.0** (MVVM pattern with source generators)
- **Entity Framework Core 9.0** with SQLite
- **xUnit** for testing

**Platforms:** Desktop (Windows/macOS/Linux), Android, iOS, Browser (WebAssembly)

## Project Structure

```
CapyCard/
├── CapyCard/                    # Solution folder
│   ├── CapyCard/                # Core shared library (main code)
│   │   ├── Models/              # Domain entities (Card, Deck, LearningSession)
│   │   ├── ViewModels/          # MVVM ViewModels
│   │   ├── Views/               # Avalonia AXAML views
│   │   ├── Services/            # Business logic services
│   │   ├── Data/                # EF Core DbContext
│   │   ├── Controls/            # Custom UI controls
│   │   ├── Converters/          # Value converters
│   │   ├── Behaviors/           # Attached behaviors
│   │   └── Migrations/          # EF Core migrations
│   ├── CapyCard.Desktop/        # Desktop platform head
│   ├── CapyCard.Android/        # Android platform head
│   ├── CapyCard.iOS/            # iOS platform head
│   ├── CapyCard.Browser/        # WebAssembly platform head
│   ├── CapyCard.Tests/          # Unit tests (xUnit)
│   ├── Directory.Build.props    # Centralized version (2.2.0)
│   └── Directory.Packages.props # Central package management
└── plans/                       # Planning documents
```

## Build Commands

All commands should be run from `CapyCard/CapyCard/` directory (where the .sln file is).

```bash
# Restore dependencies
dotnet restore

# Build entire solution
dotnet build

# Build specific project
dotnet build CapyCard/CapyCard.csproj

# Build for release
dotnet build -c Release

# Run Desktop app
dotnet run --project CapyCard.Desktop
```

## Test Commands

```bash
# Run all tests
dotnet test

# Run all tests with verbose output
dotnet test --logger "console;verbosity=detailed"

# Run a single test by full name
dotnet test --filter "FullyQualifiedName=CapyCard.Tests.SmartQueueServiceTests.CalculateNewScore_Rating1_DecreasesBoxByTwo"

# Run tests matching a pattern
dotnet test --filter "DisplayName~CalculateNewScore"

# Run all tests in a class
dotnet test --filter "ClassName=CapyCard.Tests.SmartQueueServiceTests"

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

## Code Style Guidelines

### Naming Conventions

| Element | Convention | Example |
|---------|------------|---------|
| Private fields | `_camelCase` with underscore prefix | `private string _newDeckName;` |
| Properties | `PascalCase` | `public string DeckName { get; set; }` |
| Methods | `PascalCase` | `public void LoadDecks()` |
| Local variables | `camelCase` | `var totalCards = 0;` |
| Constants | `PascalCase` | `private const int Iterations = 5000;` |
| Interfaces | `IPascalCase` | `public interface IClipboardService` |
| ViewModels | `[Name]ViewModel` | `DeckListViewModel` |
| Views | `[Name]View` | `DeckListView` |

### File Organization

- One class per file (matching filename to class name)
- ViewModels in `ViewModels/` folder
- Views in `Views/` folder with `.axaml` + `.axaml.cs` pairs
- Models in `Models/` folder as POCOs
- Services in `Services/` folder

### Using Statements / Imports

```csharp
// System namespaces first
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

// Third-party packages
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;

// Project namespaces
using CapyCard.Data;
using CapyCard.Models;
```

### MVVM Pattern

Use CommunityToolkit.Mvvm source generators:

```csharp
public partial class MyViewModel : ObservableObject
{
    // Auto-generates "MyProperty" property with change notification
    [ObservableProperty]
    private string _myProperty = string.Empty;

    // Auto-generates "DoSomethingCommand" IRelayCommand
    [RelayCommand]
    private async Task DoSomething()
    {
        // Implementation
    }
}
```

### Database Access Pattern

Use short-lived DbContext instances (unit-of-work per operation):

```csharp
// CORRECT: Short-lived context
using (var context = new FlashcardDbContext())
{
    var decks = await context.Decks.ToListAsync();
    // ... operations
    await context.SaveChangesAsync();
}

// INCORRECT: Long-lived context (causes memory leaks)
private readonly FlashcardDbContext _dbContext; // Don't do this
```

### Nullable Reference Types

The project has `<Nullable>enable</Nullable>`. Always handle nullability:

```csharp
// Use nullable types for optional values
public Deck? SelectedDeck { get; set; }

// Use null-forgiving operator only when certain
public virtual Deck Deck { get; set; } = null!;

// Prefer null checks
if (deck != null)
{
    // Safe to use deck
}
```

### Error Handling

Current pattern (to be improved - see plans/):

```csharp
try
{
    // Operation
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
    // TODO: Add proper user notification
}
```

### Async/Await

- Use `async Task` for async methods without return value
- Use `async Task<T>` for async methods with return value
- Use `async void` ONLY for event handlers
- Always use `ConfigureAwait(false)` in library code when appropriate

```csharp
[RelayCommand]
private async Task LoadDataAsync()
{
    using var context = new FlashcardDbContext();
    var data = await context.Decks.ToListAsync();
}
```

### AXAML Views

- Use compiled bindings: `{Binding PropertyName}` or `{CompiledBinding PropertyName}`
- Views are resolved via `ViewLocator.cs` (convention: `ViewModel` -> `View`)
- Use Material Icons: `<material:MaterialIcon Kind="Add" />`

## Important Files

| File | Purpose |
|------|---------|
| `App.axaml` | Application resources, themes (Light/Dark) |
| `ViewLocator.cs` | Convention-based View resolution |
| `FlashcardDbContext.cs` | EF Core database context |
| `Directory.Packages.props` | All NuGet package versions |
| `Directory.Build.props` | App version (2.2.0) |

## Known Issues / Technical Debt

1. **LearnViewModel** has a long-lived DbContext that should be refactored
2. **MarkdownService.cs** is empty - implement or remove
3. No dependency injection - services are instantiated with `new`
4. UI strings are hardcoded in German (no localization)
5. Test coverage is limited to `SmartQueueService`

See `plans/2026-01-21-architecture-improvements.md` for detailed improvement plan.

## EF Core Migrations

```bash
# Add new migration (from CapyCard/CapyCard/ folder)
dotnet ef migrations add MigrationName --project CapyCard

# Update database
dotnet ef database update --project CapyCard

# Remove last migration
dotnet ef migrations remove --project CapyCard
```

## Platform-Specific Code

Platform-specific implementations are in their respective projects:
- `CapyCard.iOS/` - iOS services (e.g., `ClipboardServiceiOS.cs`)
- `CapyCard.Android/` - Android services
- Core interfaces in `CapyCard/Services/` (e.g., `IClipboardService.cs`)
