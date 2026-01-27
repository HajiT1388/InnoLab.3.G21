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

## 🛠 A. Lokaler Betrieb (Build & Docker Compose)

Bevor das System in die Cloud übertragen wird, erfolgt das Bauen der Images und der lokale Testlauf.

### 1. Build der Images

Die Images werden lokal mit Docker gebaut und anschließend in das Repository (Docker Hub) gepusht.  
Ersetzen Sie `<DOCKER_HUB_USER>` durch Ihren persönlichen Account-Namen.

**Befehle:**

```bash
docker build -t <DOCKER_HUB_USER>/innolab3g21-server:latest . 
docker push <DOCKER_HUB_USER>/innolab3g21-server:latest
```

### 2. Starten der Docker-Container

Im Projektverzeichnis wird die Umgebung mittels Docker Compose gestartet:

```bash
docker-compose up -d
```

### 💡 Wichtiger Hinweis zur Fehlerbehebung

Sollten Container (z. B. eine Datenbank wie `crate-db`) nicht starten, liegt dies oft an zu geringem virtuellem Speicher.

**Fehlermeldung:**

```bash
vm.max_map_count [65530] is too low
```

#### Lösung für Windows (WSL)

Alle Container stoppen und in der PowerShell oder CMD folgende Befehle ausführen:

```bash
wsl -d docker-desktop
sysctl -w vm.max_map_count=262144
exit
```

Danach den Start erneut versuchen:

```bash
docker-compose up -d
```

---

## ☁️ B. Cloud-Bereitstellung (via OKD Weboberfläche)

Die produktive Bereitstellung erfolgt über die grafische Oberfläche der OKD-Konsole.  
Dies ermöglicht eine intuitive Verwaltung der Ressourcen.

### Voraussetzungen

- Vorhandenes Docker Hub Konto mit einem Personal Access Token (PAT)
- Zugriff auf die OKD-Webkonsole mit entsprechenden Berechtigungen im Namensraum

### Durchführungsschritte in der OKD-Oberfläche

1. **Erstellung des Image Pull Secrets**  
   Unter `Workloads -> Secrets` ein neues Secret (z. B. `dockerhub-auth`) vom Typ *Image Pull Secret* anlegen.  
   Docker-Hub-Benutzername und PAT hinterlegen, um Rate-Limits zu umgehen.

2. **Konfiguration der Broker-Einstellungen (ConfigMap)**  
   Über `Workloads -> ConfigMaps` die `mosquitto-config` erstellen.  
   Darin die `mosquitto.conf` definieren, um den Broker-Betrieb im Cluster zu steuern.

3. **Deployment der Container**  
   Die Bereitstellung erfolgt über den Import der YAML-Konfigurationen oder den *Deploy Image*-Dialog.

   **Wichtig:**
   - Das zuvor erstellte `imagePullSecret` muss zugewiesen werden
   - `imagePullPolicy` auf `Always` setzen, um die Synchronität mit Docker Hub zu garantieren

4. **Vernetzung**  
   Über `Networking -> Services` die internen Kommunikationswege definieren  
   und eine Route für den externen Zugriff auf das 3D-Frontend (Port `8080`) erstellen.

---

## 🔄 Migration und Datenbestand

- **Keine Datenmigration erforderlich**  
  Da die Anwendung auf der Verarbeitung von Echtzeit-Sensordaten basiert, müssen keine historischen Datenbestände migriert werden.

- **Dynamische Konfiguration**  
  Die Zuordnung der Sensoren zu den Räumen im 3D-Modell erfolgt dynamisch über die in den Deployments hinterlegten Umgebungsvariablen für die MQTT-Topics.


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