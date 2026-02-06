# CapyCard To-Do List

## In Progress

- [x] Lokales .NET SDK (9.0.310) + Android/iOS Workloads fuer VS Code/Builds eingerichtet.
- [x] Beim Hinzufügen oder Bearbeiten einer Karte sollten die Eingaben für Vorder und Rückseite immer gespeichert werden, auch wenn man die Activity wechselt. Vor allem für den Fall, dass man eine Karte bearbeitet, während die neue Karte noch nicht hinzugefügt wurde. Aktuell gehen die Eingaben verloren, wenn man zum Beispiel, wenn man zum CardListView wechselt und dort eine bestehende Karte bearbeitet und dann wieder zurück zur neuen Karte wechselt. Das ist bei der Bearbeitung nervig. Auch wenn man zurück zum DeckListView wechselt, sollten die Eingaben für die neue Karte gespeichert bleiben. Sie sollten erst gelöscht werden, wenn die Karte tatsächlich hinzugefügt wurde oder der Nutzer explizit den Vorgang abbricht. Auch beim Verlassen der App sollte der Entwurf der neuen Karte gespeichert bleiben, damit der Nutzer später weitermachen kann.

## Mobile

### Android

## Features

- [ ] Sitzungsbezogenen Lernfortschritt (sortierter/Zufallsmodus) in Export/Import für .capycard integrieren.
- [ ] Textgröße auf Mobile anpassen
- [ ] Navigation verbessern (evtl Sidebar hinzufügen oder Pfad oben anzeigen, mit Optionen zum Klicken auf vorherige Seiten)
- [ ] Bei Klick auf Pfeil zwischen Vorder und Rückseite sollte der Modus wechseln, mit der die Karte erstellt wird (Klassisch (Vorder- und Rückseite), Beiseitig (Es werden zwei Karten erstellt - einmal mit der Vorderseite als Vorderseite und einemal mit der Rückseite als Vorderseite))
- [ ] Beim Installieren der App auf den Geräten wird ja korrekt die neue Version über die alte drüber installiert. Auch die Datenbank wird dabei korrekt übernommen. Ich möchte aber gerne als Sicherheitsfeature, dass bei der Installation vor dem Überschreiben der alten Version ein Backup der alten Datenbank erstellt wird. Sodass der Nutzer im Notfall auf die alte Version zurückgehen kann, falls bei der neuen Version etwas schiefgeht. Das Backup sollte in einem speziellen Ordner gespeichert werden, der nur von der App genutzt wird, und sollte mit Datum und Uhrzeit versehen sein, damit der Nutzer weiß, wann das Backup erstellt wurde. Es sollte auch eine Option in den Einstellungen geben, um alte Backups zu löschen, um Speicherplatz zu sparen und um manuell Backups zu erstellen oder anzuwenden.

## UI-Overhaul

## Editor

- [ ] Markdown Support im Rich-Text Editor sollte noch erweitert werden. Es sollten noch mehr markdown Features unterstützt werden, wie z.B. Tabellen, Checklisten, Zitate etc. Außerdem sollte es möglich sein, Markdown direkt in den Editor einzufügen und es sollte automatisch formatiert werden. Auch Formeln (LaTeX) sollten unterstützt werden.
- [ ] Sofort-Vorschau im Rich-Text Editor: Während der Nutzer den Text für die Vorder- oder Rückseite eingibt, sollte eine Live-Vorschau angezeigt werden, die zeigt, wie die Karte später aussehen wird. Es sollte sich so anfühlen, als ob man direkt in der Karte schreibt und sieht, wie sie formatiert wird. Momentan muss man erst die Eingabe beenden oder den Fokus wechseln, damit der Editor die Formatierung anzeigt. Das sollte sofort beim Tippen passieren.
- [ ] Die Farbe für die Hervorhebungen auf den Karten sollte an das Farbschema der App angepasst werden. Es sollte dann die Farbe für die Primary Buttons als Hervorherbungsfarbe genutzt werdenu und die Textfarbe der Primary Buttons für die Schriftfarbe der Hervorhebung. So passt es besser zum Design der App.

## Bugs

## Mobile Bugs

## Fixed Bugs

- [x] macOS-DMG Build auf self-contained Publish umgestellt, damit CapyCard ohne lokal installiertes .NET startet (inkl. Runtime-Check und Signierung im Workflow).
- [x] Manual-Release Workflow erweitert: Dropdown-Strategie fuer Asset-Builds (`missing_only`, `rebuild_selected`, `rebuild_all`) plus gezielte Auswahl einzelner Plattform-Assets.
- [x] macOS-Menueleiste zeigt jetzt den App-Namen `CapyCard` statt `Avalonia Application` durch gesetztes `Application.Name`.
- [x] Fenstergröße beim ersten Start der App automatisch an Bildschirmgröße anpassen (z.b. 75% breite, 85% Höhe) (Desktop-Fenster ist auf kleineren Bildschirmen zu groß beim ersten mal starten der App.
- [x] Performance beim Ausklappen von Decks im DeckListView oder dem Wechseln von der DeckListView zum CardListView verbessern. Aktuell gibt es da teilweise spürbare Verzögerungen, vor allem bei vielen Decks/Karten.
