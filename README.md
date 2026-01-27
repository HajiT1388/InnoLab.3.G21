# InnoLab 3 - FIWARE - Gebäude A

Unser Projekt zeigt einen 3D-Gebäudeplan mit Live-Komfortdaten aus MQTT.
Der .NET-Server nimmt Sensordaten entgegen, berechnet daraus Komfortwerte und
liefert die Web-UI samt WebSocket-Updates aus.

## Features
- 3D-Viewer mit GLB-Modellen pro Stockwerk
- Live-Updates über WebSocket (`/ws`)
- MQTT-Import für Raumwerte (CO2, Temperatur, Luftfeuchte, Druck, Airquality)
- Komfortberechnung mit Fallback auf manuelle Scores

## Projektstruktur
- `Grundriss A/Server` - .NET 8 Server (EP: `Program.cs`)
- `Grundriss A/Server/wwwroot` - Frontend (index.html, comfort.js, GLB-Assets)
- `EndlosPublisher.ps1` - Testdaten-Publisher für MQTT
- `docker-compose.yml` - Lokaler Stack (Mosquitto + Server + Publisher)

## Schnellstart (Docker)
```bash
docker compose up --build
```
Danach im Browser öffnen:
```
http://localhost:5000/
```
`__run.bat` auf Windows startet den Stack und öffnet die URL automatisch.

## Lokal ausführen (ohne Docker)
```bash
cd "Grundriss A/Server"
dotnet restore
dotnet run
```
Standardmäßig läuft der Server auf:
```
http://localhost:8080/
```
Der Server verbindet sich per MQTT standardmäßig zu `test.mosquitto.org`.

## Konfiguration
### Server (Umgebungsvariablen)
- `ASPNETCORE_URLS` (Default: `http://+:8080/`)
- `MQTT_HOST` (Default: `test.mosquitto.org`)
- `MQTT_PORT` (Default: `1883`)
- `MQTT_TOPIC_PREFIX` (Default: `building/floor`)

### Publisher (EndlosPublisher.ps1)
- `MQTT_BROKER` (Default: `test.mosquitto.org`)
- `MQTT_PORT` (Default: `1883`)
- `MQTT_TOPIC_PREFIX` (Default: `building/floor`)
- `MQTT_INTERVAL` (Sekunden, Default: `15`)
- `MQTT_QOS` (0..2, Default: `1`)

## MQTT Topic-Format
```
building/floor/{floor}/room/{room}/{metric}
```
`metric`:
`airquality`, `co2`, `temperature`, `humidity`, `pressure`.

Sensorisierte Räume sind in `Grundriss A/Server/Program.cs` hinterlegt
(Floors 2-6, im Format A2.07, A3.11, ...).

## Aufräumen
`__clean_all.bat` stoppt und entfernt alle Docker-Container, Images und Volumes
auf dem System.