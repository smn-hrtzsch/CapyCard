# Dieses Skript wendet Datenbank-Migrationen an und startet dann die Anwendung.
# Es ist die Lösung für das Problem, dass die Migration beim App-Start unter Windows fehlschlägt.

# Schritt 1: Alle ausstehenden Entity Framework-Migrationen anwenden.
# Dies stellt sicher, dass das Datenbankschema auf dem neuesten Stand ist.
Write-Host "Wende Datenbank-Migrationen an..."
dotnet ef database update

# Prüfen, ob der Migrationsbefehl erfolgreich war
if ($LASTEXITCODE -ne 0) {
    Write-Host "Datenbank-Migration fehlgeschlagen. Ausführung wird angehalten."
    # Pause, damit der Benutzer den Fehler sehen kann
    Read-Host "Drücke Enter, um das Fenster zu schließen"
    exit
}

Write-Host "Migrationen erfolgreich angewendet."
Write-Host ""
Write-Host "Anwendung wird gestartet..."

# Schritt 2: Die Anwendung starten.
dotnet run
