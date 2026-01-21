# Architektur-Verbesserungsplan CapyCard

## Zusammenfassung

Dieser Plan adressiert technische Schulden und Architektur-Verbesserungen für das CapyCard-Projekt.

---

## Phase 1: Kritische Fixes (Sofort)

### 1.1 LearnViewModel DbContext Memory Leak beheben

- **Datei:** `CapyCard/ViewModels/LearnViewModel.cs`
- **Problem:** `_dbContext` wird im Konstruktor erstellt aber nie disposed
- **Lösung:** Short-lived DbContext Pattern wie in `DeckDetailViewModel`:

```csharp
// Statt:
private readonly FlashcardDbContext _dbContext;

// Verwenden:
using (var context = new FlashcardDbContext())
{
    // ... Operationen
}
```

- [ ] Implementieren
- [ ] Testen

### 1.2 Leere/Unbenutzte Dateien entfernen

- [ ] `CapyCard/Services/MarkdownService.cs` - Implementieren oder löschen
- [ ] `CapyCard.Tests/UnitTest1.cs` - Sinnvoll umbenennen oder löschen

### 1.3 Fehlerbehandlung verbessern

- [ ] Zentrale Error-Handling-Klasse erstellen
- [ ] User-Feedback bei Fehlern (nicht nur Console.WriteLine)
- **Beispiel-Implementation:**

```csharp
public interface IErrorHandler
{
    void HandleError(Exception ex, string userMessage);
    void LogWarning(string message);
}
```

---

## Phase 2: Testbarkeit & Architektur (Kurz-/Mittelfristig)

### 2.1 Service-Interfaces extrahieren

- [ ] `ISmartQueueService` Interface erstellen
- [ ] `IPdfGenerationService` Interface erstellen
- [ ] `IFlashcardRepository` für Datenzugriff

**Beispiel:**

```csharp
public interface ISmartQueueService
{
    void CalculateNewScore(CardSmartScore smartScore, int rating);
    Card? GetNextCard(List<Card> cards, List<CardSmartScore> scores);
}
```

### 2.2 Dependency Injection einführen

- [ ] `Microsoft.Extensions.DependencyInjection` hinzufügen
- [ ] Service-Registrierung in `App.axaml.cs`
- [ ] ViewModels per DI auflösen

**Beispiel Setup:**

```csharp
public static IServiceProvider Services { get; private set; }

private static IServiceProvider ConfigureServices()
{
    var services = new ServiceCollection();

    services.AddDbContext<FlashcardDbContext>();
    services.AddSingleton<ISmartQueueService, SmartQueueService>();
    services.AddSingleton<IErrorHandler, ErrorHandler>();
    services.AddTransient<MainViewModel>();

    return services.BuildServiceProvider();
}
```

### 2.3 ViewModelBase mit Funktionalität erweitern

- **Datei:** `CapyCard/ViewModels/ViewModelBase.cs`
- [ ] `IsBusy` Property hinzufügen
- [ ] Navigation-Helpers
- [ ] Error-Handling-Integration

```csharp
public abstract class ViewModelBase : ObservableObject
{
    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    protected IErrorHandler ErrorHandler { get; }

    protected ViewModelBase(IErrorHandler errorHandler)
    {
        ErrorHandler = errorHandler;
    }
}
```

---

## Phase 3: Code-Qualität (Mittelfristig)

### 3.1 Große ViewModels aufteilen

#### DeckDetailViewModel (528 Zeilen)

- [ ] Sub-Deck Logik in `SubDeckService` extrahieren
- [ ] Card-CRUD in `CardService` auslagern
- [ ] Import/Export Logik separieren

#### LearnViewModel (579 Zeilen)

- [ ] Session-Management in `LearningSessionService`
- [ ] Progress-Tracking separieren

### 3.2 Logging-Framework einführen

- [ ] `Microsoft.Extensions.Logging` hinzufügen
- [ ] Console.WriteLine durch ILogger ersetzen
- [ ] Log-Levels konfigurieren (Debug, Info, Warning, Error)

### 3.3 .editorconfig hinzufügen

- [ ] Datei im Root erstellen
- [ ] C# Naming Conventions
- [ ] Indentation Rules
- [ ] File Header Template

**Beispiel `.editorconfig`:**

```ini
root = true

[*.cs]
indent_style = space
indent_size = 4
charset = utf-8
end_of_line = lf
insert_final_newline = true

# Naming
dotnet_naming_rule.private_fields_should_be_camel_case.severity = suggestion
dotnet_naming_rule.private_fields_should_be_camel_case.symbols = private_fields
dotnet_naming_rule.private_fields_should_be_camel_case.style = camel_case_underscore

dotnet_naming_symbols.private_fields.applicable_kinds = field
dotnet_naming_symbols.private_fields.applicable_accessibilities = private

dotnet_naming_style.camel_case_underscore.required_prefix = _
dotnet_naming_style.camel_case_underscore.capitalization = camel_case
```

---

## Phase 4: Langfristige Verbesserungen

### 4.1 Test-Coverage erweitern

Aktuell: Nur `SmartQueueService` hat Tests

**Priorität:**

- [ ] Card CRUD Operationen
- [ ] Learning Progress Persistenz
- [ ] Navigation Flow
- [ ] ViewModel Unit Tests (mit gemockten Services)

### 4.2 Lokalisierung vorbereiten

- [ ] Resource-Dateien (`.resx`) für Strings
- [ ] Sprach-Switching-Infrastruktur
- [ ] Mindestens DE + EN Support

### 4.3 Repository Pattern (Optional)

Falls Datenzugriff komplexer wird:

```csharp
public interface IDeckRepository
{
    Task<Deck?> GetByIdAsync(int id);
    Task<List<Deck>> GetAllAsync();
    Task<Deck> CreateAsync(Deck deck);
    Task UpdateAsync(Deck deck);
    Task DeleteAsync(int id);
}
```

---

## Priorisierte Checkliste

### Sofort (Diese Woche)

- [ ] LearnViewModel DbContext Leak fixen
- [ ] Leere Dateien aufräumen
- [ ] .editorconfig hinzufügen

### Kurzfristig (2-4 Wochen)

- [ ] Service Interfaces erstellen
- [ ] Basis-Error-Handling implementieren
- [ ] ViewModelBase erweitern

### Mittelfristig (1-2 Monate)

- [ ] Dependency Injection vollständig einführen
- [ ] Große ViewModels refactoren
- [ ] Logging-Framework

### Langfristig (Bei Bedarf)

- [ ] Test-Coverage auf 60%+ bringen
- [ ] Lokalisierung implementieren
- [ ] Repository Pattern bei Bedarf

---

## Datei-Übersicht (Neue/Zu Ändernde Dateien)

| Aktion                 | Pfad                                         |
| ---------------------- | -------------------------------------------- |
| **Neu**                | `CapyCard/Services/ISmartQueueService.cs`    |
| **Neu**                | `CapyCard/Services/IPdfGenerationService.cs` |
| **Neu**                | `CapyCard/Services/IErrorHandler.cs`         |
| **Neu**                | `CapyCard/Services/ErrorHandler.cs`          |
| **Neu**                | `.editorconfig`                              |
| **Ändern**             | `CapyCard/ViewModels/LearnViewModel.cs`      |
| **Ändern**             | `CapyCard/ViewModels/ViewModelBase.cs`       |
| **Ändern**             | `CapyCard/App.axaml.cs` (DI Setup)           |
| **Löschen**            | `CapyCard/Services/MarkdownService.cs`       |
| **Löschen/Umbenennen** | `CapyCard.Tests/UnitTest1.cs`                |

---

## Hinweise

- Der existierende UI-Overhaul-Plan (`2026-01-21-ui-overhaul.md`) sollte parallel weiterverfolgt werden
- Bei DI-Einführung: Schrittweise migrieren, nicht alles auf einmal
- Tests vor Refactoring schreiben, wo möglich
