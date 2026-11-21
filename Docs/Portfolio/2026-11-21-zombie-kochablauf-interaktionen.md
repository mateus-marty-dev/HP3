# 2026-11-21 – Zombie-Kochablauf: Greifen, NavMesh und Herd als Interaktionssystem

- **Projekt:** HP.3 – *Petrusilius’ Schloss*
- **Bereich:** Level Design, Environment & World Building, NPC-Gameplay, Unity-Physik und Navigation
- **Werkzeuge:** Unity 6 (C#, NavMeshAgent / AI Navigation, Animator, Rigidbody, Collider), Visual Studio, Console, Git und Chat-basierte Fehlersuche
- **Status:** **Iterativer Playtest / technisch noch nicht abgeschlossen**
- **Zeit:** Keine vollständig verlässliche Start-/Endzeit dokumentiert; die hier ausgewerteten Screenshots und Tests stammen vom **20.–21.11.2026**.
- **Integrationsstand:** In der lokalen Unity-Szene wurden Teilabläufe erfolgreich getestet, gleichzeitig gab es Regressionen bei der Pfannenaufnahme und Herdnavigation. Dieser Portfolio-Eintrag synchronisiert **keine Gameplay-Scripts oder Szenendaten** nach GitHub.
- **Bezug:** [Zombie und Wendeltreppe (15.11.)](2026-11-15-zombie-wendeltreppe-navmesh.md) · [Vom Garten zur Tomatensuppe (04.11.)](2026-11-04-garten-zur-tomatensuppe.md)

## Session_S – Ausgangslage und Ziel

Im Schloss wird der Zombie zum tollpatschigen Gehilfen Petrusilius’. Vorhanden war eine Interaktionskette aus vier Tomaten ernten, in einem beweglichen Korb transportieren, durch die Schlosstür und die enge Wendeltreppe zur Küche gehen sowie Korb abstellen. Der Transport über die Treppe war bereits in einer vorherigen Session debuggt und als funktionierend bestätigt worden.

**Session-Ziel:** Den Ablauf zu einer nachvollziehbaren Kochhandlung ausbauen. Der Zombie soll den Korb ausleeren, die Tomaten vom Boden aufnehmen, in die Pfanne legen, gegebenenfalls Wasser am Teich holen, das Herdfeuer entzünden und die Pfanne auf der Kochfläche absetzen. Eine vorbereitete Pfanne (genügend Wasser und vier Tomaten) soll einen direkten Einstieg bei der Feuerinteraktion ermöglichen.

**Erfolgskriterium:** Der NPC kann die Stationen autonom und plausibel verbinden, ohne Gegenstände physikalisch wegzustossen, auf den Herd zu laufen oder beim ersten unerreichbaren Griffpunkt endgültig abzubrechen.

## Arbeitsschritte und Iterationen

### 1. Tomaten aus dem Korb – Physik und Zustandsprüfung

Nach dem Ausschütten rollten die Tomaten teils lange über den Küchenboden. Die Küchenroutine wartete zunächst nur begrenzt auf eine stabile Position und konnte bei Zeitüberschreitung abbrechen. Ein Abbremsen mittels Rigidbody-Dämpfung und das Erkennen einer kurzen Ruhephase wurden eingeführt. Die Arbeit zeigte: **Physikalisches Stillliegen** und **erfolgreich greifbare Position** sind zwei getrennte Zustände.

In einem Test blieben mehrere Tomaten unerreicht; die Console meldete beispielsweise:
```text
[Zombie Kuechentomaten] Nur 2/4 Tomaten in der Pfanne.
Ripe_Tomato_4: Bodenobjekt nicht greifbar – Handabstand nicht gemessen;
Standpositionen getestet: 0, NavMesh 0, Etage 0, Pfade 0
```
Dies bedeutet **nicht**, dass die Hand zwingend zu kurz ist. Die Suche fand für diese Tomate nicht einmal einen zulässigen Kandidaten. Daraufhin wurden die Standortsuche und später zeitlich wiederkehrende Prüfungen erprobt, damit der Spieler eine fehlende Tomate versetzen kann, ohne den gesamten Auftrag neu zu starten. Der vollständige Vier-Tomaten-Durchlauf wurde in dieser Session **nicht durchgehend zuverlässig bestätigt**.

### 2. Isolierter Einzelfall: eine Tomate aufnehmen

Um die Kopplung von Navigation, Bücken, Handbewegung und Objektphysik getrennt prüfen zu können, entstand ein **separater Test `ZombieTomatoPickup.cs`**. Dieser wird in der Szene per **F** in Reichweite von Petrusilius gestartet, während der normale `ZombieBehaviour`-Component für den Einzeltest deaktiviert ist.

Die ersten Versionen zogen die Tomate scheinbar magnetisch zur Hand oder verweigerten den Griff wegen kleiner Abstände (beispielsweise **0,27 m Handabstand** gegenüber **0,22 m Toleranz**). Anschliessend wurde die Armbewegung mit der Tomatenbewegung verbunden. Ein weiteres Problem: Ein starker Wurf konnte den beweglichen Korb wegschleudern. Die kontrollierte Lösung führte die Tomate **über die Korböffnung** und liess sie **ohne Wurfimpuls** hineinfallen.

**Belegter Zwischenstand:** Der Nutzer bestätigte für den Einzeltest ausdrücklich: **„Perfekt“**. Die erprobte Greif- und sanfte Ablagelogik wurde anschliessend in die grössere Küchenroutine zurückübertragen. Das Wiederverwenden der Einzeltestlogik bei *mehreren* Tomaten erforderte zusätzliche Iterationen.

### 3. Bedingte Aufträge: vorbereitete Pfanne erkennen

Für eine bereits vorbereitete Pfanne sollte der Zombie keine unnötigen Wege ausführen. Dafür wurden die Daten des vorhandenen `RainCollector` (`FillLevel`) und die Fruchterkennung der `BucketContents`-Logik herangezogen. Der frühe Startcheck wurde verbessert, weil `BucketContents.ContainsBody()` bestimmte Rigidbody-Zustände, etwa kinematische Früchte, nicht mitzählt.

Eine Console-Meldung bestätigte die Unterscheidung von Wasserstand und Zutatenzahl, z. B. **„Wasser 100 %, Tomaten in Pfanne 0/4: normaler Auftrag“**. Andere Tests gelangten über den vorbereiteten Zustand direkt zur Herdinteraktion. **Entscheidend ist die tatsächliche Inhaltsprüfung, nicht allein der sichtbare Wasserstand.**

### 4. Herd anzünden – Zielpunkte und Navigation

Das bestehende `StoveFireInteractable.Interact()` kann bereits Flammen, Licht, Klang und das Verkohlen des Brennholzes steuern. Für den Zombie wurden getrennte Zielpunkte eingeführt:

- **`Zombie_StoveStandPoint`:** Standpunkt des Zombies **auf dem Küchenboden vor dem Herd**.
- **`StovePotPlacePoint`:** Position, an der der **Topfboden auf der Herdplatte** aufliegen soll.

Zunächst waren die Zielpunkte im Inspector vertauscht. Dadurch konnte der Zombie auf der Herdoberseite landen. Auch spätere automatische NavMesh-Suchen hielten Abstände ein, prüften aber teilweise nicht zuverlässig, **ob die Position innerhalb des Herdkorpus lag**. In einem Test war das Feuer an und die Pfanne korrekt oben, aber **der Zombie stand selbst auf dem Herd** – ein funktionaler Teilerfolg mit sichtbar ungültigem NPC-Verhalten.

Mit zusätzlichen Sicherheitsbedingungen sollte die Herdoberfläche von erlaubten NPC-Standplätzen ausgeschlossen werden. Allerdings entstand dadurch eine Regression: Der Zombie stand sichtbar vor dem Feuer, die Console meldete rund **1,01 m Abstand bei 2,20 m Reichweite**, dennoch wurde die Feuerinteraktion blockiert. Die Sicherheitsbedingungen mussten danach für *Feuer bedienen* und *Pfanne abstellen* getrennt betrachtet werden.

**Level-Design-Lektion:** Ein Objektziel und eine begehbare Standfläche sind nicht dasselbe. Kollisionsgeometrie, NavMesh und Greifdistanz müssen aufeinander abgestimmt sein. Die Herdoberfläche muss in der gebackenen Navigation ausgeschlossen beziehungsweise als **Not Walkable** markiert werden, ohne den Küchenboden davor zu sperren.

### 5. Kochtopf aufnehmen und auf der Herdplatte absetzen

Das Aufnehmen der Pfanne hatte beim früheren **Wasserholen am Teich** bereits funktioniert. Nach Erweiterung um den Herdbesuch war der Ausgangspunkt anders: Der Zombie stand zunächst am Feuer und suchte **von dort aus** einen Griffstandplatz bei der Pfanne.

Laufzeitdiagnosen der Pfannenaufnahme zeigten:
```text
[Zombie Pot Diagnose]
Kandidaten: 384 | NavMesh: 0 | Stockwerk: 0
Freier Abstand: 0 | Vollstaendige Pfade: 0
```
An anderer Stelle war die Pfanne erreichbar, aber der Abstand der beiden Hände zu den Griffen betrug rund **0,30–0,33 m**. Es wurden mehrere alternative Standplätze und vorsichtig tolerantere Greifversuche erprobt. Die spätere flexible Ablage prüft die Zielposition des Topfs, bevor er losgelassen wird. **Die robuste Gesamtfunktion blieb dabei offen**: In einem Playtest stand der Topf auf der Herdplatte, aber die NPC-Standposition war falsch.

## Entscheidungen und technische Erkenntnisse

| Thema | Erkenntnis / Entscheidung |
|---|---|
| **Einzeltest statt Vollauftrag** | Greifen und sanftes Ablegen zunächst isoliert mit `ZombieTomatoPickup.cs` nachweisen. |
| **Navigation vor Greifdistanz** | Bei `NavMesh: 0` helfen zusätzliche Arm-Toleranzen allein nicht. Ursache in NavMesh-Sampling, Etagenbezug oder unpassender Standfläche suchen. |
| **Physik beim Ablegen** | Gegenstände über die Öffnung führen und mit möglichst geringem Impuls freigeben statt in den Behälter zu schleudern. |
| **Begehbarkeit vs. Interaktionsziel** | `Zombie_StoveStandPoint` und `StovePotPlacePoint` trennen; Herdplatten sind keine NPC-Standplätze. |
| **Sicherheit und Regressionen** | Strengere Einschränkungen verhindern einen Fehler, können aber vorher funktionierende Interaktionen blockieren. |
| **Datengetriebener Auftrag** | Früh prüfen, welche Schritte durch bereits vorhandenes Wasser und Tomaten übersprungen werden dürfen. |
| **Wiederholung / Spielerhilfe** | Unerreichbare Tomaten später erneut prüfen; dadurch entsteht ein flexiblerer Weltzustand statt eines einmaligen Skriptdurchlaufs. |

## Architektur-Rückblick: mehr als 2'000 Zeilen Küchenroutine

Die fortlaufende Integration brachte die `ZombieKitchenRoutine.cs` laut Entwicklungsstand auf **über 2'000 Zeilen**. Damit wuchs das Risiko, mit einer Korrektur an der Pfanne versehentlich Feuer, Korb oder Navigation zu beeinflussen.

**Nächster Architektur-Schritt, noch nicht umgesetzt:** Den Code zunächst **verhaltensneutral** in mehrere `partial class ZombieBehaviour`-Dateien aufteilen: Auftragskoordination, Navigation / Standplatzsuche, Tomatengreifen, Pfannentransport und Herdinteraktion. Später sollen daraus wiederverwendbare NPC-Fähigkeiten mit gemeinsamen Fehler- und Wiederholungsmechanismen entstehen. Neue Aufträge könnten dann Fähigkeiten kombinieren, statt neue monolithische Routinen zu duplizieren.

## Screenshots und Medienbelege

Die folgenden vier **Original-Screenshots aus dem Playtest** wurden für die Portfolio-Auswahl gesichtet und in einem separaten Medienpaket (verkleinerte JPG-Kopien plus Originaldateinamen) vorbereitet. **Sie sind noch nicht als Binärdateien ins GitHub-Repository übertragen.** Daher hier bewusst keine nicht vorhandenen relativen Bild-Links:

1. **`01-tomaten-am-boden.jpg`** – Küchenboden mit ausgeschütteten Tomaten; Testkontext für die Bodenaufnahme (Screenshot 20.11., 20:57).
2. **`02-standplatz-diagnose.jpg`** – Inspector und Console zur flexiblen Pfannen-/Standplatzsuche; Hindernis `NavMesh: 0` (21.11., 17:17).
3. **`03-pfanne-feuer-erfolg-und-problem.jpg`** – Pfanne auf der Herdplatte und brennendes Feuer, Zombie steht jedoch fälschlich auf dem Herd (21.11., 17:23).
4. **`04-kuechen-standpunkt.jpg`** – Standpunkt des Zombies vor dem Herd; Regression durch zu strenge Sicherheitsprüfung (21.11., 17:33).

**Bereits im Repository verfügbarer Kontext (keine Aufnahme dieser Zombie-Session):** [Gameplay-Video „Vom Garten zur Tomatensuppe“](media/2026-11-04-garten-zur-tomatensuppe-kurz.mp4?raw=true). Dieses zeigt die räumliche Spielumgebung und die bestehende Spieler-Kochhandlung. Die [Dokumentation der Turmmauer](2026-11-01-turmmauer.md) enthält zudem Originalaufnahmen des Schlossturms.

## Session_E – Ergebnis und Reflexion

### Erreicht / beobachtet
- Funktionierenden **Einzeltest** zum Aufnehmen einer Tomate und sanften Ablegen in einen Korb entwickelt; Nutzer bestätigte den Erfolg.
- Funktionierende Teile der Zombie-Kochroutine erweitert: Korb ausleeren, einzelne Bodentomaten aufnehmen und in die Pfanne ablegen.
- Zustandsorientierten Startcheck für Wasser und Tomaten eingeführt; direkte Herdinteraktion bei vorbereitetem Gericht erprobt.
- Funktionierenden Feuer-Effekt und eine **einzelne** Topfablage auf der Herdplatte im Play Mode sichtbar beobachtet, jedoch mit fehlerhafter NPC-Position.
- Wiederholungsansätze für unerreichbare Tomaten und alternative Standplätze entwickelt.

### Offen / nicht zuverlässig nachgewiesen
- Alle vier ausgeschütteten Tomaten in **jedem** Durchlauf aufnehmen.
- Sicherstellen, dass der Zombie nach dem Anzünden stets **auf dem Küchenboden** bleibt.
- Vollständigen Ablauf Feuer → Topf aufnehmen → Topf korrekt abstellen **ohne** NavMesh-/Greifregression.
- Die aktuelle lokale Szenen- und Scriptversion kontrolliert nach GitHub synchronisieren.
- Medienbilder in die geplante GitHub-Ablage einspielen, sobald ein Binärdatei-Upload möglich ist.

### Reflexion

Die Iterationen verdeutlichen, dass **begehbarer Weg**, **physikalische Erreichbarkeit** und **tatsächlich ausgeführte Interaktion** separat gemessen werden müssen. Die häufigen lokalen Korrekturen haben das Verhalten in Teilen robuster gemacht, aber bei jeder neuen Aufgabe neue Abhängigkeiten erzeugt. Für das Portfolio ist gerade diese Grenze zwischen Level-Geometrie, Gameplay-Verhalten und Systemarchitektur wertvoll. Der nächste sinnvolle Schritt ist deshalb weniger eine weitere Sonderregel als eine **stabile, modular testbare Grundlage** für Navigation und Greifen.

## Portfolio-Ergebnis

### Kurztext

In *Petrusilius’ Schloss* soll ein Zombie als tollpatschiger Gehilfe selbstständig Zutaten einsammeln, Gefässe transportieren und am Herd kochen. Ich verband Navigation über einen engen Schlossturm, physikalisches Greifen und zustandsabhängige Aufträge zu einer zunehmend komplexen Interaktionsschlaufe. Isolierte Playtests halfen, das Greifen und sanfte Ablegen von Tomaten zuverlässig zu entwickeln; Console-Diagnosen deckten anschliessend Grenzfälle zwischen NavMesh, Standplatz und Herdgeometrie auf. Die Arbeit zeigt meinen Schwerpunkt an der Schnittstelle von **Level Design, Environmental Interaction und Technical Gameplay** – einschliesslich der Entscheidung, eine zu gross gewordene Routine in wiederverwendbare Module zu überführen.

### Nächste Schritte

- [ ] Herdfläche im **NavMesh** zuverlässig als nicht begehbar markieren; sicherer Boden-Standplatz bleibt erreichbar.
- [ ] Feuer und Topfablage mit getrennten, nachvollziehbaren Reichweitenprüfungen testen.
- [ ] Vier-Tomaten-Durchlauf mit Screenshots/Video vollständig nachweisen.
- [ ] Aktuelle Gameplay-Scripts kontrolliert versionieren und den stabilen Stand im Repository sichern.
- [ ] `ZombieKitchenRoutine.cs` in kleinere, **verhaltensneutrale** Module aufteilen.
- [ ] Die vier Original-Screenshots über den Repository-Medienordner nachpflegen.
