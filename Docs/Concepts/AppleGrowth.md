# Apfelwachstum einrichten

Der Baum bleibt sichtbar. Nur die Fruechte werden ein- und ausgeblendet.

1. Pro Apfelposition ein leeres, aktives Kindobjekt `AppleHolder` im Baum anlegen und `AppleGrowth` hinzufuegen.
2. Einen gruenen und einen roten Apfel als separate Kinder darunter platzieren. Beide muessen an derselben Position sitzen und ihre gewuenschte Endgroesse haben. Nur Fruechte zuweisen, keine komplette Baumstufe.
3. Die beiden Objekte aus der Hierarchy in `Green Apple` und `Red Apple` ziehen. Das Elternobjekt immer aktiv lassen. Beim Skalieren sollte der Pivot des gruenen Apfels am Fruchtansatz liegen.
4. `Weather Manager` wird beim Start automatisch gefunden. Weitere Apfelpositionen koennen durch Duplizieren des eingerichteten AppleHolder angelegt werden.
5. Szene ausserhalb des Play-Modus speichern.

## Ablauf und Test

- Beim Start sind beide Apfelmodelle unsichtbar. Sonne allein erzeugt keine Aepfel.
- Taste 3: Regen fuellt den Wasservorrat. Zehn Sekunden Regen fuellen ihn standardmaessig voll.
- Taste 1: Sonne nutzt gespeichertes Wasser. Nach zehn Sonnensekunden erscheint ein winziger gruener Apfel; in weiteren zwanzig Sekunden waechst er auf volle Groesse.
- Nach weiteren zwanzig Sonnensekunden wird das gruene Modell durch das rote ersetzt. Der Modellwechsel ist direkt; das Groessenwachstum ist kontinuierlich.
- Taste 2 pausiert den Fortschritt; Regen pausiert das Wachstum ebenfalls und fuellt Wasser nach. Ohne Wasser pausiert die Pflanze bis zum naechsten Regen und Sonnenschein. Fortschritt geht dabei nicht verloren.
- Zum Test der Wassergrenze nur eine Sekunde regnen lassen: der Vorrat reicht ungefaehr fuer zehn Sonnensekunden. Nach erneutem Regen und Sonne muss das Wachstum weiterlaufen.
- Reife Aepfel fallen einzeln nach einer zufaelligen Wartezeit von 15 bis 90 Sekunden herunter, unabhaengig vom Wetter. `Fall When Ripe`, `Minimum Fall Delay` und `Maximum Fall Delay` steuern das Verhalten pro AppleGrowth. Jeder Apfel faellt einmal; alle fallen irgendwann, wenn die Simulation lange genug laeuft.
- Zum schnellen Test die Wartezeit vor dem Play-Modus auf 1 bis 5 Sekunden setzen. Mehrere Aepfel reifen lassen, danach Bewoelkung einschalten: sie muessen trotzdem zeitversetzt fallen. Gruene Aepfel duerfen nicht fallen. Der Boden braucht einen Collider, damit die Fruechte liegen bleiben.
- Beim Fallen wird nur das rote Modell vom Baum geloest und erhaelt Rigidbody und Kugelcollider. Collider direkt auf den zugewiesenen Crown Meshes werden fuer diese Frucht ignoriert, damit sie nicht in der Krone feststeckt.
- Ernte, erneuter Fruchtzyklus, Schatten- und Dachpruefung sind noch nicht implementiert.

Das Skript veraendert weder Materialien noch die vorhandenen Karotten- und Tomatenkomponenten. Die Baum-Szene muss im Inspector eingerichtet werden; das importierte AppleTree-Modell wird nicht automatisch umgebaut.
