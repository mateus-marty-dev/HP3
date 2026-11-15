# Zombie: Korb in die Küche bringen

Aktueller Auftrag (15.11.2026): Nach dem Ablegen des Korbs nimmt der Zombie den Kochtopf auf und wartet damit in den Händen. Der weitere Kochablauf bleibt aufgehoben.

Zu Beginn bleibt der Zombie im Idle. Petrusilius muss mit leeren Händen in Interaktionsreichweite gehen, den Zombie ansehen und E drücken. Erst dann startet der bestehende Ablauf mit Korbholen und Tomatensammeln. Weiteres Drücken startet keine zweite Routine. Nach Abschluss bleibt der Zombie im Idle; ein neuer Play-Durchlauf setzt den Auftrag zurück.

Nach dem bestehenden Einsammeln von vier Tomaten:

1. Den gefüllten Korb aufnehmen und zur Tür gehen.
2. Die Tür öffnen.
3. Den Korb über die Treppe zum Küchenziel tragen.
4. Den Korb dort ablegen.
5. Zum Kochtopf gehen, sich zu ihm drehen, mit beiden Händen greifen und anheben.
6. Mit dem Topf in den Händen im Idle warten.

Im ZombieBehaviour kann `Kitchen Destination` auf einen Zielpunkt in der Küche gesetzt werden. Ohne Zuweisung dient das vorhandene Objekt `Pan` als Ortsreferenz. `Cooking Pot` legt den aufzunehmenden Kochtopf fest; ohne Zuweisung wird ebenfalls `Pan` verwendet. Der Topf benötigt Rigidbody und PickupInteractable und muss für beide Hände erreichbar sein. Wasserholen, Umfüllen der Tomaten, Transport zum Ofen und Feueranzünden bleiben aus seiner Routine entfernt.

Der Weg muss durchgehend auf dem in der Szene gebackenen NavMesh erreichbar sein. Bei fehlendem Ziel, blockiertem Weg oder fehlendem Abstellplatz zeigt `Interaction Status` den Grund; der Zombie wartet. Die Änderung dieses Ablaufs repariert keine Lücken im NavMesh.

Die allgemeinen Spielerfunktionen für Pfanne, Wasser, Tomaten und Ofen bleiben verfügbar.
