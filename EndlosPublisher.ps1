$broker = if ([string]::IsNullOrWhiteSpace($env:MQTT_BROKER)) { "test.mosquitto.org" } else { $env:MQTT_BROKER }
$port = 1883
if (-not [string]::IsNullOrWhiteSpace($env:MQTT_PORT)) {
    $parsedPort = 0
    if ([int]::TryParse($env:MQTT_PORT, [ref]$parsedPort)) { $port = $parsedPort }
}
$topicPrefix = if ([string]::IsNullOrWhiteSpace($env:MQTT_TOPIC_PREFIX)) { "building/floor" } else { $env:MQTT_TOPIC_PREFIX }
$topicPrefix = $topicPrefix.Trim('/').Trim()
$intervalSeconds = 15

if (-not [string]::IsNullOrWhiteSpace($env:MQTT_INTERVAL)) {
    $parsedInterval = 0
    if ([int]::TryParse($env:MQTT_INTERVAL, [ref]$parsedInterval)) { $intervalSeconds = [Math]::Max(1, $parsedInterval) }
}

$qos = 1
if (-not [string]::IsNullOrWhiteSpace($env:MQTT_QOS)) {
    $parsedQos = 0
    if ([int]::TryParse($env:MQTT_QOS, [ref]$parsedQos)) { $qos = [Math]::Min([Math]::Max(0, $parsedQos), 2) }
}

# Room setup for each floor
$rooms = @{
    2 = "A1.04B", "A2.07", "A2.12"
    3 = "A3.06", "A3.11"
    4 = "A4.36"
    5 = "A5.09", "A5.11", "A5.18"
    6 = "A6.09", "A6.23", "A6.28"
}

Write-Host ("[i] MQTT Publisher -> {0}:{1}, Topic '{2}/<floor>/room/<room>/<metric}', Interval {3}s" -f $broker, $port, $topicPrefix, $intervalSeconds)

# 3. Die Publish-Metric Funktion (Direktaufruf mosquitto_pub)
function Publish-Metric($floor, $room, $metric, $value) {
    # Erzeugt den Pfad: building/floor/$floor/room/$room/$metric
    $topic = "$topicPrefix/$floor/room/$room/$metric"
    # Punkt statt Komma für C# (Wichtig für Temperatur/Druck)
    $valString = $value.ToString().Replace(',', '.') 

    try {
        # Direkter Aufruf ohne Variable
        mosquitto_pub -h $broker -p $port -t $topic -m $valString -q $qos
        Write-Host "[✓] Published -> $topic = $valString" -ForegroundColor Green
    }
    catch {
        Write-Warning "MQTT publish failed for $topic : $($_.Exception.Message)"
    }
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
    Start-Sleep -Seconds $intervalSeconds
}