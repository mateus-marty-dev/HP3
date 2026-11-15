# HP3 Development Log

## 2026-11-15 – Zombie bringt Tomatenkorb zur Küche

Die eng gewundene Steintreppe wurde für die NPC-Navigation in Unity 6 untersucht. Die Navigation wurde als NavMeshSurface in der Szene visualisiert und gebacken; Agent-Radius, Tür, Voxelgrösse und Kollisionsgeometrie wurden als mögliche Ursachen getestet. Ein eigens zugewiesener `KitchenBasketDropPoint` brachte den Zombie erstmals auf die Treppe. Die spätere Runtime-Diagnose offenbarte einen gültigen `PathComplete` bei unbekannter Restdistanz (`Infinity`) und vorhandener Geschwindigkeit: Eine 15-Sekunden-Blockadeprüfung brach die Coroutine fälschlich ab. Die Fortschrittsprüfung wurde auf Positionsänderungen des NavMeshAgents umgestellt; der Nutzer bestätigte anschliessend den erfolgreichen Durchlauf. Der aktuelle Script-Stand im Repository ist noch nicht mit dem getesteten lokalen Stand synchronisiert.

[Portfolio-Fallstudie mit Versuchen, Fehlerlog und Bildliste](../Portfolio/2026-11-15-zombie-wendeltreppe-navmesh.md)

---

Portfolio-Auswertungen, Entwicklungssessions und ihre Medien sind zentral in der [Portfolio-Übersicht](../Portfolio/README.md) abgelegt. Dieses Log enthält chronologische Entwicklungsnotizen.

---

## 2026-08-05

### Project Setup

- Created Unity 6 project.
- Connected project with Git.
- Connected project with GitHub.
- Set up Cursor as development environment.
- Created project documentation structure.
- Wrote initial README.

### Notes

The project officially begins today.

Goal:
Build a small but polished gameplay prototype focusing on environment interaction inside Petrusilius' castle.

---

## Next Steps

- Create first castle blockout.
- Build player controller.
- Implement interaction system.
