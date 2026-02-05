# Vertriebs- und Release-Plan für CapyCard

Dieser Plan adressiert die Blockadeprobleme (SmartScreen, Gatekeeper) und rechtlichen Anforderungen für den Release von CapyCard.

## 1. Analyse der aktuellen Situation

- **Windows:** Der Installer (`.msi`) ist nicht signiert. Windows SmartScreen blockiert die Ausführung ("Unbekannter Herausgeber").
- **macOS:** Die App (`.dmg`/`.app`) ist nicht notarisiert. Gatekeeper blockiert die Ausführung. Der aktuelle Workaround (`xattr -cr`) ist für Endnutzer unzumutbar.
- **Android:** Der Build-Prozess nutzt bereits einen Keystore. Für den Google Play Store fehlen jedoch rechtliche Dokumente.

## 2. Lösungswege & Kosten

Es gibt zwei Hauptwege: Den kostenlosen "Open Source"-Weg (mit Einschränkungen) und den professionellen Weg (kostenpflichtig).

### Option A: Der "Professionelle" Weg (Empfohlen für reibungslose UX)

Damit Nutzer die App ohne Warnmeldungen installieren können, sind Zertifikate notwendig.

| Plattform | Anforderung | Kosten (ca.) |
| :--- | :--- | :--- |
| **Windows** | OV Code Signing Zertifikat (z.B. Sectigo, DigiCert) | ~80€ - 300€ / Jahr |
| **macOS** | Apple Developer Program (für Notarization) | 99$ (ca. 92€) / Jahr |
| **Android** | Google Play Console Account (einmalig) | 25$ (ca. 23€) einmalig |
| **Summe** | | **~170€ - 400€ / Jahr** |

### Option B: Der "Kostenlose" Weg (Status Quo + Verbesserung)

Wenn keine Kosten entstehen sollen, müssen die Nutzer mit Warnungen leben. Wir können aber die Dokumentation verbessern.

- **Windows:** Nutzer müssen "Trotzdem ausführen" klicken. Wir können dies auf der Download-Seite erklären.
- **macOS:** Ohne 99$/Jahr gibt es *keinen* Weg, die Gatekeeper-Warnung offiziell zu umgehen. Nutzer müssen den Rechtsklick-Trick ("Öffnen" im Kontextmenü) oder den Terminal-Befehl nutzen.
- **Android:** Release als `.apk` auf GitHub (Sideloading). Nutzer müssen "Installation aus unbekannten Quellen" zulassen.

## 3. Rechtliche Anforderungen (Play Store & Allgemein)

Da du aus Deutschland kommst, sind die Anforderungen strenger (TMG, DSGVO).

### Datenschutzerklärung (Privacy Policy)
**Zwingend erforderlich** für den Google Play Store und Apple App Store.
- **Inhalt:** Welche Daten werden gesammelt? (Auch wenn es *keine* sind, muss das dort stehen).
- **Ort:** Muss in der App verlinkt sein (z.B. in "Einstellungen" -> "Über") und im Store-Listing.
- **Lösung:** Eine einfache Markdown-Datei im Repo (`PRIVACY.md`) oder eine GitHub Pages Seite hosten.

### Impressum
**Zwingend erforderlich** nach § 5 TMG, da die App "geschäftsmäßig" (auf Dauer angelegt) angeboten wird, auch wenn sie kostenlos ist.
- **Inhalt:** Name, Anschrift, Kontakt (E-Mail).
- **Ort:** In der App leicht erreichbar (max. 2 Klicks).

## 4. Umsetzungsplan (Schritt-für-Schritt)

Wir fokussieren uns darauf, die App "Ready for Store" zu machen und die Prozesse zu dokumentieren.

### Phase 1: Rechtliche Grundlagen schaffen (Kostenlos)
1.  **Privacy Policy erstellen:**
    - Erstelle `docs/PRIVACY.md` (Vorlage für "Keine Datensammlung").
    - Veröffentliche dies via GitHub Pages (`https://<user>.github.io/CapyCard/privacy`).
2.  **Impressum erstellen:**
    - Erstelle `docs/IMPRINT.md`.
    - Veröffentliche dies via GitHub Pages.
3.  **App-Integration:**
    - Füge Links zu Privacy & Impressum in den "Über"-Dialog der App ein.

### Phase 2: Windows Signing (Entscheidung erforderlich)
*Falls Budget vorhanden:*
1.  Zertifikat kaufen (z.B. bei SignMyCode, Certum).
2.  Zertifikat (PFX) als GitHub Secret hinterlegen (`WINDOWS_PFX_BASE64`).
3.  `build-windows-msi.yml` anpassen: `azure-sign-tool` oder `signtool` nutzen.

*Falls kein Budget:*
1.  Dokumentation im Release-Text auf GitHub ergänzen: "Hinweis: Da dies ein Open-Source-Projekt ist, ist der Installer nicht signiert. Bestätigen Sie die Windows-Warnung mit 'Weitere Informationen' -> 'Trotzdem ausführen'."

### Phase 3: macOS Notarization (Entscheidung erforderlich)
*Falls 99$ Budget vorhanden:*
1.  Apple Developer Account erstellen.
2.  Zertifikate ("Developer ID Application") erstellen.
3.  `build-macos.sh` anpassen:
    - `codesign` mit der Developer ID ausführen.
    - `.dmg` erstellen.
    - `xcrun notarytool` ausführen, um das DMG an Apple zu senden.
    - Nach Erfolg: "Staple" Ticket an das DMG.

*Falls kein Budget:*
1.  Bestehenden Workaround (Text im DMG-Hintergrund) beibehalten.
2.  Dokumentation ergänzen.

### Phase 4: Google Play Store (25$ einmalig)
1.  Google Play Console Account anlegen.
2.  App-Eintrag erstellen (Texte, Screenshots).
3.  `PRIVACY.md` Link hinterlegen.
4.  Signed APK (aus GitHub Actions) hochladen.
5.  Interne Teststrecke starten.

## 5. Risiken

- **Rechtlich:** Ohne Impressum droht in DE Abmahngefahr (auch bei Open Source).
- **Vertrauen:** Nutzer installieren ungern unsignierte Software ("Ist das ein Virus?").
- **Technisch:** Apple verschärft Gatekeeper ständig. Der `xattr` Workaround könnte irgendwann nicht mehr funktionieren.
