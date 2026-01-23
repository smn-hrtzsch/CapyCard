# CapyCard To-Do List

## Mobile

- [x] Die Steuerungselemente der Image Preview sind auf mobile Geräten sehr eng aneinander und das sieht nicht schön aus. Auf Desktop passt es so.

### Android

- [x] Zurück-Taste auf Android funktioniert im Learning Mode nicht.

## Features

- [ ] Beim Hinzufügen oder Bearbeiten einer Karte sollten die Eingaben für Vorder und Rückseite immer gespeichert werden, auch wenn man die Activity wechselt. Aktuell gehen die Eingaben verloren, wenn man zum Beispiel, wenn man zum CardListView wechselt und das ist bei der Bearbeitung nervig.
- [ ] Tippfehler Rot unterstreichen
- [ ] Fenstergröße automatisch an Bildschirmgröße anpassen
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

- [ ] Beim Klicken auf "alle auswählen" im CardListView wird automatisch ans Ende der Section gesprungen. Es sollte aber an der aktuellen Position bleiben.
- [ ] Beim Zoomen in der Image Preview sind die Bereiche oben und unten vom Bild ab einem gewissen Zoom-Level nicht mehr sichtbar und erreichbar. Horizontal klappt das Scrollen, aber Vertikal nicht.

## Fixed Bugs

- [x] Im Lern Modus sollten auf Desktop die Buttons für das Zurücksetzen des Lernfortschritts und das Wechseln des Lern Modus zusätztlich zum Icon auch mit Text angezeigt werden und als umrandete Buttons gestylt sein. 
- [x] Doppelklick auf Image Preview auf Mobile sollte auch zoomen können. Einmal Doppelklicken sollte 50% ran zoomen und nochmal Doppelklicken sollte wieder zurück zoomen.
- [x] Mastery Prozent anzeige wird nicht korrekt aktualisiert während des Lernens. Erst nach dem Neu Starten des Lern-Modus wird die Prozentanzeige aktualisiert.
