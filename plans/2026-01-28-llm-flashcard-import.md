# Plan: LLM-basierte Karteikarten-Generierung

## Ziel
Ermöglichen, dass Nutzer Inhalte von LLMs (wie ChatGPT, Claude, etc.) direkt in CapyCard importieren können, indem sie einen JSON-Text einfügen. Der Prozess soll durch eine klare UI und Hilfestellungen (System-Prompt) geführt werden.

## Betroffene Dateien

### Neu zu erstellen
- `CapyCard/CapyCard/Services/ImportExport/Formats/JsonFormatHandler.cs`: Implementiert `IFormatHandler` für `.json`.
- `CapyCard/CapyCard/Services/ImportExport/Models/JsonImportModels.cs`: DTOs (`JsonDeck`, `JsonCard`).
- `CapyCard/CapyCard/Views/LlmImportDialog.axaml` (und `.cs`): Der neue Dialog für den Text-Import.

### Zu ändern
- `CapyCard/CapyCard/Services/ImportExport/ImportExportService.cs`: Registrierung des `JsonFormatHandler`.
- `CapyCard/CapyCard/ViewModels/ImportViewModel.cs`:
    - Logik für den LLM-Import-Workflow (`IsLlmImportVisible`, `ImportJsonText`).
    - `GenerateSystemPrompt()` Methode.
    - Commands: `OpenLlmImportCommand`, `CopyPromptCommand`, `AnalyzeTextCommand`.
- `CapyCard/CapyCard/Views/FileSelectionDialog.axaml`:
    - Hinzufügen eines Buttons "Via LLM / Text importieren" neben "Datei auswählen".
    - Integration des `LlmImportDialog` (via Binding an `IsLlmImportVisible`).
- `CapyCard/CapyCard/Services/IClipboardService.cs`:
    - Methode `SetTextAsync(string text)` hinzufügen (und Implementierungen für Android/iOS/Desktop anpassen/mocken, falls nötig).
- `CapyCard/CapyCard/Views/FormatInfoDialog.axaml`:
    - Information über das JSON-Format hinzufügen.

## Design-Richtlinien
- **Aesthetik:** Einhaltung der "Shopping List" Ästhetik (Teal/Purple Farben, abgerundete Ecken).
- **Buttons:** Verwendung von Pill-shaped Buttons (`CornerRadius="25"`) für Hauptaktionen.
- **Karten:** Verwendung von großen abgerundeten Karten (`CornerRadius="28"`).
- **Icons:** Konsequente Nutzung von `Material.Icons.Avalonia`.
- **UX:** Konsistente Abstände und Typografie gemäß `guidelines/DESIGN.md`.

## Schritte

### 1. Datenmodelle & Format-Handler
- [x] **Modelle:** Erstelle `JsonImportModels.cs`.
- [x] **Handler:** Implementiere `JsonFormatHandler.cs`.
    - Muss `AnalyzeAsync` (Parsing, Validierung) und `ImportAsync` unterstützen.
    - Muss robust gegenüber Markdown-Code-Fences (` ```json ... ``` `) sein.
    - Muss Base64-Bilder extrahieren und temporär speichern.
- [x] **Service:** Registriere den Handler in `ImportExportService`.

### 2. Clipboard Service Erweiterung
- [x] Erweitere `IClipboardService` Interface um `Task SetTextAsync(string text)`.
- [x] Implementiere dies für die Plattformen (Android, iOS, Desktop).

### 3. ViewModel Erweiterung (`ImportViewModel`)
- [x] **Properties:** `IsLlmImportVisible` (bool), `ImportJsonText` (string).
- [x] **Prompt-Logik:** Erstelle `GenerateSystemPrompt()`.
    - *Vielseitigkeit:* Der Prompt muss sowohl für spezifisches Material (PDF, Folien) als auch für allgemeine Themen funktionieren.
    - *Struktur:* Klare JSON-Vorgabe, Feldbezeichnungen (Front, Back).
    - *Bilder:* Anleitung für Base64-Einbindung oder Verweise auf Quellen.
- [x] **Commands:**
    - `OpenLlmImportCommand`: Setzt `IsLlmImportVisible = true`.
    - `CloseLlmImportCommand`: Setzt `IsLlmImportVisible = false`.
    - `CopyPromptCommand`: Nutzt `ClipboardService.SetTextAsync` mit dem Prompt.
    - `AnalyzeTextCommand`: Wandelt `ImportJsonText` in Stream um und ruft `AnalyzeFileAsync` auf.

### 4. UI: FileSelectionDialog & LlmImportDialog
- [x] **FileSelectionDialog:** Füge Button "Via AI / Text importieren" hinzu.
- [x] **LlmImportDialog:** Erstelle UserControl basierend auf `SurfaceBrush` und `CornerRadius="28"`.
    - **Anleitung:** Schritt-für-Schritt Erklärung (Chat öffnen, Material/Thema angeben, Prompt nutzen, JSON einfügen).
    - **Hinweise:** "Scheibchenweise importieren" für bessere Qualität.
    - **Aktion:** Button "Prompt kopieren" und Textfeld für JSON.
- [x] **FormatInfoDialog:** Ergänze JSON Formatbeschreibung.

### 5. Verifikation
- [x] Teste den Flow: Klick auf "Via AI", Prompt kopieren, (Simulierter Chat), JSON einfügen, Import.
- [x] Prüfe Fehlerbehandlung bei ungültigem JSON.
- [x] Prüfe Bild-Import via Base64.

## Risiken & Hinweise
- **Prompt:** Muss so formuliert sein, dass er *immer* JSON liefert ("Antworte ausschließlich im JSON-Format...").
- **Dateigröße:** Bei vielen Base64-Bildern kann der JSON-String sehr groß werden.
