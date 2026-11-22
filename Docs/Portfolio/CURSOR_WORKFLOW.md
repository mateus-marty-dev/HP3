# HP.3 Portfolio mit Cursor und ChatGPT

Cursor erstellt auf Auftrag Session- und Wochenlog-Entwürfe aus tatsächlich geprüften Projektquellen und bereitet ihre Übernahme in GitHub vor. ChatGPT unterstützt die Reflexion zu Raumgestaltung, Spielerführung, Atmosphäre und Iterationen. Der Nutzer prüft, welche Reflexion und welche Medien ins Repository dürfen.

## Bestehende Ablage

- [Portfolio-README](README.md) für manuelle Sessions und Fallstudien, [Index](INDEX.md) als gemeinsamer Einstieg und [Session-Vorlage](TEMPLATE.md) für neue Einträge.
- [Wochenverzeichnis](Weekly_Logs/README.md) und [Wochenvorlage](Weekly_Logs/TEMPLATE.md) für Zusammenfassungen der bestehenden Sonntagsaufgabe.
- [Vorbereitete Automationsübergabe](Weekly_Logs/AUTOMATION_HANDOFF.md) als bisheriger Einrichtungsstand. Für diesen Cursor-Ablauf genügt die manuelle Übergabe des bestehenden Sonntagsentwurfs. Der vorbereitete Zusatzauftrag muss dafür nicht aktiviert werden.

## Session erstellen

1. Bei `<session_S>` Ausgangslage, Ziel, Erfolgskriterium und mögliche Vergleichsaufnahmen festhalten. Bei `<session_E>` tatsächliches Ergebnis, offene Punkte, Nachweise und Portfolio-Potenzial besprechen. Beide Marker funktionieren unabhängig von Gross- und Kleinschreibung. Bis zum ausdrücklichen Schreibauftrag bleibt die Auswertung im Chat.
2. Vor einem neuen Eintrag README und vorhandene Sessions prüfen, auch lokale noch nicht eingecheckte Einträge. Wenn bereits eine passende Session existiert, darauf verweisen und Ergänzungen im Chat vorschlagen. Manuelle Sessions können jederzeit nach der bestehenden Vorlage ergänzt werden.
3. Auf Auftrag aus der Session-Vorlage eine neue Datei `YYYY-MM-DD-kurzthema.md` anlegen. Bei einem anderen Thema mit Namenskollision einen eindeutigen Zusatz wählen. Einen neuen Eintrag gemäss Portfolio-README verlinken, sobald der Nutzer den konkreten README-Zusatz ausdrücklich freigegeben hat. Bestehende Einträge und laufende Änderungen an der Übersicht dabei erhalten.

## Wochenlog aus dem Sonntagsentwurf

1. Den Entwurf der bestehenden ChatGPT-Sonntagsaufgabe und die freigegebenen Designnotizen in den Cursor-Chat übernehmen. Für Designnotizen genügen Datum und Session, gestalterisches Problem, Alternativen, begründete Entscheidung, beobachtete oder erwartete Wirkung und offene Frage. Private Chatdetails vor der Übergabe entfernen.
2. Die Wochenvorlage kopieren. Montag bis zum ausgewiesenen Sonntagsschluss des Entwurfs in Europe/Zurich als Berichtszeitraum angeben, mit tatsächlichem Erstellungszeitpunkt und Abdeckungsgrenzen. Entwicklungen nach dem Stichtag im Folgebericht mit ihrem tatsächlichen Datum aufnehmen. Ältere Arbeit als Kontext kennzeichnen, auch wenn sie erst diese Woche dokumentiert wurde.
3. Die Herkunft als ChatGPT-Sonntagsentwurf mit Cursor-Quellenprüfung ausweisen. Technische Ergebnisse und Teststand in **Neue belegte Ergebnisse** eintragen. Freigegebene gestalterische Entscheidungen und deren Wirkung unter **Designentscheidungen und Iterationen** beschreiben. Wenn der Entwurf oder Quellen fehlen, die Lücke benennen und die vorhandenen Notizen im Chat zur Prüfung vorlegen. Einen Wochenbericht gemäss Ablageregel aus der bestehenden Sonntagszusammenfassung ableiten.
4. Vor einem ausdrücklichen Speicherauftrag den nach Sonntag benannten Zielpfad `Weekly_Logs/YYYY-MM-DD-portfolio-erfolgslog.md` auf dem aktuellen `main`, lokal und in bekannten offenen Berichts-PRs prüfen. Existiert er bereits, darauf verweisen. Eine ausdrücklich beauftragte Ergänzung erhält nach der Konvention des Wochenverzeichnisses einen eindeutigen Namen und einen Link zum ursprünglichen Bericht.
5. Neue Session- und Wochenlogs ohne Überschreiben anlegen, etwa mit `FileMode.CreateNew` in PowerShell oder einer GitHub-Operation zum Erstellen einer neuen Datei. Bei einer Kollision den vorhandenen Bericht erhalten. Aus Wochenlogs auf vorhandene Sessions mit `../DATEI.md` und auf Medien mit `../media/…` verweisen. Das Wochenverzeichnis dient als Berichtsliste.

## Quellen und Nachweise prüfen

- Arbeitsstand mit `git status --short`, `git diff` und `git diff --cached` erfassen. Relevante unversionierte Dateien gezielt lesen und als lokal kennzeichnen. Relevante Commits im Berichtszeitraum mit `git log` ermitteln und mit `git show <SHA>` prüfen. Branch, Commit oder lokalen Stand, Pfade und ggf. PR-Links nennen. Bei fehlendem Zugriff den tatsächlich geprüften Stand angeben.
- Für Unity-Skripte, Szenen, Prefabs und Blender-Dateien den sichtbaren Änderungs- oder Integrationsstand beschreiben. Ein geändertes Skript belegt Codearbeit, ein Szenendiff einen gespeicherten Szenenstand. Eigenschaften einer binären Datei nur aus einer tatsächlich geprüften Ansicht oder einer zugeordneten Quelle ableiten.
- Pro Ergebnis den Teststand ausweisen. Für einen belegten Test Szene oder Aufbau, geprüften Stand, Testschritte, erwartetes und beobachtetes Ergebnis, Datum und Quelle festhalten, soweit verfügbar. Beobachtungen aus einer manuellen Session als solche zuschreiben. Fehlgeschlagene Versuche mit Befund dokumentieren. Ohne Nachweis **Test offen** oder **Testergebnis unbestätigt** eintragen. Beispiel: «Pickup-Konfiguration geändert (Szenendiff); Aufnahme im Play Mode noch offen.»
- Bilder und Clips unter `media/YYYY-MM-DD-kurzthema/` ablegen, wenn der Nutzer die Aufnahme zur Übernahme freigibt. Vorhandene Medien an ihrem Ort belassen. Prüfen, dass der Link im PR-Stand auflösbar ist. Werkzeug, Szene oder Ansicht und sichtbaren Zustand beschriften. Ein Unity-Inspector-Bild kann eine Konfiguration zeigen, ein Gameplay-Clip den gezeigten Ablauf. Bei Vorher-Nachher-Vergleichen möglichst dieselbe Ansicht verwenden. Fehlende oder nicht geprüfte Medien konkret als offen markieren.
- Bei Designreflexion Entscheidung und Urheberquelle mit Datum festhalten. Erwartete Wirkung, eigene Beobachtung und Playtest-Feedback auseinanderhalten. Ältere Ideen und ungeprüfte ChatGPT-Vorschläge entsprechend kennzeichnen. Ohne neue belegte Entwicklung die Abdeckungsgrenze festhalten.

## Pull Request zur Prüfung

1. Vom bestätigten GitHub-`main` einen separaten Branch anlegen. Bei einem laufenden lokalen Arbeitsstand ein separates Arbeitsverzeichnis oder einen GitHub-Branch verwenden. Lokale Unity-, Blender- und Portfolio-Entwürfe nur bei ausdrücklicher Freigabe aufnehmen.
2. Nur die freigegebenen Dateien übernehmen. Für ein Wochenlog umfasst der PR die neue Berichtsdatei. Für eine neue Session sind freigegebene neue Medien und ein ausdrücklich beauftragter README-Link möglich. Referenzen auf unveröffentlichte Dateien vor der Übernahme auflösen oder die Quelle als lokal und für PR-Leser unzugänglich kennzeichnen.
3. Vor dem Push die Dateiliste und den vollständigen Diff gegen die PR-Basis prüfen, beispielsweise mit `git diff --name-status origin/main...HEAD` und `git diff --check origin/main...HEAD` bei aktuellem `origin/main`. Links, Datenschutz und Nachweisgrenzen prüfen. Nach dem Push bestätigen, dass der Remote-Branch die beabsichtigten Commits enthält.
4. Einen Pull Request gegen `main` öffnen und auf GitHub Basis, Branch und Dateiliste prüfen. Im PR Zeitraum oder Session, Quellen, Dokumentationsprüfung und offene Tests nennen. Den tatsächlichen Übertragungsstand melden. Die Prüfung und ein etwaiger Merge erfolgen durch den Nutzer.

## Einmalige Nutzung

Das HP.3-Repository in Cursor öffnen und in den Projektregeln prüfen, ob `hp3-portfolio` geladen wird. Bei fehlendem Zugriff auf das Repository zuerst die GitHub-Anmeldung prüfen. Zum Wochenabschluss den freigegebenen Sonntagsentwurf manuell an Cursor übergeben. Dafür ist keine technische Verbindung zwischen den Chats und Cursor eingerichtet.

Beispielauftrag für Cursor:

> Prüfe die beigefügte Sonntagszusammenfassung und die freigegebenen Designnotizen anhand des HP.3-Leitfadens. Zeige mir den Wochenlog-Entwurf mit Quellen, Teststand und fehlenden Medien im Chat. Warte mit dem Anlegen der Datei und dem Pull Request auf meinen Auftrag.
