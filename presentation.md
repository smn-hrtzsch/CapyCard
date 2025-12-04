<!--

author:   Simon Hörtzsch
email:    simon.hoertzsch@student.tu-freiberg.de
version:  1.0.3
language: de
narrator: Deutsch Female

import: https://raw.githubusercontent.com/LiaTemplates/dbdiagram/main/README.md
        https://raw.githubusercontent.com/liaScript/mermaid_template/master/README.md

-->

[![LiaScript](https://raw.githubusercontent.com/LiaScript/LiaScript/master/badges/course.svg)](https://liascript.github.io/course/?https://raw.githubusercontent.com/DeinUsername/CapyCard/main/presentation.md)

# Architektur: CapyCard Datenbank

<!-- data-type="none" -->
| Parameter            | Kursinformationen                                                                     |
| -------------------- | --------------------------------------------------------------------------------------|
| **Veranstaltung:**   | `Datenbanksysteme`                                                                    |
| **Semester**         | `Wintersemester 2025/2026`                                                            |
| **Hochschule:**      | `Technische Universität Bergakademie Freiberg`                                        |
| **Thema:**           | `Architekturvorstellung: CapyCard`                                                    |
| **Datum:**           | 05.12.2025                                                                            |
| **Autor**            | Simon Hörtzsch                                                                        |

---

<!-- Simon -->
## 1. Tech Stack

**Ziel der App:**

Speicherung von hierarchischen Kartenstapeln (Decks), Lernkarten und komplexen Lernzuständen auf mobilen Endgeräten und Desktops.

**Technologien:**

* **UI Framework:** Avalonia UI (Cross-Platform für Desktop & Mobile)

* **Datenbank:** SQLite (Lokale Datei auf dem Gerät)

* **ORM:** Entity Framework Core (EF Core)

---

## 2. Exkurs: ORM & Migrationen

**Was bedeutet ORM?**

ORM steht für **Object-Relational Mapping**. Es ist eine "Übersetzungsschicht" zwischen C# Klassen und SQL Tabellen.

* *SQL:*

  `SELECT * FROM Cards WHERE DeckId = 5`

* *C#:*

  `dbContext.Cards.Where(c => c.DeckId == 5).ToList()`

**Was sind Migrationen?**

Migrationen sind die "Versionsverwaltung" für das Datenbankschema.

* **Was machen sie?** Wenn wir im Code eine Klasse ändern (z.B. `Card` bekommt ein neues Feld `ImageUrl`), erstellt EF Core automatisch eine Migrations-Datei.
* **Wie funktionieren sie?** Jede Migration hat zwei Methoden:

  * `Up()`: Führt die Änderung durch (z.B. `CREATE COLUMN ImageUrl`).
  * `Down()`: Macht die Änderung rückgängig (z.B. `DROP COLUMN ImageUrl`).

* **Wofür zuständig?** Sie garantieren, dass die Datenbank auf allen Geräten (Entwickler-PC, Smartphone des Nutzers) exakt das gleiche Schema hat. Beim App-Start wird geprüft: "Welche Migration fehlt noch?" und diese dann angewendet.
* **Probleme:**

  * *Datenverlust:* Wenn man eine Spalte umbenennt, denkt die Datenbank oft, man will die alte löschen und eine neue erstellen -> Daten weg!
    *Lösung:* Manuelle Anpassung der Migration (z.B. SQL-Scripts zur Datenrettung).
  * *Konflikte:* Wenn zwei Entwickler gleichzeitig Migrationen erstellen, passen die Zeitstempel nicht mehr zusammen.

---

## 3. Die Tabellen im Detail

Wir schauen uns die 4 Haupt-Tabellen und ihre Aufgaben genau an.

                         {{0-1}}
********************************************************************************

**1. `Decks` (Die Struktur)**

Organisiert alles in Fächer und Themen.

| Feld | Typ | Beschreibung |
| :--- | :--- | :--- |
| `Id` | **PK** | Eindeutige Kennung. |
| `Name` | TEXT | Titel des Fachs (z.B. "Englisch"). |
| `ParentDeckId` | **FK** | Zeigt auf ein anderes Deck -> Ermöglicht Unterordner! |
| `IsDefault` | BOOL | Markiert den Standard-Ordner "Allgemein". |

**Besonderheit: Rekursion (Self-Referencing)**

Wir bilden Ordnerstrukturen über eine Selbstreferenz ab.

| Id | Name | ParentDeckId | Typ |
| :--- | :--- | :--- | :--- |
| 1 | Informatik | `NULL` | Hauptfach |
| 2 | Datenbanken | `1` | Thema |
| 3 | Normalisierung | `2` | Unter-Thema |

> **Vorteil:** Wir können theoretisch unendlich tief schachteln, ohne extra Tabellen.

********************************************************************************

                         {{1-2}}
********************************************************************************

**2. `Cards` (Der Inhalt)**

Die eigentliche Lernkarte.

| Feld | Typ | Beschreibung |
| :--- | :--- | :--- |
| `Id` | **PK** | Eindeutige Kennung. |
| `DeckId` | **FK** | Zu welchem Deck gehört sie? |
| `Front` | TEXT | Frage (Text oder Base64-Bild). |
| `Back` | TEXT | Antwort (Text oder Base64-Bild). |

********************************************************************************

                         {{2-3}}
********************************************************************************

**3. `CardSmartScores` (Der Algorithmus)**

Metadaten für das "Smart Learning" (Gewichteter Zufall).

| Feld | Typ | Beschreibung |
| :--- | :--- | :--- |
| `Id` | **PK** | Eindeutige Kennung. |
| `CardId` | **FK** | Gehört zu genau einer Karte (1:1). |
| `BoxIndex` | INT | Leitner-Box (0-5). Bestimmt die Wahrscheinlichkeit (Niedrig = Oft). |
| `Score` | REAL | (Platzhalter für zukünftige Fein-Sortierung). |
| `LastReviewed` | DATE | Wann wurde die Karte zuletzt abgefragt? |

> **Warum getrennt?** Trennung von statischem Inhalt (`Cards`) und dynamischem Lernfortschritt.

********************************************************************************

                         {{3-4}}
********************************************************************************

**4. `LearningSessions` (Der temporäre Zustand)**

Speichert exakt, wo der Nutzer beim Lernen aufgehört hat.

| Feld                     | Typ    | Erklärung                                                                          |
| :---                     | :---   | :---                                                                               |
| `Id`                     | **PK** | Eindeutige ID der Sitzung.                                                         |
| `DeckId`                 | **FK** | Welches Fach wird gelernt?                                                         |
| `Scope`                  | ENUM   | **Umfang:** <br> 1. `MainOnly`: Nur Karten *direkt* in diesem Deck (z.B. im Thema "Allgemein"). <br> 2. `AllRecursive`: Das Deck **plus** alle Unterdecks (rekursiv). <br> 3. `CustomSelection`: Das Deck **plus** eine manuelle Auswahl an Unterdecks. |
| `SelectedDeckIdsJson`    | JSON   | Bei `Selection`: Welche Unterdecks genau?                                          |
| `Strategy`               | ENUM   | **Wie?** `Sequential` (A-Z), `Random` (Zufall), `Smart` (Algo).                    |
| `LastLearnedIndex`       | INT    | *Nur Sequential:* Zeiger auf letzte Karte (z.B. 5).                                |
| `LearnedCardIdsJson`     | JSON   | *Nur Random:* Liste gelernter IDs gegen Wiederholungen.                            |
| `LastAccessed`           | DATE   | Zeitstempel für "Zuletzt verwendet".                                               |

**Polymorphie des Fortschritts:**

*   **Sequential:** Nutzt `LastLearnedIndex` als Zeiger.
*   **Random:** Nutzt `LearnedCardIdsJson` als Ausschlussliste.
*   **Smart:** Nutzt die globale `CardSmartScores` Tabelle (kein Feld hier nötig).

********************************************************************************

---

## 4. Das Datenmodell (ER-Diagramm)

Hier sehen wir das bereinigte Schema im Zusammenhang.

``` sql   @dbdiagram
// 1. DIE STRUKTUR (Stapel & Themen)
Table Decks {
  Id integer [pk, increment]
  Name text [not null]
  ParentDeckId integer [note: 'FK: Parent Deck']
  IsDefault boolean [note: 'Standard-Ordner']
}

// 2. DER INHALT (Die Karten)
Table Cards {
  Id integer [pk, increment]
  Front text [not null, note: 'Inhalt: Markdown / Base64 Bild']
  Back text [not null]
  DeckId integer [not null, note: 'FK: Deck']
}

// 3. DER FORTSCHRITT (Smart Mode)
Table CardSmartScores {
  Id integer [pk, increment]
  CardId integer [not null, unique, note: 'FK: 1:1 zu Card']
  
  BoxIndex integer [note: '0-5. Hauptfaktor für Wahrscheinlichkeit']
  Score double [note: 'Sekundär (Statistik)']
  LastReviewed datetime
}

// 4. DER STATUS (Aktive Sitzung)
Table LearningSessions {
  Id integer [pk, increment]
  DeckId integer [not null, note: 'FK: Deck']
  
  // SCOPE (Was?)
  Scope integer [note: '0=MainOnly, 1=Recursive, 2=Selection']
  SelectedDeckIdsJson text [note: 'JSON Liste der SubDecks']
  
  // STRATEGY (Wie?)
  Strategy integer [note: '0=Sequential, 1=Random, 2=Smart']
  
  // STATE (Wo?)
  LastLearnedIndex integer [note: 'Merkzettel für Sequential Mode']
  LearnedCardIdsJson text [note: 'Merkzettel für Random Mode']
  
  LastAccessed datetime
}

Ref: Decks.ParentDeckId > Decks.Id [delete: cascade]
Ref: Decks.Id < Cards.DeckId [delete: cascade]
Ref: Cards.Id - CardSmartScores.CardId [delete: cascade]
Ref: Decks.Id < LearningSessions.DeckId [delete: cascade]
```

---

## 5. Zusammenfassung

1. **Frameworks:** Avalonia UI & EF Core.

2. **Struktur:** Rekursive Decks für maximale Flexibilität.

3. **Inhalt vs. Metadaten:** Trennung von `Cards` (Text/Bild) und `CardSmartScores` (Lernstand).

4. **Clean Architecture:** Wir haben alte Felder aus der `Decks`-Tabelle entfernt und nutzen nun sauber die `LearningSessions`-Tabelle für alle Zustände. Daten wurden per SQL-Skript migriert.

**Vielen Dank für die Aufmerksamkeit!**

---

## Backup: Normalisierung & Design-Entscheidungen

**Verletzt `LearningSessions` die 1. Normalform (Atomare Werte)?**

Ja, durch `SelectedDeckIdsJson` und `LearnedCardIdsJson`.

**Warum keine N:M Tabellen (`SessionDecks`, `SessionCards`)?**

1.  **Performance:** Diese Daten sind reiner "State" (temporärer Zustand). Sie werden beim Starten einer Session immer *komplett* benötigt. Ein JSON-Parse ist hier schneller als komplexe Joins über drei Tabellen.
2.  **Daten-Relevanz:** Es gibt keine analytischen Abfragen wie *"Zeige alle Sessions, die Deck X beinhalten"*. Die Daten haben keinen Wert außerhalb der laufenden Session.
3.  **Wartbarkeit:** Session löschen = 1 Zeile löschen. Keine verwaisten Einträge in Mapping-Tabellen (KISS-Prinzip).

**Fazit:**
Bewusster Trade-Off zwischen **akademischer Reinheit** und **Performance/Pragmatismus** für flüchtige Daten.
