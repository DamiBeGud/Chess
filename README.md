# Chess Repository

## Deutsch

Dieses Repository besteht aus einem Desktop-Client, einem Multiplayer-Server sowie einem separaten Handout-Bereich für die Projektdokumentation. Der Schwerpunkt liegt auf einer klaren Trennung zwischen lokalem Schachspiel im Client, serverautoritativem Online-Spiel im Backend und der begleitenden Dokumentation. Diese Datei gibt einen kurzen Überblick darüber, was sich in welchem Ordner befindet.

### Ordner

`ChessApplication/` enthält den Desktop-Client. Hier liegen das Domänenmodell, die Schach-Engine, die Avalonia-Benutzeroberfläche, die Persistenz, die KI sowie der Online-Client; die zugehörigen Tests liegen unter `ChessApplication/tests/`.

`MultiplayerServer/` enthält das Backend für Online-Partien. Dort befinden sich die Match-Verwaltung, die Regelprüfung auf Serverseite, die HTTP-Transportebene, der SignalR-Hub und die Server-Tests unter `MultiplayerServer/tests/`.

`Handout/handout/` enthält die eigentliche Projektdokumentation. Dort liegen das Handout in Markdown, die erzeugte PDF und zusätzliche Dateien für die Dokumentationsarbeit.

`Handout/scripts/` enthält die Hilfsskripte für die Dokumentation. Aktuell liegt hier vor allem die Docker-basierte Markdown-zu-PDF-Konvertierung für das Handout.

### Hinweise

Die wichtigsten Einstiegspunkte sind `ChessApplication/Chess.sln` für den Client und `MultiplayerServer/MultiplayerServer.csproj` für den Server. Für die Dokumentation sind `Handout/handout/handout.md` und `Handout/scripts/Dockerfile` die zentralen Dateien. Generierte Build-Artefakte wie `bin/` und `obj/` gehören nicht zur eigentlichen Architektur, sondern entstehen beim Bauen und Testen.

Der `MultiplayerServer` ist in der Azure-Cloud bereitgestellt. Beim Start von `ChessApplication` verbindet sich der Client automatisch mit dieser Cloud-Instanz, damit Online-Funktionen und Multiplayer-Sitzungen ohne zusätzliche manuelle Konfiguration verfügbar sind.

## English

This repository contains a desktop client, a multiplayer server, and a separate handout area for the project documentation. The structure clearly separates local chess gameplay in the client, server-authoritative online play in the backend, and the supporting documentation. This file gives a short overview of what lives in each folder.

### Folders

`ChessApplication/` contains the desktop client. It holds the domain model, the chess engine, the Avalonia user interface, persistence, the AI subsystem, and the online client; the related tests live in `ChessApplication/tests/`.

`MultiplayerServer/` contains the backend for online matches. It includes match lifecycle management, server-side rules validation, the HTTP transport layer, the SignalR hub, and the server tests under `MultiplayerServer/tests/`.

`Handout/handout/` contains the actual project documentation. This is where the Markdown handout, the generated PDF, and supporting documentation files are stored.

`Handout/scripts/` contains the helper tooling for the documentation. At the moment it mainly provides the Docker-based Markdown-to-PDF conversion for the handout.

### Notes

The main entry points are `ChessApplication/Chess.sln` for the client and `MultiplayerServer/MultiplayerServer.csproj` for the server. For the documentation, the key files are `Handout/handout/handout.md` and `Handout/scripts/Dockerfile`. Generated build artifacts such as `bin/` and `obj/` are not part of the core architecture; they are created during builds and test runs.

The `MultiplayerServer` is deployed in Azure Cloud. When `ChessApplication` starts, the client automatically connects to that cloud-hosted server so that online features and multiplayer sessions are available without additional manual configuration.
