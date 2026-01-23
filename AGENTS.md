# AGENTS.md - CapyCard Codebase Guide

This document provides essential information for AI coding agents working on the CapyCard codebase.

## Project Overview

CapyCard is a cross-platform flashcard learning application built with:
- **.NET 9.0** (SDK 9.0.307)
- **Avalonia UI 11.3.9** (Cross-platform UI)
- **CommunityToolkit.Mvvm 8.4.0** (MVVM + Source Generators)
- **Entity Framework Core 9.0** (SQLite)
- **xUnit** (Testing)

**Platforms:** Desktop (Windows/macOS/Linux), Android, iOS, Browser (WASM).

## Development Guidelines

### Build Configuration & Environment
- **JAVA_HOME / JDK:** The project requires **OpenJDK 21** for Android builds.
  - This is automatically configured in `Directory.Build.props` for the default path: `/opt/homebrew/opt/openjdk@21/libexec/openjdk.jdk/Contents/Home`.
  - If your JDK is elsewhere, set the `JavaSdkDirectory` MSBuild property or the `JAVA_HOME` environment variable.

- **Platform-Specific Builds:** Always run a platform-specific build after modifying platform code:

  ```bash
  dotnet build CapyCard/CapyCard.iOS/CapyCard.iOS.csproj
  ```

- **Verification:** Always try to build or compile the project after making changes. If errors occur, provide solutions immediately.

### Pull Requests & Releases
- **Pull Requests:** 
  - Target the `main` branch unless specified otherwise.
  - After creation, **merge immediately** and **delete the source branch** (local & remote).
- **Releases:**
  - Use the GitHub release workflow. target `main`.
  - Ask if the release is **Major, Minor, or Patch** if not specified.
  - Provide a concise changelog.

## Build & Test Commands

Run from `CapyCard/CapyCard/` (solution folder).

### Build

```bash
dotnet restore
dotnet build
dotnet build -c Release
# Run Desktop app
dotnet run --project CapyCard.Desktop
```

### Test

```bash
# Run all tests
dotnet test

# Run a single test (by full name)
dotnet test --filter "FullyQualifiedName=CapyCard.Tests.SmartQueueServiceTests.Method_Scenario_Result"

# Run tests by pattern
dotnet test --filter "DisplayName~CalculateNewScore"

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Linting & Migrations

```bash
# Format code
dotnet format

# EF Core Migrations
dotnet ef migrations add MigrationName --project CapyCard
dotnet ef database update --project CapyCard
```

## Code Style Guidelines

### Naming & Organization
- **Fields:** `_camelCase` (`private string _name;`)
- **Properties/Methods:** `PascalCase` (`public void Load()`)
- **ViewModels:** `[Name]ViewModel` in `ViewModels/`
- **Views:** `[Name]View` in `Views/` (matched to VM)
- **One class per file.**

### MVVM Pattern (CommunityToolkit.Mvvm)
Use source generators to reduce boilerplate:

```csharp
public partial class DeckViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = string.Empty;

    [RelayCommand]
    private async Task Save() { /* ... */ }
}
```

### Database (EF Core)
Use **short-lived** contexts (Unit-of-Work) to avoid memory leaks:

```csharp
using (var context = new FlashcardDbContext())
{
    var decks = await context.Decks.ToListAsync();
    // ...
    await context.SaveChangesAsync();
}
```

### Error Handling & Async
- **Async:** Use `async Task` (not `async void` except for events).
- **Nullability:** Handle `null` explicitly (`<Nullable>enable</Nullable>`).
- **Exceptions:** Catch specific exceptions where possible.

## Design & UI Guidelines

CapyCard follows a specific "Shopping List" aesthetic (Teal/Purple, Rounded, Floating).

**👉 See [guidelines/DESIGN.md](guidelines/DESIGN.md) for detailed visual specs, color codes, and button classes.**

### Key UI Rules
- **Primary Color:** Teal (`#018786` Light / `#03DAC5` Dark).
- **Secondary:** Deep Purple.
- **Shapes:** Pill-shaped buttons (`CornerRadius="25"`), Large rounded cards (`CornerRadius="28"`).
- **Icons:** Use `Material.Icons.Avalonia`.
- **UX:** Floating inputs with transparent backgrounds. Handle `Escape` to release focus.

## Important Files

| File | Purpose |
| :--- | :--- |
| `CapyCard/App.axaml` | Global resources & themes |
| `CapyCard/ViewLocator.cs` | ViewModel -> View resolution |
| `CapyCard/Data/FlashcardDbContext.cs` | Database Context |
| `Directory.Packages.props` | NuGet versions |
