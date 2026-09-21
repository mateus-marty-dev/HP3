# Gartenprototyp einrichten

## Kartoffelknollen ernten

- In der letzten Kartoffelstufe sind die fuenf Knollen `Potato`, `Potato.001`, `Potato.002`, `Potato.003` und `Potato.005` als einzelne `Harvest Fruits` eingetragen.
- Die importierten Kartoffelmeshes enthielten jeweils eine duenne Wurzel. In `PotatoHarvest` liegen getrennte Knollen- und Wurzelmeshes. Die Wurzeln bleiben als eigene Geschwisterobjekte an der letzten Wachstumsstufe; nur die Knollen erhalten `PickupInteractable`.
- Nach der Reife eine Knolle anvisieren und E druecken, nochmals E zum Ablegen. Pflanze, Wurzeln und Beeren bleiben stehen. Halteposition, Nahbereichserkennung und Kuebeltransport verwenden das vorhandene Aufnahmesystem.

## Karotte ernten

- In `SampleScene` ist `Carrot_Stage_3` als Ganzes unter `Harvest Fruits` der Karotte eingetragen. Wurzel und alle sechs Blattobjekte werden gemeinsam getragen.
- Erst in der letzten Wachstumsstufe entstehen die Aufnahme-Collider. Mit leeren Haenden auf das Gruen oder die sichtbare Wurzel zielen und E druecken. Nochmals E legt die Karotte ab; danach ist sie erneut aufnehmbar.
- Die Halteposition entspricht den Tomaten. Es gibt derzeit kein Nachwachsen nach der Ernte.
- Die Zielsuche erfasst auch Collider direkt an oder um die Kamera, damit man beim Ernten aus naechster Naehe nicht zuruecktreten muss.
- Die vorhandenen Wachstumszeiten bleiben bestehen: 50 feuchte Sekunden Keimzeit und 360 feuchte Sekunden pro Stufe, insgesamt etwa 18 Minuten 50 Sekunden bis Stage 3 bei durchgehend feuchtem Boden.
- Manueller Test: Vor Reife keine Aufnahme; nach Reife am Gruen aufnehmen, vollstaendige Karotte in der Hand pruefen, ablegen und erneut aufnehmen.

## Tomaten pfluecken

- In `SampleScene` sind alle fuenf roten Fruechte der letzten Tomatenstufe als `Harvest Fruits` am `PlantGrowth` eingetragen: `Ripe_Tomato`, `Ripe_Tomato.004`, `Leaf.080`, `Leaf.085` und `Leaf.088`. Die drei Leaf-Objekte sind trotz ihres Namens Fruechte mit dem Material `Tomato_Ripe`. `Harvest Hold Point` und `Harvest Player Collider` verwenden dieselben Referenzen wie die vorhandenen aufnehmbaren Gegenstaende.
- Erst bei vollstaendigem Wachstum erhalten diese Fruechte einen Collider, einen zunaechst unbeweglichen Rigidbody und `PickupInteractable`.
- Mit leeren Haenden aus maximal 3 Metern auf eine rote Tomate zielen und E druecken: Nur diese Frucht loest sich und wird getragen. Noch einmal E legt sie mit Schwerkraft ab; sie kann erneut aufgehoben werden.
- `PlayerInteraction` erlaubt mit `Pickup Aim Radius` (0.15 Meter) leichtes Danebenzielen. Sichtbare Fruechte haben dabei Vorrang vor dem Giessen des Beets dahinter. Waende, Boden und die Reichweite bleiben beruecksichtigt; mit Radius 0 gilt wieder nur der direkte Strahl.
- `Harvest Hold Position Offset` am `PlantGrowth` verschiebt nur die getragenen Fruechte: Standard (0, 0.65, 0.35) relativ zum Hold Point. Damit liegen sie in dieser Szene etwa 1.37 Meter vor der Kamera, leicht unter der Bildmitte. Die sichtbare Mesh-Mitte wird ausgerichtet, auch wenn der importierte Pivot an der Pflanzenwurzel liegt.
- Die Pflanze und die uebrigen Fruechte bleiben stehen. Gepflueckte Fruechte wachsen derzeit nicht nach.
- Manueller Test: Vor Reife ist keine Frucht pflueckbar. Nach Reife alle fuenf Fruechte nacheinander pfluecken und ablegen, eine abgelegte Frucht erneut aufnehmen. Alle sollen an derselben sichtbaren Halteposition erscheinen. Auch das Aufheben und Ablegen des bestehenden Kuebels pruefen.

## Tomaten im Kuebel transportieren

- `RainCollector` richtet `BucketContents` automatisch anhand der Wasserflaechen am Boden und am Rand ein. Es muss nichts zusaetzlich an der Szene zugewiesen werden.
- Beim Aufheben werden lose Pickup-Objekte innerhalb dieses Innenraums voruebergehend unbeweglich und mit dem Kuebel mitgefuehrt. Ihre sichtbare Collider-Mitte entscheidet, ob sie innen liegen; ein versetzter Modell-Pivot spielt keine Rolle.
- Beim Ablegen werden Schwerkraft, Kollisionsmodus und Spieler-Kollisionen wiederhergestellt. Beim Kippen ab dem bestehenden `Pour Angle` werden die Fruechte ebenfalls freigegeben und koennen herausfallen.
- Die Tomaten bleiben eigenstaendige Objekte und werden weder geloescht noch dem Wasserobjekt untergeordnet. Kontinuierliche spekulative Kollisionserkennung verbessert ihre Kollisionen mit duennen Kuebelwaenden.
- Test: Mehrere Tomaten hineinlegen, Kuebel mehrfach aufnehmen, bewegen und ablegen. Danach kippen und die herausgefallenen Tomaten einzeln wieder aufnehmen. Gegenstaende neben dem Kuebel duerfen nicht mitgenommen werden.

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
