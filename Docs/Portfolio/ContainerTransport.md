# Physikalischer Inhalt, direkte Steuerung

## Behälter-Interaktion in HP3 – Petrusilius' Schloss

**Bereich:** Gameplay Design und technisches Prototyping  
**Werkzeuge:** Unity 6, URP, C#  
**Aufnahme:** 25. Oktober 2026  
**Stand:** Entwicklungsprototyp

In HP3 werden geerntete Gegenstände von Hand in Behälter gelegt und durch den Garten getragen. Diese alltägliche Handlung soll unmittelbar reagieren und zugleich glaubwürdig wirken: Tomaten sollen im Bucket liegen und sich bewegen können, ohne beim Aufnehmen unkontrolliert herausgeschleudert zu werden.

## Gameplay-Video

**[Video öffnen oder herunterladen – Behälter-Transport in HP3](media/container-transport-2026-10-25.mp4?raw=true)**

Die Aufnahme dokumentiert den Prototyp im Garten, das Einlegen von Tomaten und den Umgang mit dem Bucket. Sie enthält ausserdem weitere Interaktionen im Garten. Dauer: etwa **1 Minute 34 Sekunden**. Die Web-Version enthält den vollständigen bereitgestellten Clip mit Ton, auf 1920 Pixel Breite verkleinert; das Seitenverhältnis bleibt erhalten.

Falls GitHub keine Videovorschau anbietet, lässt sich die MP4 über den Link herunterladen und lokal abspielen.

## Ausgangsproblem

Beim Aufnehmen bewegte sich der Behälter direkt zum Haltepunkt des Spielers. Die enthaltenen Gegenstände reagierten darauf mit starken Physikimpulsen und konnten herausfliegen. Damit wurde eine einfache Transportaufgabe unzuverlässig.

Die zentrale Gestaltungsfrage lautete: **Wie bleibt die Steuerung direkt, ohne die Beweglichkeit des Inhalts aufzugeben?**

## Iteration und Entscheidung

Zunächst wurden Kontaktparameter wie Rückprall, Dämpfung und Kollisionsberechnung angepasst. Das allein löste das Problem beim Aufnehmen nicht ausreichend.

Ein anschliessender Versuch führte den Bucket langsamer zum Haltepunkt. Im Playtest fühlte sich diese Bewegung unpassend an und wurde zurückgenommen. Auch ein dauerhaftes Fixieren des Inhalts entsprach nicht dem gewünschten Verhalten: Gegenstände sollten weiterhin in den Hohlraum fallen und darin beweglich bleiben.

Die gewählte Lösung unterstützt deshalb gezielt den Transport des Inhalts. Die direkte Bedienung bleibt bestehen; die Gegenstände werden nicht dauerhaft am Behälter festgeklebt.

## Technischer Ansatz

- **Gemeinsame Transportbewegung:** Der Inhalt wird beim Versetzen des Behälters in dessen neuen Bezugsrahmen übertragen. Die relativen Bewegungen bleiben erhalten.
- **Dynamischer Inhalt:** Schwerkraft und Zusammenstösse bleiben aktiv. Die Gegenstände werden für den Transport nicht dauerhaft auf kinematisch umgestellt.
- **Erkennung des Hohlraums:** Boden- und Wand-Collider bestimmen den Innenbereich unabhängig vom Wasserstand. Eine Toleranz verhindert, dass Gegenstände bei kleinen Grenzüberschreitungen sofort aus der Verfolgung fallen.
- **Darstellung pro Bild:** Während des Tragens folgen visuelle Kopien dem aktuellen Haltepunkt. Die Physik wird separat aktualisiert, um sichtbares Ruckeln durch die unterschiedlichen Aktualisierungsraten zu reduzieren.
- **Schutz vor Spieler-Kollisionen:** Enthaltene Gegenstände kollidieren vorübergehend nicht mit dem Spieler. Die Kollision wird wiederhergestellt, sobald dies ohne Überlappung möglich ist.

Dies ist eine spielorientierte Unterstützung der Physik, keine vollständig unveränderte Simulation aller Trägheitseffekte.

## Ergebnis und Teststand

Der überarbeitete Bucket-Transport wurde im manuellen Spieltest als funktionierend bestätigt. Die zugehörige Aufnahme hält den Entwicklungsstand fest. Anschliessend wurde die Unterstützung auf Pan und Basket übertragen, mit Berücksichtigung der Modellorientierung und des rechteckigen Korbinnenraums.

Die Übertragung allein ist noch kein Nachweis für sämtliche Testfälle dieser beiden Behälter. Schnelle Drehungen, volle Behälter, Herausnehmen, Ausschütten und wiederholtes Ablegen sollten weiterhin systematisch geprüft werden.

## Beitrag und Portfolio-Relevanz

**Game Design:** Festlegen des gewünschten Bediengefühls, Beurteilen von Lösungsversuchen im Playtest und Verwerfen einer technisch möglichen, aber spielerisch unpassenden Verzögerung.

**Technische Entwicklung:** Zusammenspiel von Rigidbody-Physik, Inhaltserkennung, Kollisionsbehandlung und Darstellung. Die Programmierung wurde mit KI-Unterstützung erarbeitet; die Zielsetzung und die Bewertung des Spielgefühls erfolgten durch den Projektentwickler.

**Gestalterischer Kontext:** Der Garten gibt der Interaktion einen nachvollziehbaren Zweck: Ernten und Transportieren verbinden einzelne Objekte zu einer spielbaren Tätigkeit. Diese Fallstudie konzentriert sich auf Interaktionsqualität; die Aufnahme ist kein Nachweis einer abgeschlossenen visuellen Ausarbeitung.

## Nächster Schritt

Eine kurze Vergleichsaufnahme mit identischem Ablauf erstellen: drei Gegenstände einlegen, aufnehmen, laufen, drehen, abstellen und ausschütten. Diesen Ablauf für Bucket, Pan und Basket wiederholen. Für eine spätere Portfolio-Präsentation daraus einen 20–30 Sekunden langen Ausschnitt mit klar sichtbarem Inhalt schneiden.

## Relevante Projektdateien

- [BucketCarryPhysics.cs](../../Assets/Scripts/Interaktion/BucketCarryPhysics.cs) – Transport-Unterstützung und Darstellung.
- [BucketContents.cs](../../Assets/Scripts/Interaktion/BucketContents.cs) – Einbindung der Inhaltsverwaltung und Spieler-Kollisionen.
- [PickupInteractable.cs](../../Assets/Scripts/Interaktion/PickupInteractable.cs) – Aufnehmen, Tragen und Ablegen.

---

[Zur Portfolio-Übersicht](README.md)
