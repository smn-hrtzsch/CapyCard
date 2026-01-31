# Implementierung von erweiterten Einstellungen und Personalisierung

**Datum:** 31.01.2026
**Ziel:** Dem Nutzer mehr Kontrolle über das Aussehen (Themes, Farben, Dark Mode, Zen Mode) und das Verhalten (Editor Toolbar) der App geben. Die Einstellungen werden persistent in der Datenbank gespeichert.

## 1. Datenbank & Datenmodell

- [ ] **Modell erstellen:** `CapyCard/Models/UserSettings.cs`
    - Eigenschaften:
        - `int Id` (PK, immer 1)
        - `string ThemeColor` (Default: "Teal")
        - `string ThemeMode` (Default: "System" - Werte: System, Light, Dark)
        - `bool IsZenMode` (Default: false)
        - `bool ShowEditorToolbar` (Default: true)
- [ ] **DbContext erweitern:** `CapyCard/Data/FlashcardDbContext.cs`
    - `DbSet<UserSettings> UserSettings { get; set; }` hinzufügen.
- [ ] **Migration erstellen:**
    - Befehl: `dotnet ef migrations add AddUserSettings --project CapyCard`
    - Befehl: `dotnet ef database update --project CapyCard`

## 2. Services (Logik)

- [ ] **IUserSettingsService / UserSettingsService erstellen:**
    - Pfad: `CapyCard/Services/UserSettingsService.cs`
    - Methoden: `LoadSettingsAsync()`, `SaveSettingsAsync(UserSettings settings)`, `GetSettings()`
    - Sollte die aktuellen Einstellungen cachen und als `ObservableObject` oder via Events Änderungen publizieren.
- [ ] **ThemeService erstellen:**
    - Pfad: `CapyCard/Services/ThemeService.cs`
    - Methoden: `ApplyTheme(string color, string mode, bool isZen)`
    - Logik: Lädt die entsprechenden ResourceDictionaries und mergt sie in `App.Current.Resources`.

## 3. Theming & Ressourcen

- [ ] **Farb-Paletten auslagern:**
    - Erstelle `CapyCard/Styles/Themes/Colors/`
    - Erstelle folgende Farbschemata (Primary/PrimaryLight etc.):
        - `Teal.axaml` (Standard)
        - `Blue.axaml`
        - `Green.axaml`
        - `Red.axaml`
        - `Orange.axaml`
        - `Purple.axaml`
        - `Pink.axaml`
        - `Monochrome.axaml` (Schwarz/Weiß/Graustufen für maximale Neutralität und Kontrast)
- [ ] **Theme-Struktur anpassen:**
    - `App.axaml`: Entferne hardcodierte Farben. Referenziere Initialwerte oder lasse sie vom `ThemeService` beim Start setzen.
- [ ] **Zen Mode:**
    - Überlege, ob eine separate `Zen.axaml` nötig ist, oder ob der `ThemeService` einfach die Farben "dämpft" (z.B. graue Akzente statt bunte). *Entscheidung: Wir nutzen eine reduzierte Farbpalette für Zen.*

## 4. UI: Settings Dialog

- [ ] **SettingsViewModel erstellen:**
    - Pfad: `CapyCard/ViewModels/SettingsViewModel.cs`
    - Properties für alle Settings (Color, Mode, Zen, Toolbar).
    - `SaveCommand`: Speichert via `UserSettingsService` und aktualisiert `ThemeService`.
- [ ] **SettingsDialog View erstellen:**
    - Pfad: `CapyCard/Views/SettingsDialog.axaml`
    - UI-Elemente:
        - ComboBox/RadioButtons für Farbwahl (Vorschau-Kreise für alle 8 Farben).
        - RadioButtons für Mode (Light/Dark/Auto).
        - ToggleSwitch für Zen Mode.
        - ToggleSwitch für "Editor Toolbar standardmäßig anzeigen".
- [ ] **DeckListView anpassen:**
    - Button für Einstellungen oben rechts (neben Import/Info) hinzufügen.
    - `DeckListViewModel` um `OpenSettingsCommand` erweitern.

## 5. UI: Editor Toolbar

- [ ] **WysiwygEditor anpassen:**
    - Pfad: `CapyCard/Controls/WysiwygEditor.axaml.cs`
    - `IsToolbarVisible` DependencyProperty hinzufügen.
    - Initialisierung basierend auf Property-Wert.
- [ ] **DeckDetailViewModel anpassen:**
    - Zugriff auf `UserSettings` haben.
    - Beim Starten des Editors (New Card / Edit Card) den `ShowEditorToolbar` Wert aus den Settings lesen.

## 6. App Startup

- [ ] **App.axaml.cs anpassen:**
    - Beim Start (`OnFrameworkInitializationCompleted`):
        - Datenbank initialisieren (passiert schon).
        - `UserSettingsService` laden.
        - `ThemeService` initial aufrufen, um das gespeicherte Theme zu setzen.

## 7. Verifikation

- [ ] App bauen und starten.
- [ ] Prüfen, ob Default-Werte (Teal, System) geladen werden.
- [ ] Einstellungen ändern (z.B. auf Rot, Dark Mode).
- [ ] App neustarten -> Einstellungen müssen erhalten bleiben.
- [ ] Zen Mode testen (Design sollte ruhiger sein).
- [ ] Editor öffnen -> Toolbar Sichtbarkeit prüfen.

## Risiken
- **Runtime Theme Switch:** Avalonia benötigt manchmal ein explizites Re-Applying von Styles oder korrekte `DynamicResource` Bindings.
- **Migration:** Sicherstellen, dass die Migration sauber auf existierenden Datenbanken läuft.
