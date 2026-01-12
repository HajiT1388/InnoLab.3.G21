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
    exit 1
}

Write-Host "STRG+C = ENDE" -ForegroundColor Green

$rooms = @{
    2 = @("A1.04B","A2.07","A2.12")
    3 = @("A3.06","A3.11")
    4 = @("A4.36")
    5 = @("A5.09","A5.11","A5.18")
    6 = @("A6.09","A6.23","A6.28")
}

function Publish-Metric($floor, $room, $metric, $value){
    & $pub -h test.mosquitto.org -p 1883 `
           -t "building/floor/$floor/room/$room/$metric" `
           -m $value -q 1
}

while ($true) {
    foreach ($floor in $rooms.Keys) {
        foreach ($room in $rooms[$floor]) {
            $aq   = Get-Random -Minimum 0 -Maximum 100
            $co2  = Get-Random -Minimum 0 -Maximum 3000
            $temp = [math]::Round((Get-Random -Minimum 6 -Maximum 40), 1)
            $rh   = Get-Random -Minimum 0 -Maximum 100
            $pres = Get-Random -Minimum 950 -Maximum 1070

            Publish-Metric $floor $room "airquality" $aq
            Publish-Metric $floor $room "co2" $co2
            Publish-Metric $floor $room "temperature" $temp
            Publish-Metric $floor $room "humidity" $rh
            Publish-Metric $floor $room "pressure" $pres
        }
    }

    Start-Sleep -Seconds 15
}