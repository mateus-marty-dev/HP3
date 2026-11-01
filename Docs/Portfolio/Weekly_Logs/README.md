# Wöchentliche Portfolio-Erfolgslogs

[Gemeinsamer Index](../INDEX.md) · [Manuelle Portfolio-Sessions](../README.md) · [Session-Vorlage](../TEMPLATE.md)

## Zweck und Status

Dieser Bereich ist ausschliesslich für wöchentliche Zusammenfassungen der bestehenden sonntäglichen ChatGPT-Aufgabe vorgesehen. Manuelle Sessions bleiben am bisherigen Ort und frei bearbeitbar. Es wird keine zweite Sonntagsaufgabe eingerichtet.

Stand 29.10.2026: Ablage vorbereitet; noch keine Wochenberichte vorhanden. Die bestehende Aufgabe ist aus dem referenzierten Chat bekannt, ihr aktueller Auftrag und Zeitplan wurden hier nicht direkt ausgelesen. Dort genannt: Sonntag gegen 20 Uhr; vor einer Anpassung Europe/Zurich und die genaue Uhrzeit prüfen. GitHub-Push für die Aufgabe ist noch nicht eingerichtet.

## Benennung und Berichtszeitraum

- Neue Berichte nach dem Sonntagsdatum benennen: `YYYY-MM-DD-portfolio-erfolgslog.md`.
- Zeitraum: Montag bis zum ausgewiesenen Erstellungszeitpunkt am Sonntag, Zeitzone Europe/Zurich. Den Sonntagabend nach Erstellung als noch nicht erfasst kennzeichnen; spätere Entwicklungen im Folgebericht mit ihrem tatsächlichen Datum nachtragen.
- Beispiel für den nächsten vorgesehenen Lauf: `2026-11-01-portfolio-erfolgslog.md`, Zeitraum 26.10.–01.11.2026 bis zum Erstellungszeitpunkt. Das ist ein Namensbeispiel, kein bereits erzeugter Bericht.
- Vor dem Speichern prüfen, ob der Zielpfad existiert. Beim tatsächlichen Schreiben eine Funktion verwenden, die nur neue Dateien anlegt und bei vorhandenem Ziel abbricht (z. B. FileMode.CreateNew). Kein Ersetzen, Anhängen oder Zusammenführen.
- Existiert ein Bericht bereits, bleibt er unverändert. Eine ausdrücklich beauftragte Ergänzung bekommt einen neuen Namen wie `YYYY-MM-DD-portfolio-erfolgslog-ergaenzung-01.md` und verweist auf den vorherigen Bericht.
- Neue Berichte erscheinen in der Dateiliste dieses Verzeichnisses. Für den aktuellsten Bericht nach Dateiname absteigend sortieren; Ergänzungen über ihre internen Verweise zuordnen. Kein automatisch umgeschriebener Index erforderlich.

## Inhalt und Belege

Die [Wochenvorlage](TEMPLATE.md) liefert die Gliederung. Sie ist von der manuellen Session-Vorlage getrennt.

1. Tatsächlich gelesene Quellen und Abdeckungsgrenzen benennen: Sessions, Chats, Dateien und gegebenenfalls Commit-Links.
2. Neue Ergebnisse der Berichtswoche von älterem Kontext unterscheiden. Das Verschieben eines alten Chats ist kein neuer Designfortschritt.
3. Pläne, dokumentierte Umsetzung und tatsächlich belegte Tests getrennt ausweisen. Fehlende Bildnachweise oder unzugängliche Chats ausdrücklich kennzeichnen.
4. Technische Entwicklung und gestalterische Kompetenzen getrennt beschreiben; eine Spezialisierung nur bei ausdrücklichem Nachweis nennen.
5. Auch verworfene Versuche und Erkenntnisse aus Fehlern aufnehmen. Ohne neue belegbare Entwicklung genügt ein kurzer Bericht über die Abdeckungslücke beziehungsweise den belegten Stillstand.

Aus diesem Verzeichnis verweisen manuelle Sessions auf `../YYYY-MM-DD-kurzthema.md`, vorhandene Medien auf `../media/...`. Nur wirklich vorhandene Dateien verlinken. Medien bleiben unter `Docs/Portfolio/media/`; die Automatik verändert keine bestehenden Medien.

## Sicherer Übergang nach GitHub

Aktuelles Ziel ist das öffentliche Repository [mateus-marty-dev/HP3](https://github.com/mateus-marty-dev/HP3). Die lokale Projektbezeichnung HP_3 unterscheidet sich vom Repository-Namen HP3.

Bis eine Übertragung ausdrücklich vereinbart und eingerichtet ist, liefert die bestehende Aufgabe den Bericht als Markdown zur manuellen Prüfung und Übernahme. Private Chat-Inhalte vor Veröffentlichung auf ihre Eignung prüfen.

Für eine spätere automatische Übernahme ist ein separater Branch mit Pull Request zur Prüfung vorgesehen. Er darf ausschliesslich eine neue Berichtsdatei in diesem Verzeichnis hinzufügen. Vorhandene Dateien, manuelle Sessions, Vorlagen, README-Dateien und Medien werden nicht geändert. Kein automatisches Zusammenführen oder Force-Push.

Eine zusätzliche GitHub-Automatisierung wird erst nach Festlegung von Übergabeweg, Zugriff, Zielbranch und Veröffentlichung mit dem Nutzer eingerichtet. Ein Zeitplan allein stellt noch keine Verbindung zur bestehenden ChatGPT-Aufgabe her. Es werden keine Zugangsdaten im Repository abgelegt.

Den vorbereiteten Auftrag und die offenen Entscheidungen enthält die [Übergabe zur Sonntagsaufgabe](AUTOMATION_HANDOFF.md).
