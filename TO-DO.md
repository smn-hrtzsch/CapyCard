# CapyCard To-Do List

## Mobile

- [x] Die Steuerungselemente der Image Preview sind auf mobile Geräten sehr eng aneinander und das sieht nicht schön aus. Auf Desktop passt es so.

### Android

- [x] Zurück-Taste auf Android funktioniert im Learning Mode nicht.

## Features

- [x] Das Hinzufügen, Löschen und Bearbeiten von Themen sollte auch im DeckListView möglich sein. Aktuell ist das nur im CardDetailView möglich. Dazu sollte ein Eingabefeld für das Thema und einen Button zum Hinzufügen geben, das sollte aber dezenter sein, als die Eingabefelder für Fächer im DeckListView. Vielleicht am Anfang der Themen-Liste, wenn man ein Fach ausgeklappt hat.
- [x] Datei-Import sollte auch .txt oder .json Dateien im bereits unterstützem Format (KI/Text-Import Format) möglich sein.
- [ ] Beim Hinzufügen oder Bearbeiten einer Karte sollten die Eingaben für Vorder und Rückseite immer gespeichert werden, auch wenn man die Activity wechselt. Vor allem für den Fall, dass man eine Karte bearbeitet, während die neue Karte noch nicht hinzugefügt wurde. Aktuell gehen die Eingaben verloren, wenn man zum Beispiel, wenn man zum CardListView wechselt und dort eine bestehende Karte bearbeitet und dann wieder zurück zur neuen Karte wechselt. Das ist bei der Bearbeitung nervig.
- [ ] Sitzungsbezogenen Lernfortschritt (sortierter/Zufallsmodus) in Export/Import für .capycard integrieren.
- [ ] Tippfehler Rot unterstreichen
- [ ] Fenstergröße automatisch an Bildschirmgröße anpassen (Desktop-Fenster ist auf kleineren Bildschirmen zu groß)
- [ ] Textgröße auf Mobile anpassen
- [ ] Navigation verbessern (evtl Sidebar hinzufügen oder Pfad oben anzeigen, mit Optionen zum Klicken auf vorherige Seiten)
- [ ] Bei Klick auf Pfeil zwischen Vorder und Rückseite sollte der Modus wechseln, mit der die Karte erstellt wird (Klassisch (Vorder- und Rückseite), Beiseitig (Es werden zwei Karten erstellt - einmal mit der Vorderseite als Vorderseite und einemal mit der Rückseite als Vorderseite))
- [ ] Import/Export von Kartenstapeln (evtl. Anbindung an Anki): Es soll möglich sein bestehenden Kartenstapel zu importieren (aus den gängigen Formaten von Anki/RemNote etc.) und auch Kartenstapel zu exportieren. Es soll auch ein eigenes Import und Export Format geben (z.B. JSON oder CSV). Es müssen auch Bilder und die Formatierung der Karten unterstützt werden. Außerdem soll es einen klaren info-Button geben, der erklärt, wie der Import/Export funktioniert und welche Formate unterstützt werden.
- [x] Es sollte möglich sein sich mit LLMs (z.B. im Web) Karteikarten zu generieren. Meine Idee: Der Nutzer kann ein Thema angeben oder Material (z.B. Text, PDF, Webseite) hochladen und dann werden automatisch Karteikarten generiert. Dafür brauchen wir ein geignetes Format, welches von unserer App als Import akzeptiert wird. Was ist dafür am besten geeignet? Könnten wir z.B. JSON nutzen und dann nicht als Datei Importieren, sondern direkt als Text? Oder beides Anbieten? Können wir einen Button "Prompt erzeugen" Button hinzufügen, der beim Klicken einen vordefinierten Prompt in die Zwischenablage kopiert, den der Nutzer dann in ein LLM seiner Wahl einfügen kann? Der Prompt sollte so gestaltet sein, dass er dem LLM erklärt, wie die Karteikarten formatiert sein sollen, damit sie von unserer App importiert werden können. Für Bilder sollte im Prompt erklärt werden, dass sie entweder direkt als Base64 eingebunden werden können oder ein Verweis auf das Material (z.B. Webseite oder PDF Seite) gegeben werden soll, damit der Nutzer die Bilder manuell hinzufügen kann.
- [ ] Markdown Support im Rich-Text Editor sollte noch erweitert werden. Es sollten noch mehr markdown Features unterstützt werden, wie z.B. Tabellen, Checklisten, Zitate etc. Außerdem sollte es möglich sein, Markdown direkt in den Editor einzufügen und es sollte automatisch formatiert werden. Auch Formeln (LaTeX) sollten unterstützt werden.
- [ ] Sofort-Vorschau im Rich-Text Editor: Während der Nutzer den Text für die Vorder- oder Rückseite eingibt, sollte eine Live-Vorschau angezeigt werden, die zeigt, wie die Karte später aussehen wird. Es sollte sich so anfühlen, als ob man direkt in der Karte schreibt und sieht, wie sie formatiert wird. Momentan muss man erst die Eingabe beenden oder den Fokus wechseln, damit der Editor die Formatierung anzeigt. Das sollte sofort beim Tippen passieren.
- [ ] Die Farbe für die Hervorhebungen auf den Karten sollte an das Farbschema der App angepasst werden. Es sollte dann die Farbe für die Primary Buttons als Hervorherbungsfarbe genutzt werdenu und die Textfarbe der Primary Buttons für die Schriftfarbe der Hervorhebung. So passt es besser zum Design der App.

## UI-Overhaul

- [x] User mehr optionen zur Farbgebung geben (nicht nur Teal sondern z.B. auch Blau, Grün, Rot etc.). Auf ausreichend kontrast muss geachtet werden und dass die App trotzdem ein einheitliches Design hat. Die Position der Einstellungen muss gut gewählt werden (ggf. in der DeckListView in der Titelzeile oben rechts als settings Icon?)
- [x] Unterschiedliche Modi (Light/Dark/Auto) für die App hinzufügen
- [x] Zen Mode für Ablenkungsfreies Lernen (Buttons dezenter und weniger Ablenkung durch Farben)
- [x] Farbauswahl in den Einstellungen nach Farbaehnlichkeit sortieren
- [x] Settings-Dialog responsiv machen (schmale Fenster)

## Editor

- [x] Optionen des Rich-Text Editors sollte man ausblenden können. Auge Icon zum ein- und ausblenden der Optionen. (Persistent via Einstellungen speicherbar)

## Bugs
- [ ] Wenn der Zen Modus aktiviert ist, gibt es ein Problem mit dem Kompakt Modus. Dadurch, dass die Icons für Buttons ausgeblendet werden, ist im Kompaktmodus dann in dem Button gar nichts mehr sichtbar, da der Text auch ausgeblendet wird. In diesem Fall, sollte dann im Kompaktmodus die Icons auch im Zen Modus sichtbar bleiben, damit die Buttons nicht leer sind.
- [ ] Der Ausklapp Button für die Themen überlagert die anderen Buttons im Card Detail View, wenn der Bildschirm zu wenig vertikalen Platz hat. Im Standard Modus ist es okay, da er dort filled ist, aber im Zen Modus kommt es zum Problem, da er nur Outlined ist und sich dann mit den Darunterliegenden Buttons vermischt. Besser wäre es im Zen Modus den Hintergrund oder die Füllfarbe für den Button nicht transparent zu machen, sondern einfach auf die Hintergrund Farbe des DeckDetailViews zu setzen, sodass er gefüllt ist und die anderen Buttons nicht durchscheinen, aber er trotzdem dezent wirkt. Siehe Screenshot, wie es momentan aussieht: 
- [ ] Wenn der User nur ein Thema auswählt zum Exportieren, sollte auch der Name der Export Datei automatisch auf den Namen des Themas gesetzt werden. Wenn mehrere Themen ausgewählt sind, dann sollte der Deck Name + die ausgwählten Themen im Dateinamen stehen.
- [x] Zen Mode deaktivieren aktualisiert Primary Buttons nicht sofort (bleibt bis View/App-Neustart)

## Mobile Bugs

- [ ] Auf Mobile sollte in der Tabelle die Spalte für die Checkboxen schmaler sein und der Quick Button für die Vorschau nicht in jeder Zeile einzeln, sondern oben über der Tabelle rechtsbündig auf Höhe des Titels. Dann kann die letzte Spalte für das 3 Punkte Menü auch noch schmaler sein und wir haben mehr Platz für die Karteninhalte.
- [ ] Vorschau Dialog für Karten im Card List View auf Mobile muss noch responsiv optimiert werden. Die Buttons zum wechseln rechts und links sind zu breit und nehmen zu viel Platz ein. Vielleicht können wir die Buttons etwas kleiner machen oder den Abstand zur Karte verringern, um mehr Platz für die Karte zu haben. Außerdem sollte auf Mobile die Wischgesten nach rechts und links auf dem Touch Screen unterstützt werden, um zur nächsten oder vorherigen Karte zu wechseln. Auch bei schmalen Desktop Fenstern ist die Vorschau nicht optimal dargestellt, hier sollten wir auch die Buttons und Abstände anpassen, damit die Karte mehr Platz hat.
- [ ] Elemente der Image Preview auf Mobile Geräten sieht man teilweise nicht mehr, weil sie nach rechts oder links außerhalb des Bildschirms verschwinden. Prüfe das Verhalten auf verschiedenen Bildschirmgrößen.

## Fixed Bugs

- [x] Die Farben für die Auswahl des Farbschemas im Settings Dialog sollten übereinstimmen mit den tatsächlich verwendeten Farben in der App. Passe die Farbcodes für die Vorschau an, sodass sie gleich ist oder einfach direkt die Farben aus den Theme Dateien laden.
- [x] Beim Starten der App wird sie immer im Standard Modus dargestellt, obwohl ich den Zen Modus aktiviert und gespeichert habe. Erst nach dem erneuten aktivieren zur Laufzeit der App wird alles korrekt im Zen Modus dargestellt. Der Zen Modus sollte direkt beim Start der App geladen und angewendet werden, solange er in den Einstellungen aktiviert ist.
- [x] Kopieren des Systemprompts für die KI-gestützte Kartengenerierung in die Zwischenablage funktioniert auf Windows nicht. Auf MacOS und Android und iOS scheint es zu funktionieren.
- [x] Der Systemprompt, der für die KI-gestützte Kartengenerierung verwendet wird, sollte überarbeitet werden, um konsistentere und qualitativ hochwertigere Ergebnisse zu erzielen. Der aktuelle Prompt führt manchmal zu ungenauen oder unvollständigen Karten. Eine klarere Struktur und spezifischere Anweisungen könnten helfen, bessere Karten zu generieren. Außerdem muss der Prompt so angepasst werden, dass das Modell angewiesen wird eine wirklich ausführliche Anzahl an Karten zu generieren. Vor allem, wenn Material beigelegt wird wie PDFs, Texte oder Webseiten. Die Anzahl der Karten sollte sich nach dem Umfang des Materials richten. Es dient dem Nutzer als Prüfungsvorbereitung und Lerngrundlage, daher sollten so viele relevante Karten wie möglich generiert werden.
- [x] Auf iOS lässt sich das Themen Dropdown im Card Detail View nicht schließen, weil der Button zum schließen nach unten außerhalb des Bildschirms verschwindet. Prüfe dieses Verhalten für Mobile, auch die in Android und die Einabezeile für neue Themen verschwindet nach unten außerhalb des Bildschirms, wenn man die Themen ausklappt. Auf Desktop gibt es auch das Problem, dass die Eingabezeile für neue Themen nach unten außerhalb des Fensters verschwindet.
- [x] Kontraste der Bewertungs-Buttons im Smart Learn Mode verbessert. Schriftfarbe auf dunkles Schwarz/Grau geändert für besseren Kontrast.
- [x] Beim erstellen einer nummerierten Liste im Rich-Text Editor wird die Nummerierung nicht korrekt gehandhabt, wenn man einrückungen vornimmt. Momentan kann man Einrückungen vornehmen und die Nummerierung aus der ersten Ebene wird weitergeführt, anstatt eine neue Ebene zu beginnen. Passe das Verhalten so an, dass bei Einrückungen eine neue nummerierte Ebene begonnen wird.
- [x] Anki Import funktioniert noch nicht. Es gibt diese Meldungen, wenn ich apkg Dateien importieren möchte: "Bitte installieren Sie die aktuelle Anki-Version. Importieren Sie die .colpkg-Datei anschließend erneut." oder "Please update to the latest Anki version, then import the .colpkg/.apkg file again.". Aber ich habe die aktuelle Version von Anki installiert (25.09) und die apkg Dateien sollten funktionieren. Prüfe, ob wir die Anki .apkg Dateien korrekt fomatieren und importieren können.
- [x] Beim Export als .apkg Datei werden Bilder nicht korrekt exportiert und bei Anki nicht angezeigt. Prüfe, ob die Bilder korrekt in die .apkg Datei eingebunden werden.
- [x] Anki Import Fehler 500 "Ein Zahlwert war ungültig" und "index idx_notes_mid already exists" behoben. Ursachen waren leere JSON-Configs in der Legacy-DB, inkompatible Indizes und falsche Datentypen (String statt Zahl bei IDs). Export-Logik ist nun vollständig Anki-konform.
- [x] Markdown Formatierung beim Anki Export verbessert: Listen, Fettdruck, Kursiv, Unterstreichungen und Hervorhebungen werden korrekt in HTML umgewandelt.
- [x] Beim Zoomen in der Image Preview sind die Bereiche oben und unten vom Bild ab einem gewissen Zoom-Level nicht mehr sichtbar und erreichbar. Horizontal klappt das Scrollen, aber Vertikal nicht. Behoben durch Canvas mit dynamischer skalierter Größe anstatt visueller Transformation.
