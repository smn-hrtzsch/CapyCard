# CapyCard To-Do List

## Mobile

- [x] Die Steuerungselemente der Image Preview sind auf mobile Geräten sehr eng aneinander und das sieht nicht schön aus. Auf Desktop passt es so.

### Android

- [x] Zurück-Taste auf Android funktioniert im Learning Mode nicht.

## Features

- [x] Das Hinzufügen, Löschen und Bearbeiten von Themen sollte auch im DeckListView möglich sein. Aktuell ist das nur im CardDetailView möglich. Dazu sollte ein Eingabefeld für das Thema und einen Button zum Hinzufügen geben, das sollte aber dezenter sein, als die Eingabefelder für Fächer im DeckListView. Vielleicht am Anfang der Themen-Liste, wenn man ein Fach ausgeklappt hat.
- [ ] Datei-Import sollte auch .txt oder .json Dateien im bereits unterstützem Format (KI/Text-Import Format) möglich sein.
- [ ] Beim Hinzufügen oder Bearbeiten einer Karte sollten die Eingaben für Vorder und Rückseite immer gespeichert werden, auch wenn man die Activity wechselt. Vor allem für den Fall, dass man eine Karte bearbeitet, während die neue Karte noch nicht hinzugefügt wurde. Aktuell gehen die Eingaben verloren, wenn man zum Beispiel, wenn man zum CardListView wechselt und dort eine bestehende Karte bearbeitet und dann wieder zurück zur neuen Karte wechselt. Das ist bei der Bearbeitung nervig.
- [ ] Tippfehler Rot unterstreichen
- [ ] Fenstergröße automatisch an Bildschirmgröße anpassen (Desktop-Fenster ist auf kleineren Bildschirmen zu groß)
- [ ] Textgröße auf Mobile anpassen
- [ ] Navigation verbessern (evtl Sidebar hinzufügen oder Pfad oben anzeigen, mit Optionen zum Klicken auf vorherige Seiten)
- [ ] Bei Klick auf Pfeil zwischen Vorder und Rückseite sollte der Modus wechseln, mit der die Karte erstellt wird (Klassisch (Vorder- und Rückseite), Beiseitig (Es werden zwei Karten erstellt - einmal mit der Vorderseite als Vorderseite und einemal mit der Rückseite als Vorderseite))
- [ ] Import/Export von Kartenstapeln (evtl. Anbindung an Anki): Es soll möglich sein bestehenden Kartenstapel zu importieren (aus den gängigen Formaten von Anki/RemNote etc.) und auch Kartenstapel zu exportieren. Es soll auch ein eigenes Import und Export Format geben (z.B. JSON oder CSV). Es müssen auch Bilder und die Formatierung der Karten unterstützt werden. Außerdem soll es einen klaren info-Button geben, der erklärt, wie der Import/Export funktioniert und welche Formate unterstützt werden.
- [x] Es sollte möglich sein sich mit LLMs (z.B. im Web) Karteikarten zu generieren. Meine Idee: Der Nutzer kann ein Thema angeben oder Material (z.B. Text, PDF, Webseite) hochladen und dann werden automatisch Karteikarten generiert. Dafür brauchen wir ein geignetes Format, welches von unserer App als Import akzeptiert wird. Was ist dafür am besten geeignet? Könnten wir z.B. JSON nutzen und dann nicht als Datei Importieren, sondern direkt als Text? Oder beides Anbieten? Können wir einen Button "Prompt erzeugen" Button hinzufügen, der beim Klicken einen vordefinierten Prompt in die Zwischenablage kopiert, den der Nutzer dann in ein LLM seiner Wahl einfügen kann? Der Prompt sollte so gestaltet sein, dass er dem LLM erklärt, wie die Karteikarten formatiert sein sollen, damit sie von unserer App importiert werden können. Für Bilder sollte im Prompt erklärt werden, dass sie entweder direkt als Base64 eingebunden werden können oder ein Verweis auf das Material (z.B. Webseite oder PDF Seite) gegeben werden soll, damit der Nutzer die Bilder manuell hinzufügen kann.

## Card Grid View

## UI-Overhaul

- [x] Komplett neues App Design:
  - [x] Orientierung am Design der Shopping List App (siehe Referenz für Farben und Styles)
  - [x] Neues Farbschema
  - [x] einheitliche Design-Sprache bei Icons
  - [x] abgerundete Ecken bei Buttons, Eingabe-Feldern, Dialogen (falls vorhanden)
  - [x] Eingabefelder für Themen und Fächer sollten Abstand zum rand haben und eher schweben, als am Rand kleben

## Bugs

- [ ] Info Button beim File Selection Dialog für "Datei auswählen" Button muss noch hinzufgefügt werden, der erklärt, welche Formate unterstützt werden und wie der Import funktioniert. Ebenso ein Info Button für den "Via KI/Text importieren" Button.
- [ ] Bei der Auswahl, was exportiert werden soll ist bei der Option "Ausgewählte Themen" standardmäßig jedes Thema ausgewählt. Aber es sollten standardmäßig keine Themen ausgewählt sein, damit der Nutzer explizit die Themen auswählen muss, die er exportieren möchte.
- [ ] Breite der Auswahl der Themen beim Export ist viel zu breit. Sie sollte auf die Breite des Dialogs begrenzt sein und lange Themennamen sollten einfach umgebrochen werden.
- [ ] Option "Lernfortschritt übernehmen" und "Lernfortschritt mit exportieren" sollte standardmäßig deaktiviert sein.
- [ ] Position des x-Buttons zum Schließen des Import/Export Dialogs ist nicht schön, sie sollte wirklich oben rechts am Rand sein, sie ist aber viel weiter richtung Mitte versetzt. Außerdem ist der Hover Effekt zu dezent.
- [ ] Anki Import funktioniert noch nicht. Es gibt diese Meldungen, wenn ich apkg Dateien importieren möchte: "Bitte installieren Sie die aktuelle Anki-Version. Importieren Sie die .colpkg-Datei anschließend erneut." oder "Please update to the latest Anki version, then import the .colpkg/.apkg file again.". Aber ich habe die aktuelle Version von Anki installiert (25.09) und die apkg Dateien sollten funktionieren. Prüfe, ob wir die Anki .apkg Dateien korrekt fomatieren und importieren können.
- [ ] Beim Export als .apkg Datei werden Bilder nicht korrekt exportiert und bei Anki nicht angezeigt. Prüfe, ob die Bilder korrekt in die .apkg Datei eingebunden werden.
- [ ] Wenn die Fenstergröße zu schmal wird, sollte im DeckDetailView die Eingabe für die Karten untereinander sein, wie auf Mobile Geräten, um den Platz besser zu nutzen. Momentan wird die Eingabe für die Karten immer kleiner, wenn das Fenster schmaler wird, was nicht schön aussieht.
- [ ] Bei sehr schmalem Display ist die Fortschrittsanzeige im Lern Modus nicht schön, der Text zum Lernmodus und die Anzahl oder Prozentanzeige werden von der Progressbar überschattet und verdeckt. Das Layout sollte sich anpassen, sodass der Text und die Anzeige immer sichtbar sind.
- [ ] Dialoge sollten immer mit der Escape Taste geschlossen werden können. Wenn es eine Abrage gibt, ob abbrechen oder bestätigen, dann sollte Escape abbrechen und Enter bestätigen.
- [ ] Wenn der User nur ein Thema auswählt zum Exportieren, sollte auch der Name der Export Datei automatisch auf den Namen des Themas gesetzt werden. Wenn mehrere Themen ausgewählt sind, dann sollte der Deck Name + die ausgwählten Themen im Dateinamen stehen.
- [ ] Beim Klicken auf "alle auswählen" im CardListView wird automatisch ans Ende der Section gesprungen. Es sollte aber an der aktuellen Position bleiben.
- [ ] Beim Zoomen in der Image Preview sind die Bereiche oben und unten vom Bild ab einem gewissen Zoom-Level nicht mehr sichtbar und erreichbar. Horizontal klappt das Scrollen, aber Vertikal nicht.

## Fixed Bugs

- [x] Größe des Export Buttons sollte so angepasst werden, dass er wie der Filled Button "Zurück zur Fächerliste" aussieht (gleiche Höhe und Padding).
- [x] Styling der Radio Buttons im Import und Export Dialog sind noch nicht korrekt: es wird noch die System Akzentfarbe für den Rahmen benutzt, obwohl es einfach nur der Teal sein sollte, wenn die Option ausgewählt ist. Auch beim Hovern sollte nicht die System Akzentfarbe genutzt werden. Zwischen Rahmen und innerem Kreis sollte es einfach transparent sein. Der Kreis in der Mitte des Radio Buttons sollte auch ein bisschen größer sein. Siehe Screenshot:
- [x] Der Export Dialog und der Import Dialog sollten noch breiter sein und kann auch, solange es die Bildschirmgröße zulässt höher sein. Momentan ist auch der die Scroll Leiste viel zu nah am Text und dem Inhalt des Dialogs, da muss mehr Abstand hin, siehe Screenshot:

- [x] Text oder Icon Farbe für die Hinzufügen-Buttons und Import/Export-Buttons im DeckListView und DeckdetailView sollten auch in einem Grau sein, wie der Text der "Zurück zur Fächerliste" Button zum Beispiel. Das Design sollte einheitlich sein.
- [x] Beim Build des Projekts werden einige Warnungen für WASM angezeigt. Ich möchte bis aus weiteres erstmal keine WASM Entwicklung anstreben, entferne es also erstmal aus der sln oder der csproj Datei, sodass die Warnungen weg sind. Wenn man es einfach deaktivieren kann, dass WASM immer mit gebaut wird, dann wäre das auch okay.
- [x] "Themen"-Button im CardDetailView funktioniert nicht mehr (öffnet das Themen-Auswahl-Menü nicht mehr)
- [x] Wenn das Fenster vertikel zu klein wird, dann überlappt der "Themen"-Ausklapp-Button mit der Section für die Lern Buttons. (Siehe Screenshot) Es muss also die Aufteilung, die die obere Sektion des CardDetailView zum Scrollview mach angepasst werden, sodass sie nicht mehr überlappen, schau dir im Detail an, wie es momentan geregelt ist und fixe das.
- [x] Im Lern Modus sollten auf Desktop die Buttons für das Zurücksetzen des Lernfortschritts und das Wechseln des Lern Modus zusätztlich zum Icon auch mit Text angezeigt werden und als umrandete Buttons gestylt sein.
- [x] Doppelklick auf Image Preview auf Mobile sollte auch zoomen können. Einmal Doppelklicken sollte 50% ran zoomen und nochmal Doppelklicken sollte wieder zurück zoomen.
- [x] Mastery Prozent anzeige wird nicht korrekt aktualisiert während des Lernens. Erst nach dem Neu Starten des Lern-Modus wird die Prozentanzeige aktualisiert.
