<!--

author:   Simon Hörtzsch
email:    simon.hoertzsch@student.tu-freiberg.de
version:  1.0.1
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
## 1. Tech Stack & Überblick

**Technologien:**

* **Datenbank:** SQLite (Lokal)
* **ORM (Object-Relational Mapping):** Entity Framework Core (EF Core)
* **Plattform:** .NET Multi-platform App UI (MAUI) / Avalonia

**Was bedeutet ORM?**
Ein ORM ist eine "Übersetzungsschicht" zwischen der objektorientierten Welt (C# Klassen) und der relationalen Datenbank (Tabellen).

* *Statt SQL:*

  `SELECT * FROM Cards WHERE DeckId = 5`

* *Schreiben wir C#:*

  `dbContext.Cards.Where(c => c.DeckId == 5).ToList()`

**Das Konzept der Migrationen:**

Migrationen sind wie eine "Versionsverwaltung" (Git) für das Datenbankschema.

* **Was machen sie?** Wenn wir im Code eine Klasse ändern (z.B. `Card` bekommt ein neues Feld `ImageUrl`), erstellt EF Core automatisch eine Migrations-Datei.
* **Wie funktionieren sie?** Jede Migration hat zwei Methoden:

  * `Up()`: Führt die Änderung durch (z.B. `CREATE COLUMN ImageUrl`).
  * `Down()`: Macht die Änderung rückgängig (z.B. `DROP COLUMN ImageUrl`).

* **Wofür zuständig?** Sie garantieren, dass die Datenbank auf allen Geräten (Entwickler-PC, Smartphone des Nutzers) exakt das gleiche Schema hat. Beim App-Start wird geprüft: "Welche Migration fehlt noch?" und diese dann angewendet.
* **Probleme:**
  
  * *Datenverlust:* Wenn man eine Spalte umbenennt, denkt die Datenbank oft, man will die alte löschen und eine neue erstellen -> Daten weg!
    *Lösung:* Manuelle Anpassung der Migration.
  * *Konflikte:* Wenn zwei Entwickler gleichzeitig Migrationen erstellen, passen die Zeitstempel nicht mehr zusammen.

**Ziel der Architektur:**
Speicherung von hierarchischen Kartenstapeln (Decks), Lernkarten und komplexen Lernzuständen auf mobilen Endgeräten und Desktops.

---

## 2. Die Tabellen im Detail

Bevor wir das Diagramm sehen, lernen wir die vier Hauptdarsteller kennen.

                         {{0-1}}
********************************************************************************

**1. `Decks` (Die Struktur)**
Organisiert alles in Fächer und Themen.

* **`Id` (PK):** Eindeutige Kennung.
* `Name`: Titel des Fachs (z.B. "Englisch").
* **`ParentDeckId` (FK):** Zeigt auf ein anderes Deck -> Ermöglicht Unterordner!

********************************************************************************

                         {{1-2}}
********************************************************************************

**2. `Cards` (Der Inhalt)**
Die eigentliche Lernkarte.

* **`Id` (PK):** Eindeutige Kennung.
* **`DeckId` (FK):** Zu welchem Deck gehört sie?
* `Front` / `Back`: Frage und Antwort (Text/Bild).

********************************************************************************

                         {{2-3}}
********************************************************************************

**3. `CardSmartScores` (Der Algorithmus)**
Das "Gehirn" der App. Speichert, wie gut man eine Karte kann.

* **`Id` (PK):** Eindeutige Kennung.
* **`CardId` (FK):** Gehört zu genau einer Karte.
* `BoxIndex`: Leitner-Box (0 = Neu, 5 = Gelernt).
* `Score`: Berechnete Priorität für die Abfrage.

********************************************************************************

                         {{3-4}}
********************************************************************************

**4. `LearningSessions` (Der temporäre Zustand)**
Das "Lesezeichen". Merkt sich, wo der Nutzer beim Lernen aufgehört hat.

* **`Id` (PK):** Eindeutige Kennung.
* **`DeckId` (FK):** Welches Fach wird gelernt?
* `Mode` & `OrderMode`: Was und Wie wird gelernt? (Enums).
* `LastLearnedIndex`: Wo waren wir stehengeblieben?

********************************************************************************

---

## 3. Das Datenmodell (ER-Diagramm)

Das Modell basiert auf einer zentralen `Decks`-Tabelle, an der alles hängt.

``` sql   @dbdiagram
// 1. DIE STRUKTUR (Stapel & Themen)
Table Decks {
  Id integer [pk, increment]
  Name text [not null]
  ParentDeckId integer [note: 'Verweist auf das Hauptfach. NULL = Dies ist ein Hauptfach']
  IsDefault boolean [note: 'True für den Ordner "Allgemein"']
  
  // Legacy / Hilfsfelder
  LastLearnedCardIndex integer
  LearnedShuffleCardIdsJson text
  IsRandomOrder boolean
}

// 2. DER INHALT (Die Karten)
Table Cards {
  Id integer [pk, increment]
  Front text [not null, note: 'Die Frage. Kann Markdown oder Base64-kodierte Bilder enthalten.']
  Back text [not null, note: 'Die Antwort. Kann Markdown oder Base64-kodierte Bilder enthalten.']
  DeckId integer [not null, note: 'Zu welchem Thema gehört die Karte?']
}

// 3. DER FORTSCHRITT (Metadaten)
Table CardSmartScores {
  Id integer [pk, increment]
  CardId integer [not null, unique, note: '1:1 Beziehung zu Cards']
  Score double [note: 'Berechnete Priorität. Niedriger = Wichtiger']
  BoxIndex integer [note: 'Leitner-Box (0-5)']
  LastReviewed datetime [note: 'Wann zuletzt gelernt?']
}

// 4. DER STATUS (Laufsitzungen)
Table LearningSessions {
  Id integer [pk, increment]
  DeckId integer [not null]
  
  // 1. WAS lernen wir? (Scope)
  Mode integer [note: 'Scope: 0=Nur Deck, 1=Rekursiv, 2=Auswahl']
  SelectedDeckIdsJson text [note: 'Nur bei Mode 2: Welche Unterdecks?']
  
  // 2. WIE lernen wir? (Strategy)
  OrderMode integer [note: 'Strategy: 0=Sortiert, 1=Zufall, 2=Smart']
  
  // 3. WO sind wir? (Fortschritt je nach Strategy)
  LastLearnedIndex integer [note: 'Nur bei Sortiert: Index-Pointer']
  LearnedCardIdsJson text [note: 'Nur bei Zufall: Liste erledigter IDs']
  // Smart-Mode nutzt stattdessen die CardSmartScores Tabelle!
  
  LastAccessed datetime
}

// BEZIEHUNGEN
// Ein Deck kann ein ParentDeck haben (Rekursion)
Ref: Decks.ParentDeckId > Decks.Id [delete: cascade]

// Ein Deck hat viele Karten (1:N)
Ref: Decks.Id < Cards.DeckId [delete: cascade]

// Eine Karte hat genau einen SmartScore (1:1)
Ref: Cards.Id - CardSmartScores.CardId [delete: cascade]

// Ein Deck kann Teil von vielen Sessions sein (1:N)
Ref: Decks.Id < LearningSessions.DeckId [delete: cascade]
```

---

## 4. Detail: Die Deck-Hierarchie

                         {{0-1}}
********************************************************************************

**Problemstellung:**
Wir wollen "Hauptfächer" (z.B. *Mathe*) und darin "Themen" (z.B. *Analysis*, *Geometrie*) abbilden.

**Lösung: Self-Referencing Table**
Wir nutzen nur **eine** Tabelle `Decks` für beides. Der Unterschied liegt im `ParentDeckId`.

| Id | Name | ParentDeckId | Bedeutung |
| :--- | :--- | :--- | :--- |
| **1** | Mathe | `NULL` | **Hauptfach** (Root) |
| **2** | Englisch | `NULL` | **Hauptfach** (Root) |
| **3** | Analysis | `1` | **Thema** (gehört zu Mathe) |
| **4** | Geometrie | `1` | **Thema** (gehört zu Mathe) |
| **5** | Vokabeln | `2` | **Thema** (gehört zu Englisch) |

> **Architektur-Notiz:**
> Die Datenbank erlaubt durch diese Struktur theoretisch unendlich viele Unterebenen. Die App beschränkt dies aktuell logisch auf 2 Ebenen (Fach -> Thema), bleibt aber zukunftssicher.

********************************************************************************

                         {{1-2}}
********************************************************************************

**Relation: 1:N (Rekursiv)**

* **Eins** Hauptdeck kann **Viele** Unterdecks (Themen) haben.
* Ein Unterdeck gehört zu genau **Einem** Hauptdeck.

**Lösch-Weitergabe (Cascade Delete):**
Löscht der Nutzer das Fach "Mathe" (Id 1), erkennt die Datenbank die Abhängigkeit und löscht automatisch "Analysis" (Id 3) und "Geometrie" (Id 4) mit.

********************************************************************************

---

## 5. Detail: Karte & Lernfortschritt

                         {{0-1}}
********************************************************************************

**Tabelle `Cards` (Inhalt)**

Hier stehen die statischen Daten.

* **Relation:** 1 Deck : N Karten.
* `Front` / `Back`: Speichert Text (Markdown) oder **Base64-kodierte Bilddaten**.

********************************************************************************

                         {{1-2}}
********************************************************************************

**Tabelle `CardSmartScores` (Metadaten)**

Hier stehen die dynamischen Lerndaten. Wir trennen Inhalt (`Cards`) vom Fortschritt (`Scores`).

* **Relation:** 1 Karte : 1 Score (One-to-One).
* **Warum getrennt?** Wenn wir den Algorithmus ändern oder den Fortschritt zurücksetzen wollen, müssen wir die eigentlichen Karteninhalte nicht anfassen.

**Die Attribute:**

| Attribut | Erklärung |
| :--- | :--- |
| `BoxIndex` | Zahl **0 bis 5**. Entspricht den Fächern im klassischen Karteikasten (Leitner-System). 0 = Neu/Vergessen, 5 = Langzeitgedächtnis. |
| `Score` | Eine `double` Zahl. Wird berechnet aus BoxIndex + vergangener Zeit. Dient als **Sortierschlüssel** für die Lern-Queue. |
| `LastReviewed` | Zeitstempel. Dient als "Tie-Breaker", wenn zwei Karten den gleichen Score haben (die ältere wird zuerst gezeigt). |

********************************************************************************

---

## 6. Detail: Learning Sessions

                         {{0-1}}
********************************************************************************

**Das Konzept: Scope vs. Strategy**

Die Tabelle `LearningSessions` steuert zwei Dinge:

1. **Scope (Was?):** Das Feld `Mode` bestimmt den Umfang.
  
    * *MainOnly:* Nur Karten aus dem gewählten Deck.
    * *AllRecursive:* Deck + alle Unterdecks.
    * *CustomSelection:* Deck + manuell gewählte Unterdecks (gespeichert in `SelectedDeckIdsJson`).

2. **Strategy (Wie?):** Das Feld `OrderMode` bestimmt die Reihenfolge.

    * *Sequential:* Der Reihe nach (Index 1, 2, 3...).
    * *Random:* Zufällig gemischt.
    * *Smart:* Algorithmus-basiert.

********************************************************************************

                         {{1-2}}
********************************************************************************

**Fortschrittsspeicherung (State Management)**

Je nach `OrderMode` wird der Fortschritt unterschiedlich gespeichert:

| Modus | Speicherort | Funktionsweise |
| :--- | :--- | :--- |
| **Sequential** | `LastLearnedIndex` | Ein einfacher Integer-Zeiger (z.B. "Karte 5"). Beim Beenden wird nur diese Zahl gespeichert. |
| **Random** | `LearnedCardIdsJson` | Eine Liste erledigter IDs (z.B. `[4, 12, 9]`). Verhindert, dass Karten doppelt kommen, bis alle einmal gezeigt wurden. |
| **Smart** | `CardSmartScores` (Tabelle) | Nutzt **kein** Feld in der Session! Der Fortschritt ist global in den SmartScores gespeichert. Die Session merkt sich nur, dass wir im "Smart Mode" waren. |

********************************************************************************

                         {{2-3}}
********************************************************************************

**Warum JSON für Listen?**

`LearnedCardIdsJson`: `"[102, 55, 9, 12]"`

* **Performanz:** Ein einziger String-Read ist schneller als ein Join über eine N:M-Verknüpfungstabelle für temporäre Daten.
* **Einfachheit:** Wenn die Session gelöscht/zurückgesetzt wird, muss nur dieses eine Feld geleert werden. Keine verwaisten Einträge in Hilfstabellen.

********************************************************************************

---

## 7. Zusammenfassung

1. **Hierarchie:** Gelöst über `ParentDeckId` in einer einzigen Tabelle (Self-Referencing). Flexibel für die Zukunft.
2. **Daten-Trennung:** Inhalt (`Cards`) und Lernstatus (`CardSmartScores`) sind getrennt (Separation of Concerns).
3. **Performance:** Metadaten wie `Score` sind optimiert für schnelle Sortierung der Abfrage-Warteschlange.
4. **Pragmatismus:** Temporäre Zustände (`Sessions`) nutzen JSON statt komplexer Relationen.

**Vielen Dank für die Aufmerksamkeit!**
