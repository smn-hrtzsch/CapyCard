# Erweiterter Markdown-Support im Rich-Text-Editor

**Datum:** 11.02.2026  
**Ziel:** Der Rich-Text-Editor und die Anzeige-Komponenten sollen zusaetzliche Markdown-Features konsistent unterstuetzen: Tabellen, Checklisten, Zitate und Formeln (Unicode + LaTeX-Syntax). Markdown soll direkt eingefuegt werden koennen und korrekt formatiert dargestellt werden. Der KI-System-Prompt fuer den Kartenimport soll entsprechend aktualisiert werden.  
**Abgrenzung:** Live-WYSIWYG waehrend des Tippens (separater TO-DO-Punkt) ist nicht Teil dieses Plans.

## Betroffene Dateien

- `CapyCard/CapyCard/CapyCard/Services/MarkdownService.cs` (zentraler Parser/Normalizer, aktuell leer)
- `CapyCard/CapyCard/CapyCard/Controls/WysiwygEditor.Parsing.cs` (Rendering im Editor-Preview)
- `CapyCard/CapyCard/CapyCard/Controls/WysiwygEditor.Formatting.cs` (Paste-Normalisierung)
- `CapyCard/CapyCard/CapyCard/Controls/WysiwygEditor.axaml` (optionale Toolbar-Erweiterung fuer neue Markdown-Aktionen)
- `CapyCard/CapyCard/CapyCard/Controls/WysiwygEditor.axaml.cs` (Handler/Shortcuts fuer neue Aktionen)
- `CapyCard/CapyCard/CapyCard/Controls/FormattedTextBlock.cs` (Read-only Anzeige in Learn/CardList)
- `CapyCard/CapyCard/CapyCard/ViewModels/ImportViewModel.cs` (System-Prompt in `GenerateSystemPrompt()`)
- `CapyCard/CapyCard/CapyCard/Services/Pdf/QuestPdfDocument.cs` (Export-Fallback fuer neue Syntax)
- `CapyCard/CapyCard/CapyCard/Services/ImportExport/Formats/AnkiFormatHandler.cs` (Anki-Konvertierungs-Fallback)
- `CapyCard/CapyCard.Tests/MarkdownServiceTests.cs` (neu, Parser/Regressionstests)
- optional (nur falls externe Markdown-Lib genutzt wird): `CapyCard/Directory.Packages.props`, `CapyCard/CapyCard/CapyCard/CapyCard.csproj`

## Schritte

- [ ] 1) Markdown-Formatvertrag (MVP) finalisieren: Tabellen (Pipe-Syntax), Checklisten (`- [ ]`, `- [x]`), Zitate (`>`), Formeln (`$...$`, `$$...$$`) als offiziell unterstuetzte Features definieren.
- [ ] 2) Rueckwaertskompatibilitaet festschreiben: bestehende Syntax fuer Fett/Kursiv/Unterstrichen/Highlight/Bilder/Listen bleibt unveraendert.
- [ ] 3) `MarkdownService` in `Services/MarkdownService.cs` implementieren und als zentrale Parse-/Normalize-Schicht etablieren.
- [ ] 4) Input-Normalisierung implementieren (CRLF/LF vereinheitlichen, optionale Markdown-Codefences beim Paste entfernen, stabile Leerzeilenbehandlung).
- [ ] 5) Inline-Parsing robust machen: Formel- und Bildsegmente vor Inline-Markup schuetzen, damit `*`/`_` in Formeln nicht falsch als Kursiv/Underline interpretiert werden.
- [ ] 6) Block-Parsing erweitern: Tabellenblock-Erkennung, Checklisten-Erkennung, Zitatbloecke, Block-Formeln.
- [ ] 7) `WysiwygEditor.Parsing.cs` auf `MarkdownService` umstellen und neue Blocktypen rendern (Checklisten, Zitate, Tabellen, Formeln).
- [ ] 8) `FormattedTextBlock.cs` ebenfalls auf dieselbe Parse-/Renderbasis umstellen, damit Anzeige in Learn/CardList identisch zum Editor-Preview ist.
- [ ] 9) Paste-Flow in `WysiwygEditor.Formatting.cs` erweitern: eingefuegtes Markdown normalisieren und als ein Undo-Schritt einpflegen, ohne Bild-Paste zu brechen.
- [ ] 10) Optional Toolbar erweitern (`WysiwygEditor.axaml` + `.axaml.cs`): Schnellaktionen fuer Checkliste, Zitat, Tabelle, Formeltemplate.
- [ ] 11) KI-System-Prompt in `ImportViewModel.cs` aktualisieren: neue erlaubte Formate explizit auffuehren, bisheriges Verbot fuer Tabellen/Zitate entfernen.
- [ ] 12) Prompt um JSON-Escaping-Regeln fuer Formeln ergaenzen (`\\` in LaTeX, `\n` fuer Zeilenumbrueche) und Beispielkarten mit Tabelle + Formel aufnehmen.
- [ ] 13) Export-Fallback in `QuestPdfDocument.cs` absichern: neue Features mindestens lesbar degradieren (z. B. Checklisten als `[ ]/[x]`, Tabellen als textuelle Darstellung).
- [ ] 14) Export-Fallback in `AnkiFormatHandler.cs` absichern: neue Syntax bei Konvertierung nicht zerstoeren, mindestens textuell erhalten.
- [ ] 15) Unit-Tests neu anlegen (`MarkdownServiceTests`): Tabellen, Checklisten, Zitate, Inline-/Block-Formeln, sowie Formel-Regression mit `Sigma*`, `Gamma*`, `q_0`.
- [ ] 16) Prompt-Regressionstest ergaenzen (String-basierter Test), damit erlaubte/unerlaubte Formatregeln nicht unbemerkt zurueckdrehen.
- [ ] 17) Build/Test ausfuehren: `dotnet build` und `dotnet test`.
- [ ] 18) Manuelle Verifikation in allen Editor-Kontexten (`DeckEditorControl`, `CardListView`, `LearnView`) inkl. End-to-End-KI-Import mit Tabelle/Formel-Beispiel.

## Risiken / Edge-Cases

- Formelzeichen-Konflikte: `*`, `_`, `|` in Formeln koennen Markup triggern, wenn nicht vorab geschuetzt.
- JSON-Gueltigkeit: LaTeX ohne korrektes Escaping fuehrt zu ungueltigem Import-JSON.
- Tabellenlayout in kleinen Breiten: Overflow/Umbruch kann Lesbarkeit verschlechtern.
- Renderer-Drift: Ohne zentrale Parse-Schicht divergieren Editor, Read-only-Anzeige und Exporte.
- Performance: grosse Tabellen oder lange Formeln duerfen den UI-Thread nicht blockieren.
- Bestehende Daten: Alte Karten duerfen durch Parser-Erweiterung nicht anders interpretiert werden.
