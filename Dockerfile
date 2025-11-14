FROM mcr.microsoft.com/powershell:latest

RUN apt-get update && \
    apt-get install -y mosquitto-clients && \
    rm -rf /var/lib/apt/lists/*

WORKDIR /app

COPY EndlosPublisher.ps1 .

CMD ["pwsh", "-File", "EndlosPublisher.ps1"]