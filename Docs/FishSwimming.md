# Fisch-Schwimmen

## Einrichtung

In Unity die Szene `Assets/Scenes/SampleScene.unity` oeffnen und ausserhalb des Play Mode
`Tools > Hotzenplotz > Fisch-Wegpunkte einrichten` ausfuehren.
Die Einrichtung sichert zuvor die gespeicherte Szene und den Fisch-Controller unter
`Backups/FishSwimming/`. Ungespeicherte Szenenaenderungen vorher selbst speichern.

Das Werkzeug verwendet `Fish.fbx` und dessen ersten importierten Animationsclip.
Ohne Clip bricht es ab. Der vorhandene Controller verweist bisher auf eine im Projekt
nicht vorhandene Asset-GUID. Das Werkzeug ersetzt diese Referenz durch eine eigene,
auf Wiederholung eingestellte Kopie `FishSwim.anim`.

Es erzeugt `FishSwimming` mit sechs Wegpunkten und einem Fisch (Laenge ca. 55 cm).
Ein erneuter Aufruf bei vorhandenem `FishSwimming` bricht ab, um Duplikate zu vermeiden.

## Kontrolle im Play Mode

1. `FishSwimming` auswaehlen und Gizmos einschalten: Kugeln markieren Ziele, Linien
   zeigen moegliche direkte Schwimmwege.
2. Wegpunkte bei Bedarf in der Draufsicht verschieben. **Jede Verbindung** muss im
   Wasser liegen. Die zentrale Ausgangsroute ist nur aus den Wasser-Renderer-Grenzen
   abgeleitet; Ufer, Steine, Boden und Pflanzen werden nicht automatisch erkannt.
3. Die gesamte Fischgroesse muss unter Wasser und ueber dem Boden bleiben.
4. Falls der Fisch seitwaerts/rueckwaerts schwimmt: am `Fish`-Objekt im `AIMove`
   `Model Rotation Offset` anpassen (typischerweise Y = 90, -90 oder 180).
5. Animation auf Schleife und ruhige Richtungswechsel kontrollieren.

`Min Speed`, `Max Speed` (Meter/Sekunde) und `Turn Speed` (Grad/Sekunde) steuern
Bewegung und Drehung. Root Motion bleibt aus; das Animator-Tempo folgt der Geschwindigkeit.
Die urspruengliche zufaellige, nicht proportionale Skalierung wurde entfernt.

## Vorhandene AI

`AIMove` verwendet weiterhin `AISpawner` als Wegpunkt-Manager, auch ohne aktive
Spawn-Gruppen. Wegpunkte werden explizit im Inspector zugewiesen; der Tag `waypoint`
ist nicht mehr noetig. Leere Listen halten den Fisch an, ein einzelnes Ziel wird
angefahren und danach gehalten. Unmittelbare Wiederholungen werden vermieden.
`AISpawner` begrenzt die Anzahl pro Gruppe und erzeugt Fische an Wegpunkten statt
zufaellig ausserhalb der Route. Das alte Feld `spawnRate` pro Gruppe war und bleibt
unbenutzt; das globale Spawn-Intervall gilt fuer alle Gruppen.

## Verifikationsstand

Am 28.10.2026 im geoeffneten Unity 6000.3.15f1 eingerichtet und in SampleScene
 gespeichert. Sicherung der vorherigen Szene und des Controllers liegt unter
`Backups/FishSwimming/` (Zeitstempel 20261028_214820).

Der importierte Clip `Armature|Action` dauert 10,41667 Sekunden und besitzt 281
Animationskurven. `FishSwim.anim` ist auf Schleife gestellt und dem Controller
zugeordnet. Die Materialien verwenden Universal Render Pipeline/Lit.

Die Editor-Pruefung ueber `Tools > Hotzenplotz > Fisch-Schwimmen pruefen` simulierte
120 Sekunden: ca. 56,25 Meter Bewegung, fuenf von sechs Wegpunkten besucht,
mindestens 0,62 Meter Abstand des Fischursprungs zur Wasseroberflaeche und 0,67 Meter
zum Terrain. Das ist eine Bewegungspruefung ohne Physik- oder Hindernisvermeidung;
die Fischlaenge betraegt ca. 55 cm. Das Werkzeug setzt Position und Rotation nach
der Simulation zurueck. `Fish3.glb` wird nicht verwendet.
`Model Rotation Offset` ist fuer dieses FBX auf Y = 180 Grad eingestellt. Im Play Mode wurde die Bewegung zusaetzlich beobachtet; anschliessend wurde Play Mode beendet und die Szene gespeichert.
