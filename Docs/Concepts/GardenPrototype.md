# Gartenprototyp einrichten

1. Die aktuelle Szene in Unity speichern.
2. `GardenBed` auf das Beet setzen. Das Beet braucht einen Collider auf demselben Objekt oder einem Kind, damit der vorhandene Spieler-Raycasts es mit E erreicht.
3. Pro Karotte ein leeres, aktives Elternobjekt `Carrot` anlegen und dort `PlantGrowth` hinzufuegen.
4. Das sichtbare Modell `Carrot_Stage_0` als Kind darunter ablegen. Das Elternobjekt bleibt immer aktiv.
5. Im Feld `Garden Bed` das Beet zuweisen. Unter `Growth Stages` zuerst nur ein Element anlegen und das vollstaendige Stage-0-Modell zuweisen, nicht einzelne Blaetter.
6. Weitere Modelle spaeter als separate Kinder hinzufuegen und in Reihenfolge Stage 0, Stage 1, Stage 2 zuweisen. Kopien anderer Karotten sind eigene Pflanzen, keine Wachstumsstufen derselben Pflanze.

## Verhalten und manueller Test

- Beim Spielstart sind alle zugewiesenen Stufen unsichtbar. Jede Pflanze gilt bereits als gesaet.
- Der Boden beginnt trocken. Auch nach 10 Sekunden erscheinen keine Blaetter.
- Mit leeren Haenden aus maximal 3 Metern auf das Beet schauen und E druecken. Das erhoeht die Feuchtigkeit um 0.5; fuer diesen Test wird kein Wasser aus einem Eimer verbraucht.
- Nach 10 Sekunden mit ausreichend feuchtem Boden erscheint Stage 0 mit 5 Prozent ihrer eingerichteten Groesse. Sie waechst innerhalb weiterer 20 feuchter Sekunden gleichmaessig auf ihre volle Groesse. `Initial Scale Fraction` und `Seconds Per Stage` steuern Startgroesse und Wachstumsdauer. Auch bei nur einer Stufe findet dieses Wachstum statt.
- Sind weitere Stufen zugewiesen, erscheint alle weiteren 20 feuchten Sekunden die naechste Stufe. Die vorherige verschwindet. Bei nur einer zugewiesenen Stufe endet der Prototyp bei Stage 0.
- Im Play-Modus `Moisture` am Beet auf 0 setzen: der Fortschritt pausiert. Wieder giessen: er laeuft weiter, ohne Neustart.
- Mehrere Pflanzen koennen dasselbe Beet referenzieren und teilen dessen Feuchtigkeit.
- Das Skalieren erfolgt um den Pivot des Stage-0-Objekts. Falls dieser nicht am Pflanzenansatz liegt, ein leeres Kindobjekt am Pflanzenansatz anlegen, das Modell darunter platzieren und dieses Kindobjekt als Stage 0 zuweisen.
- Spiel stoppen und neu starten: Wachstum und Feuchtigkeit beginnen wieder mit den gespeicherten Startwerten. Es gibt noch kein Speichersystem.

## Regenbewaesserung

- `GardenBed` findet den aktiven `WeatherManager` beim Start automatisch. Er kann auch im Inspector zugewiesen werden.
- Taste 3 aktiviert Regen. Bei aktiviertem `Receives Rain` erhaelt das Beet kontinuierlich 0.05 Feuchtigkeit pro Sekunde ueber dieselbe `AddWater`-Methode wie E.
- Ein trockenes Beet erreicht nach etwa 2.3 Sekunden die Wachstumsschwelle. Danach beginnt die Keimzeit von 10 feuchten Sekunden.
- Mit Taste 1 oder 2 endet die Regenbewaesserung. Restfeuchtigkeit bleibt erhalten; der Boden trocknet weiter langsam aus.
- Test: Ohne E Regen aktivieren, steigende Feuchtigkeit und anschliessend Keimung beobachten. Danach Sonne aktivieren und sinkende Feuchtigkeit pruefen. Bei `Receives Rain` aus darf Regen keine Feuchtigkeit hinzufuegen; E funktioniert weiterhin.
- Ueberdachung wird nicht automatisch erkannt. Fuer geschuetzte Beete `Receives Rain` deaktivieren.

## Kuebelbewaesserung

- Der vorhandene `RainCollector` bewaessert beim Ausleeren automatisch das naechste aktive Beet innerhalb von `Watering Distance` (Standard: 1.5 Meter).
- Die Entfernung wird vom optionalen `Pour Point`, sonst von `Water Top`, zur Collider-Oberflaeche des Beets gemessen. Der Beet-Collider muss auf dem `GardenBed`-Objekt oder einem Kind liegen und darf kein Trigger sein.
- Ab dem bestehenden Kippwinkel (`Pour Angle`, aktuell 55 Grad) sinkt der Fuellstand. Nur die wirklich ausgegossene Menge erhoeht die Bodenfeuchtigkeit; ein voller Kuebel liefert standardmaessig 1.0 Feuchtigkeit. `Moisture Per Full Bucket` steuert dieses Verhaeltnis.
- Ausserhalb der Reichweite geht ausgegossenes Wasser verloren. Ein leerer oder aufrechter Kuebel bewaessert nicht. Mehrere nahe Beete erhalten die Menge nicht mehrfach; nur das naechste wird gewaessert.
- Test im Play-Modus: Sonne mit Taste 1 einschalten, `Fill Level` am Kuebel auf 0.5 setzen, nahe am Beet kippen und beide Werte beobachten. Mit den aktuellen 0.25 Pour Speed ist er nach etwa zwei Sekunden leer und hat dem Beet insgesamt 0.5 Feuchtigkeit zugefuehrt (abzueglich Austrocknung).
- Anschliessend leer weiterkippen, aufrecht halten und ausserhalb der Reichweite ausleeren: keine weitere Kuebelbewaesserung. Bei zwei nahen Beeten darf nur eines Wasser erhalten.
- Dies ist eine Naeherungspruefung, keine Wasserstrahl-Simulation: Hindernisse zwischen Kuebel und Beet werden noch nicht beruecksichtigt.

Die vorhandene E-Bedienung legt getragene Gegenstaende zuerst ab. Das Kippen verwendet weiterhin die vorhandene Kuebelrotation.
