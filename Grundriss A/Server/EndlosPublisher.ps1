# EndlosPublisher.ps1
# nur Räume, alle 5 Sekunden
# bleibt offen, wenn mosquitto_pub fehlt

$possible = @(
    "C:\Program Files\mosquitto\mosquitto_pub.exe",
    "C:\Program Files (x86)\mosquitto\mosquitto_pub.exe",
    "mosquitto_pub"
)

$pub = $null
foreach ($p in $possible) {
    $cmd = Get-Command $p -ErrorAction SilentlyContinue
    if ($cmd) { $pub = $cmd.Source; break }
}

if (-not $pub) {
    Write-Host "mosquitto_pub wurde nicht gefunden." -ForegroundColor Red
    Write-Host "Bitte installieren oder Pfad im Skript anpassen." -ForegroundColor Red
    Read-Host "RETURN zum Beenden"
    exit 1
}

Write-Host "Publisher läuft ... (STRG+C zum Abbrechen)" -ForegroundColor Green

while ($true) {

    # 2. Stock
    "A1.04B","A2.07","A2.12" | ForEach-Object {
        $value = Get-Random -Minimum 0 -Maximum 100
        & $pub -h test.mosquitto.org -p 1883 `
               -t "building/floor/2/room/$_/airquality" `
               -m $value -q 1
    }

    # 3. Stock
    "A3.06","A3.11" | ForEach-Object {
        $value = Get-Random -Minimum 0 -Maximum 100
        & $pub -h test.mosquitto.org -p 1883 `
               -t "building/floor/3/room/$_/airquality" `
               -m $value -q 1
    }

    # 4. Stock
    "A4.36" | ForEach-Object {
        $value = Get-Random -Minimum 0 -Maximum 100
        & $pub -h test.mosquitto.org -p 1883 `
               -t "building/floor/4/room/$_/airquality" `
               -m $value -q 1
    }

    # 5. Stock
    "A5.09","A5.11","A5.18" | ForEach-Object {
        $value = Get-Random -Minimum 0 -Maximum 100
        & $pub -h test.mosquitto.org -p 1883 `
               -t "building/floor/5/room/$_/airquality" `
               -m $value -q 1
    }

    # 6. Stock
    "A6.09","A6.23","A6.28" | ForEach-Object {
        $value = Get-Random -Minimum 0 -Maximum 100
        & $pub -h test.mosquitto.org -p 1883 `
               -t "building/floor/6/room/$_/airquality" `
               -m $value -q 1
    }

    Start-Sleep -Seconds 5
}
