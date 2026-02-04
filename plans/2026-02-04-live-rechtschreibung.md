# Live Rechtschreibpruefung (de-DE) in der Kartenbearbeitung

**Datum:** 04.02.2026
**Ziel:** Offline-Rechtschreibpruefung (de-DE) beim Hinzufuegen/Bearbeiten von Karten. Fehler werden unterstrichen, Vorschlaege koennen angezeigt und angewendet werden. Grammatikpruefung wird vorerst NICHT umgesetzt.

## 1. Abhaengigkeiten & Assets

- [ ] **Hunspell Library einbinden:** `WeCantSpell.Hunspell` (NuGet).
- [ ] **de-DE Woerterbuch als Asset:** `de_DE.aff` + `de_DE.dic` nach `CapyCard/Resources/Spellcheck/`.
- [ ] **Lizenz pruefen & dokumentieren:** Woerterbuch (z. B. LibreOffice/SCOWL) muss fuer App-Distribution geeignet sein.
- [ ] **Projektdateien anpassen:** Einbindung als `AvaloniaResource` oder `EmbeddedResource` im `CapyCard.csproj`.

## 2. Domain-Model & Services

- [ ] **Modell erstellen:** `CapyCard/Models/TextIssue.cs`
  - Properties: `Start`, `Length`, `Word`, `Suggestions`, `Kind` (z. B. `Spelling`)
- [ ] **Interface definieren:** `CapyCard/Services/TextChecking/ITextCheckingService.cs`
  - `Task<IReadOnlyList<TextIssue>> CheckAsync(string text, string locale, CancellationToken ct)`
- [ ] **Hunspell Service implementieren:** `CapyCard/Services/TextChecking/HunspellSpellCheckService.cs`
  - Dictionary-Laden via `AssetLoader`
  - Caching der `WordList`
  - Tokenizer fuer Worte: Regex mit Unicode-`Letter` (de-DE kompatibel)

## 3. Editor-Integration (WysiwygEditor)

- [ ] **Neue Editor-States:** `SpellCheckEnabled`, `SpellCheckLocale` (Default: `de-DE`)
- [ ] **Debounce + Cancellation:** 300-500 ms (TextChanged -> async Check)
- [ ] **Overlay fuer Unterstreichungen (Edit-Modus):**
  - Neues `TextBlock`-Overlay mit transparentem Foreground
  - Unterstreichungen via Inlines + `TextDecorations`
  - Muss Padding/Font/Wrap exakt wie `EditorTextBox` matchen
- [ ] **Parsing-Helper erweitern:**
  - Methode zum Erzeugen von Inlines mit Unterstreichungen anhand `TextIssue`-Ranges
  - Image-Placeholders (`![img]`) ignorieren (keine Spellchecks)

## 4. Vorschlaege anzeigen & anwenden

- [ ] **Context-Menu auf dem Editor:**
  - Vorschlaege fuer Wort unter Cursor/Selektion
  - Aktionen: Vorschlag anwenden, Ignorieren
- [ ] **Apply-Logik:**
  - Ersetze Text direkt im `EditorTextBox` ueber Range
  - Aktualisiere Issues nach Ersetzung (erneuter Check via Debounce)

## 5. Einbindung in Views

- [ ] `CapyCard/Views/DeckEditorControl.axaml` -> `WysiwygEditor` mit `SpellCheckEnabled=true`
- [ ] `CapyCard/Views/CardListView.axaml` (Edit-Preview) -> aktivieren
- [ ] `CapyCard/Views/LearnView.axaml` (Edit in Learn) -> aktivieren

## 6. Ressourcen & Styling

- [ ] **Underline-Farbressourcen:** z. B. `SpellingUnderlineBrush` in `App.axaml`
- [ ] Unterstreichung farblich ans UI anpassen (Primary/Warning-Ton)

## 7. Verifikation

- [ ] Test: korrekt/inkorrekt (de-DE) in allen Editoren
- [ ] Test: Paste, Undo/Redo, Listen, Markdown-Tokens
- [ ] Performance: lange Texte (5k+ Zeichen)
- [ ] Asset-Laden auf Desktop/Android/iOS/WASM

## Risiken / Edge-Cases

- **Overlay-Alignment:** Padding/Font/Wrap muessen exakt passen, sonst verschoben.
- **Markdown-Token:** Koennen zu falschen Offsets fuehren, wenn nicht ignoriert.
- **Asset-Groesse:** Woerterbuch vergroessert App-Size.
- **Avalonia TextDecorations:** keine echte wellige Linie -> ggf. gestrichelt.

## Ergebnis

Offline-Rechtschreibpruefung de-DE mit Live-Unterstreichungen und Vorschlaegen. Grammatikpruefung bleibt bewusst ausgeschlossen.
