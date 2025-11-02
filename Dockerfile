# Verwende offizielles PowerShell-Image
FROM mcr.microsoft.com/powershell:latest

# Mosquitto Clients installieren
RUN apt-get update && \
    apt-get install -y mosquitto-clients && \
    rm -rf /var/lib/apt/lists/*

# Arbeitsverzeichnis setzen
WORKDIR /app

# PS1-Skript kopieren
COPY EndlosPublisher.ps1 .

# Standardkommando
CMD ["pwsh", "-File", "EndlosPublisher.ps1"]

