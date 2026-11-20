# Teich und Ufer

Die gemeinsame Abfrage liegt in `HP3_WaterBody.TryGetOpenWaterSurface`.
Als offenes Wasser gilt nur ein Punkt innerhalb des rechteckigen PondSurface-Meshs
und der Triggerzone, an dem der höchste feste Boden-Collider unter der Wasserhöhe liegt.
Die Triggerbox allein ist keine Ufergrenze. Bewegliche Gegenstände und NPCs werden
bei der Bodenmessung ignoriert. Ufer, Felsen und Gelände benötigen passende Collider.

`MeasureShore()` tastet die Fläche in einem 25-cm-Raster ab. Es liefert Wasserpunkte
und eine Richtung zum Land für weitere Interaktionen. Dies ist eine gerasterte
Annäherung, keine zentimetergenaue Kontur; Wellen und Geometrieänderungen können
die Ergebnisse verändern. Für ein anders geformtes Wasser-Mesh muss die Prüfung
der Mesh-Fläche angepasst werden.

In Unity beim HP3_WaterBody-Komponentenmenü **Ufer vermessen (25-cm-Raster)** wählen.
Bei ausgewähltem Objekt und eingeschalteten Gizmos zeigt Grün die Wasserpunkte,
Gelb die Richtung zum Land. Die Console nennt die gemessene Anzahl.
Die NPC-Interaktion misst bei der Zielsuche erneut und prüft zusätzlich einen
trockenen NavMesh-Standplatz sowie einen vollständigen Laufweg.

Auch RainCollector nutzt diese Wasserdefinition: Unter Gelände und unter dem
Teichboden wird kein Wasser aufgenommen.

Validierung: Skripte kompiliert. Die Uferkontur und der vollständige Schöpfvorgang
müssen noch in der laufenden Szene geprüft werden.
