# Plan: Import/Export von Kartenstapeln

**Datum:** 2026-01-27  
**Status:** Entwurf  
**Geschätzte Dauer:** ~2,5-3,5 Wochen

---

## Ziel

Implementierung einer vollständigen Import/Export-Funktionalität für Kartenstapel in CapyCard mit:

1. **Eigenes Format** (CapyCard JSON/ZIP)
2. **Anki-Kompatibilität** (.apkg Import & Export)
3. **CSV-Import/Export** für einfache Tabellenkalkulationen
4. **Flexibler Import**: Als neues Fach, in bestehendes Fach, oder als Thema
5. **Granularer Export**: Ganzes Fach, mehrere Themen auswählen, oder einzelne Karten
6. **Optionaler Lernfortschritt**: Export/Import von SmartScore-Daten
7. **Info-Button** mit Erklärung der unterstützten Formate
8. **Multiplattform-Support**: Desktop, iOS, Android, Browser (WASM)

---

## Unterstützte Formate

| Format | Import | Export | Beschreibung |
|--------|--------|--------|--------------|
| `.capycard` (ZIP+JSON) | ✅ | ✅ | Eigenes Format mit Bildern und Fortschritt |
| `.apkg` (Anki) | ✅ | ✅ | Anki 2.1+ Deck-Pakete |
| `.csv` | ✅ | ✅ | Einfaches Tabellenformat (Front;Back) |

---

## Architektur

### Neue Dateien

```
CapyCard/
├── Services/
│   └── ImportExport/
│       ├── IImportExportService.cs      # Interface für Import/Export
│       ├── ImportExportService.cs       # Hauptservice (Koordinator)
│       ├── Formats/
│       │   ├── IFormatHandler.cs        # Interface für Format-Handler
│       │   ├── CapyCardFormatHandler.cs # .capycard (JSON+ZIP)
│       │   ├── AnkiFormatHandler.cs     # .apkg (SQLite+ZIP)
│       │   └── CsvFormatHandler.cs      # .csv (Tab/Semicolon)
│       └── Models/
│           ├── ImportOptions.cs         # Import-Konfiguration
│           ├── ExportOptions.cs         # Export-Konfiguration
│           └── ImportResult.cs          # Ergebnis mit Statistiken
├── ViewModels/
│   ├── ImportViewModel.cs               # Import-Dialog ViewModel
│   └── ExportViewModel.cs               # Export-Dialog ViewModel
└── Views/
    ├── ImportDialog.axaml               # Import-Dialog UI
    ├── ExportDialog.axaml               # Export-Dialog UI
    └── FormatInfoDialog.axaml           # Info-Dialog für Formate
```

### Betroffene bestehende Dateien

| Datei | Änderung |
|-------|----------|
| `DeckListView.axaml` | Import-Button hinzufügen |
| `DeckListViewModel.cs` | Import-Command hinzufügen |
| `DeckDetailView.axaml` | Export-Button im Header |
| `DeckDetailViewModel.cs` | Export-Command hinzufügen |
| `CardListView.axaml` | Export-Button für ausgewählte Karten |
| `CardListViewModel.cs` | Export-Command hinzufügen |
| `MainViewModel.cs` | Dialog-Navigation |

---

## Datenmodelle

### ImportOptions.cs
```csharp
public class ImportOptions
{
    public ImportTarget Target { get; set; } // NewDeck, ExistingDeck, ExistingSubDeck
    public int? TargetDeckId { get; set; }   // Für ExistingDeck/ExistingSubDeck
    public string? NewDeckName { get; set; } // Für NewDeck
    public bool IncludeProgress { get; set; } // Lernfortschritt importieren
    public DuplicateHandling OnDuplicate { get; set; } // Skip, Replace, KeepBoth
}

public enum ImportTarget { NewDeck, ExistingDeck, ExistingSubDeck }
public enum DuplicateHandling { Skip, Replace, KeepBoth }
```

### ExportOptions.cs
```csharp
public class ExportOptions
{
    public ExportFormat Format { get; set; }          // CapyCard, Anki, CSV
    public ExportScope Scope { get; set; }            // FullDeck, SelectedSubDecks, SelectedCards
    public bool IncludeProgress { get; set; }         // Lernfortschritt exportieren
    public List<int>? SelectedSubDeckIds { get; set; } // Für SelectedSubDecks (Mehrfachauswahl!)
    public List<int>? SelectedCardIds { get; set; }   // Für SelectedCards
}

public enum ExportFormat { CapyCard, Anki, CSV }
public enum ExportScope { FullDeck, SelectedSubDecks, SelectedCards }
```

### CapyCard JSON-Format (.capycard)

```json
{
  "version": "1.0",
  "exportDate": "2026-01-27T12:00:00Z",
  "application": "CapyCard",
  "deck": {
    "name": "Biologie",
    "isDefault": false,
    "cards": [
      {
        "front": "Was ist Photosynthese?",
        "back": "Der Prozess, bei dem Pflanzen...",
        "progress": {
          "score": 0.75,
          "boxIndex": 3,
          "lastReviewed": "2026-01-25T10:00:00Z"
        }
      }
    ],
    "subDecks": [
      {
        "name": "Zellbiologie",
        "isDefault": false,
        "cards": [...]
      }
    ]
  },
  "media": {
    "img_001.png": "base64...",
    "img_002.jpg": "base64..."
  }
}
```

**Hinweis:** Bilder sind bereits als Base64 Data-URIs im `Front`/`Back` der Karten eingebettet. Das `media`-Feld ist optional für extrahierte Bilder (zur Reduzierung der Dateigröße bei großen Decks).

---

## Anki-Format (.apkg)

### Struktur
```
deck.apkg (ZIP-Archiv)
├── collection.anki2  (SQLite-Datenbank)
└── media             (JSON: {"0": "image.jpg", "1": "audio.mp3"})
    ├── 0             (Binärdatei: image.jpg)
    └── 1             (Binärdatei: audio.mp3)
```

### Mapping: Anki → CapyCard

| Anki | CapyCard | Bemerkung |
|------|----------|-----------|
| `notes.flds` (Field 0) | `Card.Front` | Mit HTML → Markdown-Konvertierung |
| `notes.flds` (Field 1) | `Card.Back` | Mit HTML → Markdown-Konvertierung |
| `decks` (JSON in col) | `Deck` | Hierarchie aus `::` parsen |
| `cards.ivl`, `cards.factor` | `CardSmartScore` | Optional, nur bei IncludeProgress |
| Media-Referenzen `<img src="...">` | Base64 Data-URI | Bilder einbetten |

### Mapping: CapyCard → Anki

| CapyCard | Anki | Bemerkung |
|----------|------|-----------|
| `Card.Front` | `notes.flds[0]` | Markdown → HTML |
| `Card.Back` | `notes.flds[1]` | Markdown → HTML |
| `Deck.Name + SubDeck.Name` | `decks` mit `::` | z.B. "Biologie::Zellbiologie" |
| Base64 Data-URI | Media-Dateien | Bilder extrahieren |

### Anki-Besonderheiten
- **Note Types (Models):** CapyCard verwendet immer "Basic" (2 Felder: Front, Back)
- **Cloze Deletions:** Werden als Text behandelt (kein spezielles Parsing)
- **Audio/Video:** Werden als Links behandelt (kein Abspielen in CapyCard)
- **Scheduling:** `cards.type`, `cards.queue`, `cards.due` → BoxIndex approximieren

---

## CSV-Format

### Export-Format
```csv
Front;Back;Deck;SubDeck;BoxIndex;LastReviewed
"Was ist Photosynthese?";"Der Prozess...";"Biologie";"Allgemein";3;2026-01-25
```

- **Trennzeichen:** Semikolon (`;`) — kompatibel mit deutschen Excel-Versionen
- **Encoding:** UTF-8 mit BOM
- **Bilder:** Als `[Bild: img_001.png]` Placeholder (ohne Base64)

### Import-Format
- Erkennt automatisch Trennzeichen (`;`, `,`, `\t`)
- Mindestens 2 Spalten (Front, Back)
- Optionale Header-Zeile (wird erkannt)

---

## Design-Richtlinien

→ Vollständige Spezifikation: [guidelines/DESIGN.md](../guidelines/DESIGN.md)

### Buttons für Import/Export

| Element | Klasse | Icon | Tooltip |
|---------|--------|------|---------|
| Import-Button | `Classes="primary"` | `MaterialIcon Kind="Import"` | "Kartenstapel importieren" |
| Export-Button (Header) | `Classes="primary"` | `MaterialIcon Kind="Export"` | "Fach exportieren" |
| Export-Button (Thema) | `Classes="icon"` | `MaterialIcon Kind="Export"` | "Thema exportieren" |
| Info-Button | `Classes="icon"` | `MaterialIcon Kind="InformationOutline"` | "Über Formate" |

### Dialog-Styling

Alle Import/Export-Dialoge folgen dem bestehenden Overlay-Pattern (siehe `DeckListView.axaml`):

```xml
<Grid Background="#80000000"> <!-- Dimmed Overlay -->
    <Border Background="{DynamicResource SurfaceBrush}" 
            Padding="24"
            CornerRadius="28"
            MinWidth="320"
            MaxWidth="480"
            BoxShadow="0 4 16 0 #40000000">
        <!-- Dialog-Inhalt -->
    </Border>
</Grid>
```

### Dialog-Elemente

| Element | Styling |
|---------|---------|
| Titel | `FontSize="20" FontWeight="Bold" Foreground="{DynamicResource TextControlForeground}"` |
| Beschreibung | `Foreground="{DynamicResource TextMutedBrush}" TextWrapping="Wrap"` |
| Radio-Buttons | Standard Avalonia, mit `Margin="0,8"` |
| Checkboxen | Standard Avalonia, mit `Margin="0,8"` |
| Primär-Button | `Classes="primary"` (z.B. "Importieren", "Exportieren") |
| Abbrechen-Button | `Classes="secondary"` |

### Farben (Referenz)

| Verwendung | Resource |
|------------|----------|
| Primary (Teal) | `{DynamicResource PrimaryBrush}` — `#018786` (Light) / `#03DAC5` (Dark) |
| Surface | `{DynamicResource SurfaceBrush}` |
| Text | `{DynamicResource TextControlForeground}` |
| Muted Text | `{DynamicResource TextMutedBrush}` |
| Overlay | `#80000000` (50% schwarz) |

---

## Bildpositionierung

Die Position von Bildern innerhalb des Kartentexts muss bei allen Formaten **exakt erhalten bleiben**.

### Aktuelles Format in CapyCard

Bilder sind als Markdown-Syntax im `Front`/`Back`-Text eingebettet:

```markdown
Text vor dem Bild
![Bild](data:image/png;base64,iVBORw0KGgo...)
Text nach dem Bild
```

Die **Position im Text** bestimmt, wo das Bild auf der Karte erscheint.

### Konvertierungsregeln

| Richtung | Transformation | Position erhalten? |
|----------|----------------|-------------------|
| CapyCard → CapyCard | Keine (1:1 Kopie) | ✅ Ja |
| CapyCard → Anki | `![alt](data:...)` → `<img src="0">` | ✅ Ja |
| Anki → CapyCard | `<img src="0">` → `![Bild](data:...)` | ✅ Ja |
| CapyCard → CSV | `![alt](data:...)` → `[Bild: 1]` | ✅ Ja (Platzhalter) |
| CSV → CapyCard | Keine Bilder möglich | ❌ N/A |

### Implementierungsdetails

#### Export: CapyCard → Anki

```csharp
// Regex für Markdown-Bilder
var imageRegex = new Regex(@"!\[([^\]]*)\]\(data:([^;]+);base64,([^)]+)\)");

// Für jedes Match:
// 1. Base64 dekodieren und als Media-Datei speichern (z.B. "0", "1", ...)
// 2. Im Text ersetzen: ![alt](data:...) → <img src="0">
// Position bleibt durch String-Replacement erhalten!
```

#### Import: Anki → CapyCard

```csharp
// Regex für HTML-Bilder
var imgRegex = new Regex(@"<img\s+src=""(\d+)""[^>]*>");

// Für jedes Match:
// 1. Media-Datei laden (aus media-Dictionary)
// 2. Als Base64 kodieren
// 3. Im Text ersetzen: <img src="0"> → ![Bild](data:image/...;base64,...)
```

### Testfälle für Bildpositionierung

- [ ] Bild am **Anfang** der Karte
- [ ] Bild in der **Mitte** des Textes
- [ ] Bild am **Ende** der Karte
- [ ] **Mehrere Bilder** auf einer Karte
- [ ] Bild auf **Vorder- und Rückseite**
- [ ] **Round-Trip-Test:** CapyCard → Anki → CapyCard (Positionen identisch?)
- [ ] **Round-Trip-Test:** CapyCard → CSV → CapyCard (Platzhalter an korrekter Position?)

---

## UI/UX Design

### 1. DeckListView: Import-Button

Platzierung: Rechts neben dem "Fach hinzufügen"-Button

```
┌─────────────────────────────────────────────┐
│ Meine Fächer                                │
├─────────────────────────────────────────────┤
│ [Name des Fachs...    ] [+] [↓ Import] [?]  │
└─────────────────────────────────────────────┘
```

- `[↓ Import]` — Öffnet Datei-Picker, dann Import-Dialog
- `[?]` — Öffnet Format-Info-Dialog

### 2. DeckDetailView: Export-Button

Platzierung: Im Header neben dem Deck-Namen

```
┌─────────────────────────────────────────────┐
│ [← Zurück]  Biologie          [↑ Export]    │
└─────────────────────────────────────────────┘
```

### 3. SubDeckListControl: Export pro Thema

Jedes Unterthema erhält einen kleinen Export-Button:

```
┌─────────────────────────────────────────────┐
│ Themen                                      │
├─────────────────────────────────────────────┤
│ • Zellbiologie (12 Karten)        [↑] [✎]  │
│ • Genetik (8 Karten)              [↑] [✎]  │
└─────────────────────────────────────────────┘
```

### 4. CardListView: Export ausgewählter Karten

Neben dem PDF-Export-Button:

```
┌─────────────────────────────────────────────┐
│ [PDF] [Export] — 5 Karten ausgewählt        │
└─────────────────────────────────────────────┘
```

### 5. Import-Dialog

```
┌─────────────────────────────────────────────────┐
│ Kartenstapel importieren                    [X] │
├─────────────────────────────────────────────────┤
│ Datei: biologie.capycard                        │
│ Gefunden: 42 Karten in 3 Themen                 │
│                                                 │
│ Importieren als:                                │
│ ○ Neues Fach: [Biologie            ]            │
│ ○ In bestehendes Fach: [▼ Auswählen...]         │
│ ○ Als Thema in: [▼ Auswählen...]                │
│                                                 │
│ Optionen:                                       │
│ ☑ Lernfortschritt übernehmen                    │
│ ☐ Vorhandene Karten aktualisieren               │
│                                                 │
│               [Abbrechen]  [Importieren]        │
└─────────────────────────────────────────────────┘
```

### 6. Export-Dialog

```
┌─────────────────────────────────────────────────┐
│ Kartenstapel exportieren                    [X] │
├─────────────────────────────────────────────────┤
│ Exportieren: Fach "Biologie" (42 Karten)        │
│                                                 │
│ Format:                                         │
│ ○ CapyCard (.capycard) — Empfohlen              │
│ ○ Anki (.apkg) — Kompatibel mit Anki            │
│ ○ CSV (.csv) — Für Tabellenkalkulationen        │
│                                                 │
│ Was exportieren:                                │
│ ○ Ganzes Fach inkl. aller Themen                │
│ ○ Ausgewählte Themen:                           │
│   ☑ Zellbiologie (12 Karten)                    │
│   ☐ Genetik (8 Karten)                          │
│   ☑ Ökologie (15 Karten)                        │
│ ○ Nur ausgewählte Karten (5)                    │
│                                                 │
│ Optionen:                                       │
│ ☑ Lernfortschritt mit exportieren               │
│                                                 │
│               [Abbrechen]  [Exportieren]        │
└─────────────────────────────────────────────────┘
```

**Hinweis:** Die Themen-Checkboxen sind nur sichtbar, wenn "Ausgewählte Themen" gewählt ist.

### 7. Format-Info-Dialog

```
┌─────────────────────────────────────────────────┐
│ Import/Export Formate                       [X] │
├─────────────────────────────────────────────────┤
│                                                 │
│ CAPYCARD (.capycard)                            │
│ Das native Format von CapyCard. Enthält alle    │
│ Karten mit Formatierung, Bildern und optional   │
│ deinen Lernfortschritt.                         │
│                                                 │
│ ANKI (.apkg)                                    │
│ Kompatibel mit Anki und AnkiDroid. Ermöglicht   │
│ den Austausch mit Millionen von geteilten       │
│ Kartenstapeln auf ankiweb.net.                  │
│                                                 │
│ CSV (.csv)                                      │
│ Einfaches Tabellenformat. Öffne in Excel oder   │
│ Google Sheets. Bilder werden nicht unterstützt. │
│                                                 │
│                              [Verstanden]       │
└─────────────────────────────────────────────────┘
```

---

## Implementierungs-Schritte

### Phase 1: Grundstruktur (2-3 Tage)

- [ ] **1.1** Verzeichnisstruktur erstellen (`Services/ImportExport/`)
- [ ] **1.2** Interface `IFormatHandler` definieren
- [ ] **1.3** Datenmodelle erstellen (`ImportOptions`, `ExportOptions`, `ImportResult`)
- [ ] **1.4** `ImportExportService` als Koordinator implementieren

### Phase 2: CapyCard-Format (2 Tage)

- [ ] **2.1** `CapyCardFormatHandler` implementieren
- [ ] **2.2** JSON-Serialisierung mit `System.Text.Json`
- [ ] **2.3** ZIP-Handling mit `System.IO.Compression`
- [ ] **2.4** Unit-Tests für Import/Export Round-Trip

### Phase 3: CSV-Format (1 Tag)

- [ ] **3.1** `CsvFormatHandler` implementieren
- [ ] **3.2** Automatische Trennzeichen-Erkennung
- [ ] **3.3** UTF-8 BOM für Excel-Kompatibilität
- [ ] **3.4** Unit-Tests

### Phase 4: Anki-Format (5-7 Tage)

- [ ] **4.1** SQLite-Abhängigkeit prüfen (EF Core nutzt bereits SQLite)
- [ ] **4.2** Anki-Datenbank-Schema als Modelle abbilden
- [ ] **4.3** `AnkiFormatHandler.ImportAsync()` implementieren
  - [ ] ZIP entpacken
  - [ ] `collection.anki2` lesen
  - [ ] Notes → Cards konvertieren
  - [ ] Media-Dateien einbetten
  - [ ] HTML → Markdown konvertieren
- [ ] **4.4** `AnkiFormatHandler.ExportAsync()` implementieren
  - [ ] Markdown → HTML konvertieren
  - [ ] SQLite-Datenbank erstellen
  - [ ] Media extrahieren und referenzieren
  - [ ] ZIP erstellen
- [ ] **4.5** Umfangreiche Tests mit echten Anki-Decks

### Phase 5: UI-Implementierung (3-4 Tage)

- [ ] **5.1** `ImportDialog.axaml` + `ImportViewModel.cs`
- [ ] **5.2** `ExportDialog.axaml` + `ExportViewModel.cs`
- [ ] **5.3** `FormatInfoDialog.axaml`
- [ ] **5.4** Buttons in `DeckListView` hinzufügen
- [ ] **5.5** Buttons in `DeckDetailView` hinzufügen
- [ ] **5.6** Export in `CardListView` integrieren
- [ ] **5.7** Export in `SubDeckListControl` integrieren

### Phase 6: Testen & Polish (3-4 Tage)

- [ ] **6.1** End-to-End-Tests auf Desktop (Windows, macOS, Linux)
- [ ] **6.2** Tests auf iOS (Simulator + echtes Gerät)
  - [ ] Datei-Picker funktioniert
  - [ ] Share Sheet als Export-Alternative
  - [ ] SQLite/Anki-Import funktioniert
- [ ] **6.3** Tests auf Android (Emulator + echtes Gerät)
  - [ ] Storage Access Framework funktioniert
  - [ ] Share Intent als Export-Alternative
  - [ ] SQLite/Anki-Import funktioniert
- [ ] **6.4** Tests im Browser (WASM)
  - [ ] Datei-Upload via `<input type="file">`
  - [ ] Blob-Download für Export
  - [ ] Anki-Format korrekt deaktiviert mit Hinweis
- [ ] **6.5** Bildpositionierungs-Tests (siehe Abschnitt "Bildpositionierung")
  - [ ] Round-Trip: CapyCard → Anki → CapyCard
  - [ ] Round-Trip: CapyCard → CSV → CapyCard (Platzhalter)
  - [ ] Mehrere Bilder pro Karte
- [ ] **6.6** Edge-Cases behandeln (leere Decks, große Dateien, korrupte Dateien)
- [ ] **6.7** Fehlermeldungen und Benutzerführung
- [ ] **6.8** Lokalisierung (deutsche Texte)

---

## Risiken & Edge-Cases

### Technische Risiken

| Risiko | Mitigation |
|--------|------------|
| Anki-Format-Änderungen | Versionscheck einbauen, nur v11+ unterstützen |
| Große Dateien (>100MB) | Streaming, Progress-Anzeige, Speicheroptimierung |
| iOS Sandbox-Einschränkungen | `StorageProvider` von Avalonia verwenden |
| SQLite-Version im Anki-Format | Microsoft.Data.Sqlite verwenden (flexibel) |

### Edge-Cases

| Fall | Verhalten |
|------|-----------|
| Doppelte Karten | Dialog: "Überspringen / Ersetzen / Beide behalten" |
| Karten ohne Deck-Zuordnung | In "Allgemein"-Unterdeck importieren |
| Bilder mit unsupportetem Format | Als Text-Platzhalter `[Bild nicht unterstützt]` |
| Anki Cloze-Karten | Als normale Karten mit `{{c1::text}}` als Text |
| Audio/Video in Anki | Als `[Audio: datei.mp3]` Platzhalter |
| Leere Felder | Warnung anzeigen, aber Import erlauben |

---

## Abhängigkeiten

### Neue NuGet-Pakete

| Paket | Zweck |
|-------|-------|
| `Microsoft.Data.Sqlite` | Anki .anki2 lesen/schreiben |
| *(bereits vorhanden)* `System.IO.Compression` | ZIP-Handling |
| *(bereits vorhanden)* `System.Text.Json` | JSON-Serialisierung |

### Bestehende Nutzung

- `FlashcardDbContext` für Datenbank-Operationen
- `StorageProvider` (Avalonia) für Datei-Dialoge
- Bestehendes Event-Pattern aus `CardListView` (PDF-Export)

---

## Multiplattform-Kompatibilität

### Übersicht

| Plattform | Datei-Picker | Export-Methode | SQLite | Status |
|-----------|--------------|----------------|--------|--------|
| **Desktop** (Win/Mac/Linux) | Native Dialog | Speichern unter... | ✅ Nativ | Volle Funktionalität |
| **iOS** | `UIDocumentPickerViewController` | Share Sheet / Speichern | ✅ via Microsoft.Data.Sqlite | Volle Funktionalität |
| **Android** | Storage Access Framework (SAF) | Share Intent / Speichern | ✅ via Microsoft.Data.Sqlite | Volle Funktionalität |
| **Browser (WASM)** | `<input type="file">` | Blob-Download | ⚠️ sql.js (WebAssembly) | Eingeschränkt* |

*Browser: Anki-Import/Export erfordert `sql.js` für SQLite-Unterstützung im Browser. Alternativ: Anki-Format im Browser deaktivieren und nur CapyCard/CSV anbieten.

### Technische Details pro Plattform

#### Desktop (Windows, macOS, Linux)
- **Datei-Dialoge:** `TopLevel.StorageProvider.OpenFilePickerAsync()` / `SaveFilePickerAsync()`
- **Keine Einschränkungen:** Voller Dateisystem-Zugriff
- **SQLite:** Verwendet native SQLite-Bibliothek via `Microsoft.Data.Sqlite`

#### iOS
- **Datei-Dialoge:** Avalonia mappt auf `UIDocumentPickerViewController`
- **Export-Alternative:** Share Sheet (`UIActivityViewController`) für "Teilen an..." (AirDrop, Mail, etc.)
- **Sandbox:** Nur Zugriff auf von Nutzer ausgewählte Dateien (kein direkter Dateisystem-Zugriff)
- **SQLite:** `Microsoft.Data.Sqlite` enthält native iOS-Bindings

```csharp
// iOS-spezifischer Share-Code (optional, für bessere UX)
#if IOS
public async Task ShareFileAsync(string filePath)
{
    var url = NSUrl.FromFilename(filePath);
    var activityController = new UIActivityViewController(new[] { url }, null);
    await UIApplication.SharedApplication.KeyWindow.RootViewController
        .PresentViewControllerAsync(activityController, true);
}
#endif
```

#### Android
- **Datei-Dialoge:** Avalonia nutzt Storage Access Framework (SAF) seit Android 11
- **Export-Alternative:** Share Intent für "Teilen an..." (WhatsApp, Drive, etc.)
- **Berechtigungen:** Keine zusätzlichen Berechtigungen nötig (SAF ist permission-less)
- **SQLite:** `Microsoft.Data.Sqlite` enthält native Android-Bindings

```csharp
// Android-spezifischer Share-Code (optional, für bessere UX)
#if ANDROID
public void ShareFile(string filePath, string mimeType)
{
    var uri = AndroidX.Core.Content.FileProvider.GetUriForFile(
        Platform.CurrentActivity, 
        $"{Platform.CurrentActivity.PackageName}.fileprovider", 
        new Java.IO.File(filePath));
    
    var intent = new Intent(Intent.ActionSend);
    intent.SetType(mimeType);
    intent.PutExtra(Intent.ExtraStream, uri);
    intent.AddFlags(ActivityFlags.GrantReadUriPermission);
    Platform.CurrentActivity.StartActivity(Intent.CreateChooser(intent, "Exportieren via..."));
}
#endif
```

#### Browser (WASM)
- **Import:** `<input type="file">` via Avalonia's `StorageProvider`
- **Export:** Blob-Download (automatischer Download im Browser)
- **SQLite-Problem:** Browser hat keinen nativen SQLite-Zugriff
- **Lösung für Anki-Format:**
  1. **Option A:** `sql.js` (SQLite kompiliert zu WebAssembly) als JS-Interop einbinden
  2. **Option B:** Anki-Format im Browser deaktivieren (nur CapyCard/CSV)
  3. **Empfehlung:** Option B für v1.0, Option A als spätere Erweiterung

```csharp
// Browser-spezifische Formatprüfung
public IEnumerable<ExportFormat> GetAvailableFormats()
{
    yield return ExportFormat.CapyCard;
    yield return ExportFormat.CSV;
    
    #if !BROWSER
    yield return ExportFormat.Anki; // Anki nur auf nativen Plattformen
    #endif
}
```

### Implementierungs-Checkliste für Multiplattform

- [ ] **P1** Abstraktion über `IFileService` Interface für plattformspezifische Unterschiede
- [ ] **P2** Conditional Compilation (`#if IOS`, `#if ANDROID`, `#if BROWSER`) für Share-Funktionen
- [ ] **P3** Share Sheet auf iOS/Android als Alternative zu "Speichern unter..."
- [ ] **P4** Anki-Format im Browser deaktivieren (mit Hinweis-Text)
- [ ] **P5** Testen auf allen Plattformen vor Release

---

## Offene Fragen

1. **Konfliktbehandlung bei Import:** Soll der Nutzer bei jedem Duplikat gefragt werden, oder einmal für alle?
   - *Empfehlung:* Einmal zu Beginn wählen ("Für alle übernehmen")

2. **Anki-Notiztypen:** Sollen komplexe Note Types (>2 Felder) unterstützt werden?
   - *Empfehlung:* Vorerst nur "Basic" (2 Felder). Warnung bei anderen.

3. **RemNote-Format:** Soll RemNote (.rem, .md) ebenfalls unterstützt werden?
   - *Empfehlung:* In Phase 2 als separates Feature planen

---

## Zusammenfassung

Dieser Plan ermöglicht CapyCard-Nutzern:

- **Eigene Backups** mit vollständigem Lernfortschritt erstellen
- **Kartenstapel teilen** mit anderen CapyCard-Nutzern
- **Anki-Decks** von ankiweb.net oder aus anderen Apps importieren
- **Eigene Decks** für Anki-Nutzer exportieren
- **CSV-Import** aus Excel/Google Sheets für schnelles Erstellen großer Kartenstapel

Die Implementierung folgt den bestehenden Patterns der Codebase (MVVM, Event-basierte Dialoge, kurze DbContext-Lebensdauer) und ist modular aufgebaut, sodass weitere Formate später ergänzt werden können.
