# 2026-11-15 – Zombie mit Tomatenkorb über die Wendeltreppe

- **Projekt:** HP.3 / Petrusilius’ Schloss
- **Bereich:** NPC-Interaktion, Navigation, Level Design – Environment & World Building
- **Werkzeuge:** Unity 6 (NavMeshSurface / AI Navigation / C#), Console, Chat-basierte Fehlersuche
- **Status:** Funktionsziel im Play Mode laut abschliessendem Nutzertest erreicht; Bild des final abgestellten Korbs noch ausstehend
- **Zeit:** Sessionstart am 15.11.2026 um ca. 11:24 Uhr (Schweizer Zeit); Sessionende und Dauer nicht erfasst
- **Integrationsstand:** Gameplay in lokaler Unity-Szene getestet. Der zum Dokumentationszeitpunkt auf GitHub liegende `ZombieKitchenRoutine.cs` ist noch eine ältere Version mit `CookTomatoSoup()`; der lokal erfolgreich getestete Code ist **nicht** durch diesen Dokumentations-Commit synchronisiert.
- **Bezug:** [Vom Garten zur Tomatensuppe (04.11.)](2026-11-04-garten-zur-tomatensuppe.md) · [Modulare Turmmauer (01.11.)](2026-11-01-turmmauer.md)

> **Hinweis zum Verlauf:** Die Untersuchung der fehlenden Küchenroute begann bereits am 13.–14.11. Die folgenden Iterationen und der erfolgreiche Abschlusstest fanden am 15.11. statt. Es handelt sich nicht um mehrere unabhängig abgeschlossene Features.

## Session_S – Ausgangslage und Ziel

Der Zombie verfügt bereits über eine Interaktionsschlaufe: Korb suchen und aufnehmen, vier reife Tomaten ernten, sie physikalisch in den Korb legen und den gefüllten Korb tragen. Er öffnet die Schlosstür über `DoorInteractable.OpenForNpc()`. Die geplante letzte Etappe – den Korb über die enge Wendeltreppe zur Küche zu transportieren und dort abzusetzen – scheiterte bisher.

**Erfolgskriterium:** Der NPC soll nach dem Tomatensammeln den vollständigen Weg Garten → Turmeingang → Wendeltreppe → Küche zurücklegen und den Korb dort abstellen, ohne bereits funktionierende Korb- und Tomatenphysik zu zerstören.

**Design-Kontext:** Der Turm besitzt eine aus Steinsegmenten gebaute, enge Wendeltreppe. Die Navigation muss sich der Architektur anpassen, die zugleich die Atmosphäre des Schlosses prägt. Sichtbare Mauer- und Treppengeometrie sollten möglichst erhalten bleiben.

## Arbeitsschritte, Hypothesen und Versuche

### 1. NavMesh-Struktur sichtbar und prüfbar machen

**Ausgangslage:** Das Script erzeugte bei fehlendem Start-NavMesh zur Laufzeit ein GameObject `Zombie Navigation` mit einer `NavMeshSurface`. Nach dem Beenden des Play Mode verschwand es. Zusätzlich wurde nach dem Öffnen der Tür erneut `surface.BuildNavMesh()` aufgerufen. Warnungen wie `Source mesh Cylinder does not allow read access` erschwerten die Diagnose.

**Versuche:**
- Dauerhaftes GameObject `Navigation` mit `NavMeshSurface` in der Szene eingerichtet und manuell gebacken.
- NavMesh-Anzeige im Scene-Overlay `AI Navigation` aktiviert. Die türkisfarbenen begehbaren Flächen wurden sichtbar.
- `Humanoid` für den Test auf Radius **0.25 m**, Höhe **1.8 m**, Step Height **0.75 m** und Max Slope **45°** eingestellt.
- `Minimum Region Area` testweise auf **0** gesetzt.
- Später `Override Voxel Size = 0.05 m` getestet, um die enge Treppengeometrie genauer abzutasten.
- Einfluss des Tür-Colliders und des `NavMesh Modifier` (Modus `Remove Object`) geprüft.

**Ergebnis:** Das NavMesh wurde sichtbar und einzelne graue Stellen zwischen den türkisen Treppenbereichen wurden dokumentiert. Weder kleinere Voxel noch die Tür-/Radius-Tests beseitigten allein die Fehlermeldung `Kein begehbarer Weg`. Graue Setzstufen sind für sich genommen noch kein Beweis für eine unterbrochene Navigation.

**Gestalterische Entscheidung:** Die Steinarchitektur nicht vorschnell umbauen. Zunächst tatsächliche Pfadberechnung und Zielwahl analysieren; eine unsichtbare Navigationsrampe blieb als mögliche spätere Massnahme offen, wurde für den erfolgreichen Test jedoch nicht benötigt.

### 2. Das Ziel der Küchenlieferung explizit festlegen

**Problem:** `DeliverBasketToKitchen()` benutzte bei nicht zugewiesenem `kitchenDestination` den Transform des Objekts `Pan` als Ersatz. `TryApproach()` suchte rund um diesen Gegenstand geeignete NavMesh-Punkte – bei mehreren Stockwerken ein potenziell ungeeigneter Ansatz.

**Änderung:** Das bereits angelegte GameObject `KitchenBasketDropPoint` wurde dem Inspector-Feld **Kitchen Destination** des Zombie zugewiesen.

**Beobachtung:** Danach wurde statt `Kein begehbarer Weg` ein vollständiger Pfad gefunden. Der Zombie betrat die Wendeltreppe und stieg mehrere Stufen hinauf, blieb aber im weiteren Verlauf stehen. Im Inspector stand nun `Weg blockiert: Korb ueber die Treppe in die Kueche bringen`.

### 3. Engstelle, Korbphysik und Geschwindigkeit prüfen

**Hypothesen:** Wand/Clearance, eine zu enge Kurve, der getragene Korb oder die Bewegungsparameter könnten den Lauf blockieren.

**Versuche und Beobachtungen:**
- Die innere Mauer wurde testweise im Play Mode verkleinert; keine nachgewiesene Lösung. Änderungen an der Mauer beeinflussen ein bereits gebackenes NavMesh nicht automatisch.
- Das Abschalten der Basket-Collider war kein geeigneter Test: Die Tomaten fielen daraufhin durch den Korb. Die Collider mussten für die bestehende Physik aktiv bleiben.
- Eine zeitweilig erhöhte Laufgeschwindigkeit (im Inspector bis ca. **3.09 m/s**) liess den Zombie weiter nach oben gelangen, beseitigte die Blockade aber nicht. Der Zwischenstand an der Treppe wurde im Game View dokumentiert.

**Zwischenergebnis:** Grössere Reichweite bei höherem Tempo deutete darauf hin, dass ein zeitbasierter Abbruch statt einer geometrischen Blockade beteiligt sein könnte.

### 4. Die entscheidende Console-Diagnose

Eine vorübergehende Warnung in `WalkToKitchenStation()` gab beim Abbruch aus:

```text
Zombie blockiert!
Restweg: +unendlich m
Geschwindigkeit: 1.99 m/s
Gewuenschte Geschwindigkeit: 1.99 m/s
Naechster Wegpunkt: (-25.87, 60.84, -14.47)
Pfadstatus: PathComplete
```

**Interpretation:** `PathComplete` bestätigte einen berechneten vollständigen Pfad. Zugleich meldete der Agent im Moment der Warnung Geschwindigkeit, während `remainingDistance` unendlich blieb. Die Warnung beweist daher nicht, dass der Zombie physisch feststeckte.

**Fehler im bisherigen Fortschritts-Check:**

```csharp
float lastDistance = float.PositiveInfinity;
if (agent.remainingDistance < lastDistance - 0.02f)
{
    lastDistance = agent.remainingDistance;
    blockedSeconds = 0f;
}
else blockedSeconds += Time.deltaTime;
if (blockedSeconds > 15f) break;
```

Wenn `remainingDistance` unbekannt und `Infinity` ist, wird trotz tatsächlicher Bewegung kein Fortschritt erkannt. Nach 15 Sekunden wird die Coroutine abgebrochen. Dies erklärt plausibel, warum höhere Geschwindigkeit den Zombie weiter brachte, ohne die Ursache zu beheben.

### 5. Lösung: realen Fortschritt statt unbekannter Restdistanz messen

`WalkToKitchenStation()` wurde im lokalen Arbeitsstand angepasst:
- Ankunft am tatsächlichen Ende der berechneten Route (`path.corners[^1]`) zusätzlich über Positionsabstand prüfen.
- `remainingDistance` nur für die Ankunft verwenden, wenn der Wert endlich und nicht `NaN` ist.
- Fortschritt anhand von `agent.nextPosition` statt anhand der unter Umständen unendlichen `remainingDistance` messen.
- Den 15-Sekunden-Blockadetimer nur bei ausbleibender Positionsänderung weiterlaufen lassen.

**Beispiel aus dem Lösungsprinzip (gekürzt, kein vollständiger Ersatz der Methode):**

```csharp
Vector3 routeEnd = path.corners[path.corners.Length - 1];
Vector3 lastPosition = agent.nextPosition;

bool validRemaining =
    !float.IsInfinity(agent.remainingDistance) &&
    !float.IsNaN(agent.remainingDistance);

bool arrived =
    Vector3.Distance(agent.nextPosition, routeEnd)
        <= agent.stoppingDistance + 0.12f ||
    (validRemaining &&
        agent.remainingDistance <= agent.stoppingDistance + 0.08f);

if (Vector3.Distance(agent.nextPosition, lastPosition) > 0.03f)
{
    lastPosition = agent.nextPosition;
    blockedSeconds = 0f;
}
```

**Ergebnis:** Der Nutzer meldete unmittelbar nach dem Test der überarbeiteten Methode: **„Perfekt, hat geklappt!“** Damit ist die erfolgreiche Fortsetzung der Interaktionsschlaufe im Play Mode aus dem Nutzerfeedback belegt. Ein zusätzlicher Screenshot der finalen Korbablage wurde in dieser Unterhaltung noch nicht bereitgestellt.

## Entscheidungen und Abwägungen

| Entscheidung | Verworfener / zurückgestellter Ansatz | Begründung |
|---|---|---|
| Eigenen `KitchenBasketDropPoint` verwenden | Position der Pfanne als Navigationsziel | Trennung von Laufziel und interaktivem Objekt; klarere Zielposition im Innenraum |
| Bestehende Treppe beibehalten | Wände verkleinern oder sofort unsichtbare Rampe modellieren | Kein belastbarer Nachweis, dass die sichtbare Geometrie die Ursache war |
| Physikalische Basket-Collider beibehalten | Alle Collider abschalten | Die Tomaten benötigen weiterhin physikalische Begrenzung |
| Fortschritt über Positionsänderung prüfen | Nur `remainingDistance` vergleichen | `Infinity` darf einen bewegten Agenten nicht fälschlich als blockiert werten |
| Diagnose über Runtime-Logs priorisieren | Weiteres blindes Tuning von Radius, Voxeln und Geschwindigkeit | Der Console-Log lieferte die kausale Erklärung |

## Screenshots und Nachweise

In der Unterhaltung wurden Originalaufnahmen erstellt. Für die spätere Bildablage im Repository sind diese vier Nachweise ausgewählt:

1. **`01-navmesh-stufenluecken.png`** – Scene View mit türkisen, stellenweise unregelmässigen NavMesh-Flächen auf der Wendeltreppe (15.11., 11:55).
2. **`02-zombie-am-treppeneingang.png`** – Zombie mit Tomatenkorb vor bzw. am Anfang der Treppe, trotz Bake noch ohne erfolgreichen Küchenweg (15.11., 12:05).
3. **`03-zombie-vor-kueche-blockiert.png`** – Der Zombie hat deutlich an Höhe gewonnen, läuft aber vor Erreichen des Ziels nicht weiter (15.11., 12:17).
4. **`04-console-infinity-warnung.png`** – Diagnose mit `Restweg: +unendlich`, `Geschwindigkeit: 1.99 m/s` und `PathComplete` (15.11., 12:22).

**Medienstatus:** Die vier Original-Screenshots liegen aus der Unterhaltung als separates Archiv vor; sie wurden mit diesem reinen Text-Commit **noch nicht** als Binärdateien nach `Docs/Portfolio/media/2026-11-15-zombie-wendeltreppe-navmesh/` hochgeladen. Deshalb enthält dieser Bericht bewusst keine kaputten relativen Bild-Links. Ein abschliessender Aufnahmebeleg des Korbs in der Küche fehlt noch.

## Session_E – Ergebnis und Reflexion (fachlicher Zwischenstand)

### Erreicht
- Eindeutiges Küchenziel im NPC-Inspector eingerichtet.
- Wendeltreppe wird im Spiel vom Zombie betreten und bestiegen.
- Vorzeitigen Coroutine-Abbruch bei unbekannter NavMesh-Restdistanz als eigentliche Problemursache eingegrenzt und lokal korrigiert.
- Nutzer bestätigt erfolgreiche Interaktionsschlaufe nach der Korrektur.

### Noch offen / nicht nachgewiesen
- Aktuellen lokalen Script- und Szenenstand nach GitHub synchronisieren; dieser Dokumentations-Commit verändert **keine** Gameplay-Scripts.
- Finale Korbablage mit Screenshot oder kurzem Gameplay-Clip dokumentieren.
- Getragene Korbbreite und enge Treppenführung bei ruhigem Schlendertempo systematisch testen.
- Kinematic-Rigidbody-Warnungen (`Setting linear/angular velocity of a kinematic body is not supported`) getrennt untersuchen.
- Robustheit der Navigation nach Türöffnung für spätere Builds und mögliche weitere NPCs prüfen.

### Erkenntnisse

Die Debugging-Frage änderte sich während der Session: zunächst „Ist die Wendeltreppe begehbar?“, später „Bricht mein Script einen gültigen Laufweg fälschlich ab?“. Die Unterscheidung zwischen **Pfad vorhanden**, **Figur bewegt sich** und **Coroutine erklärt Bewegung als blockiert** war entscheidend. Als Arbeitsmethode bewährten sich Screenshot → Hypothese → isolierter Test → Console-Werte → kleine Codekorrektur. Rückblickend hätte die Instrumentierung früher beginnen können, statt zuerst die Bake-Parameter mehrfach anzupassen.

## Portfolio-Ergebnis

### Kurztext

Für die autonome Zombie-Interaktionsschlaufe in *Petrusilius’ Schloss* sollte ein NPC vier Tomaten ernten, den gefüllten Korb durch den Turmeingang und über die enge Wendeltreppe tragen und in der Küche abstellen. Ich untersuchte die NavMesh-Geometrie in Unity, definierte einen eigenständigen Küchen-Zielpunkt und testete Tür-, Collider- und Geschwindigkeitsannahmen. Die Laufzeitdiagnose zeigte schliesslich, dass ein unbekannter `remainingDistance`-Wert (`Infinity`) einen falschen 15-Sekunden-Abbruch auslöste. Durch eine Fortschrittsprüfung anhand der tatsächlichen Agent-Position konnte die Route im Play Mode erfolgreich abgeschlossen werden. Der Fall verbindet räumliches Level Design mit nachvollziehbarer, datenbasierter Gameplay-Fehlersuche.

### Kompetenzen und nächster Präsentationsschritt

- **Level Design / Environment:** Begehbarkeit, Clearance und Zielpunkte in einem vertikal verbundenen, engen Schlossraum.
- **Technical Gameplay:** NavMeshAgent-Diagnose, Navigation und Physikinteraktion unter realen Laufzeitbedingungen.
- **Iterativer Prozess:** Fehlannahmen dokumentiert; keine unnötige Änderung der bestehenden Turmgeometrie.
- **Portfolio-Beleg:** Vier Entwicklungs-Screenshots; als nächste Aufnahme ein kurzer ungeschnittener Durchlauf vom Turmeingang bis zum sichtbar abgelegten Korb.

