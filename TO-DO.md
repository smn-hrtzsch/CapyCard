# CapyCard To-Do List

## Mobile

- [x] Die Steuerungselemente der Image Preview sind auf mobile Geräten sehr eng aneinander und das sieht nicht schön aus. Auf Desktop passt es so.

### Android

- [x] Zurück-Taste auf Android funktioniert im Learning Mode nicht.

## Features

- [ ] Beim Hinzufügen oder Bearbeiten einer Karte sollten die Eingaben für Vorder und Rückseite immer gespeichert werden, auch wenn man die Activity wechselt. Vor allem für den Fall, dass man eine Karte bearbeitet, während die neue Karte noch nicht hinzugefügt wurde. Aktuell gehen die Eingaben verloren, wenn man zum Beispiel, wenn man zum CardListView wechselt und dort eine bestehende Karte bearbeitet und dann wieder zurück zur neuen Karte wechselt. Das ist bei der Bearbeitung nervig.
- [ ] Tippfehler Rot unterstreichen
- [ ] Fenstergröße automatisch an Bildschirmgröße anpassen (Desktop-Fenster ist auf kleineren Bildschirmen zu groß)
- [ ] Textgröße auf Mobile anpassen
- [ ] Navigation verbessern (evtl Sidebar hinzufügen oder Pfad oben anzeigen, mit Optionen zum Klicken auf vorherige Seiten)
- [ ] Bei Klick auf Pfeil zwischen Vorder und Rückseite sollte der Modus wechseln, mit der die Karte erstellt wird (Klassisch (Vorder- und Rückseite), Beiseitig (Es werden zwei Karten erstellt - einmal mit der Vorderseite als Vorderseite und einemal mit der Rückseite als Vorderseite))
- [ ] Import/Export von Kartenstapeln (evtl. Anbindung an Anki): Es soll möglich sein bestehenden Kartenstapel zu importieren (aus den gängigen Formaten von Anki/RemNote etc.) und auch Kartenstapel zu exportieren. Es soll auch ein eigenes Import und Export Format geben (z.B. JSON oder CSV). Es müssen auch Bilder und die Formatierung der Karten unterstützt werden. Außerdem soll es einen klaren info-Button geben, der erklärt, wie der Import/Export funktioniert und welche Formate unterstützt werden.

## Card Grid View

## UI-Overhaul

- [ ] Komplett neues App Design:
  - [ ] Orientierung am Design der Shopping List App (siehe Referenz für Farben und Styles)
  - [ ] Neues Farbschema
  - [ ] einheitliche Design-Sprache bei Icons
  - [ ] abgerundete Ecken bei Buttons, Eingabe-Feldern, Dialogen (falls vorhanden)
  - [ ] Eingabefelder für Themen und Fächer sollten Abstand zum rand haben und eher schweben, als am Rand kleben

## Bugs

- [ ] Beim Build des Projekts werden einige Warnungen für WASM angezeigt. Ich möchte bis aus weiteres erstmal keine WASM Entwicklung anstreben, entferne es also erstmal aus der sln oder der csproj Datei, sodass die Warnungen weg sind. Wenn man es einfach deaktivieren kann, dass WASM immer mit gebaut wird, dann wäre das auch okay.
- [ ] Text oder Icon Farbe für die Hinzufügen-Buttons und Import/Export-Buttons im DeckListView und DeckdetailView sollten auch in einem Grau sein, wie der Text der "Zurück zur Fächerliste" Button zum Beispiel. Das Design sollte einheitlich sein.
- [ ] Der Export Dialog und der Import Dialog sollten noch breiter sein und kann auch, solange es die Bildschirmgröße zulässt höher sein. Momentan ist auch der die Scroll Leiste viel zu nah am Text und dem Inhalt des Dialogs, da muss mehr Abstand hin, siehe Screenshot: 
- [ ] Styling der Radio Buttons im Import und Export Dialog sind noch nicht korrekt: es wird noch die System Akzentfarbe für den Rahmen benutzt, obwohl es einfach nur der Teal sein sollte, wenn die Option ausgewählt ist. Siehe Screenshot: 
- [ ] Bei der Auswahl, was exportiert werden soll ist bei der Option "Ausgewählte Themen" standardmäßig jedes Thema ausgewählt. Aber es sollten standardmäßig keine Themen ausgewählt sein, damit der Nutzer explizit die Themen auswählen muss, die er exportieren möchte.
- [ ] Wenn der User nur ein Thema auswählt zum Exportieren, sollte auch der Name der Export Datei automatisch auf den Namen des Themas gesetzt werden. Wenn mehrere Themen ausgewählt sind, dann sollte der Deck Name + die ausgwählten Themen im Dateinamen stehen.
- [ ] Beim Klicken auf "alle auswählen" im CardListView wird automatisch ans Ende der Section gesprungen. Es sollte aber an der aktuellen Position bleiben.
- [ ] Beim Zoomen in der Image Preview sind die Bereiche oben und unten vom Bild ab einem gewissen Zoom-Level nicht mehr sichtbar und erreichbar. Horizontal klappt das Scrollen, aber Vertikal nicht.

## Fixed Bugs

- [x] "Themen"-Button im CardDetailView funktioniert nicht mehr (öffnet das Themen-Auswahl-Menü nicht mehr)
- [x] Wenn das Fenster vertikel zu klein wird, dann überlappt der "Themen"-Ausklapp-Button mit der Section für die Lern Buttons. (Siehe Screenshot) Es muss also die Aufteilung, die die obere Sektion des CardDetailView zum Scrollview mach angepasst werden, sodass sie nicht mehr überlappen, schau dir im Detail an, wie es momentan geregelt ist und fixe das.
- [x] Im Lern Modus sollten auf Desktop die Buttons für das Zurücksetzen des Lernfortschritts und das Wechseln des Lern Modus zusätztlich zum Icon auch mit Text angezeigt werden und als umrandete Buttons gestylt sein. 
- [x] Doppelklick auf Image Preview auf Mobile sollte auch zoomen können. Einmal Doppelklicken sollte 50% ran zoomen und nochmal Doppelklicken sollte wieder zurück zoomen.
- [x] Mastery Prozent anzeige wird nicht korrekt aktualisiert während des Lernens. Erst nach dem Neu Starten des Lern-Modus wird die Prozentanzeige aktualisiert.
