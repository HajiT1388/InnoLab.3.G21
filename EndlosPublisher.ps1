$broker = if ([string]::IsNullOrWhiteSpace($env:MQTT_BROKER)) { "test.mosquitto.org" } else { $env:MQTT_BROKER }
$port = 1883
if (-not [string]::IsNullOrWhiteSpace($env:MQTT_PORT)) {
    $parsedPort = 0
    if ([int]::TryParse($env:MQTT_PORT, [ref]$parsedPort)) { $port = $parsedPort }
}
$topicPrefix = if ([string]::IsNullOrWhiteSpace($env:MQTT_TOPIC_PREFIX)) { "building/floor" } else { $env:MQTT_TOPIC_PREFIX }
$topicSuffix = if ([string]::IsNullOrWhiteSpace($env:MQTT_TOPIC_SUFFIX)) { "airquality" } else { $env:MQTT_TOPIC_SUFFIX }
$topicPrefix = $topicPrefix.Trim('/').Trim()
$topicSuffix = $topicSuffix.Trim('/').Trim()
$intervalSeconds = 3
if (-not [string]::IsNullOrWhiteSpace($env:MQTT_INTERVAL)) {
    $parsedInterval = 0
    if ([int]::TryParse($env:MQTT_INTERVAL, [ref]$parsedInterval)) { $intervalSeconds = [Math]::Max(1, $parsedInterval) }
}
$floorCount = 7
if (-not [string]::IsNullOrWhiteSpace($env:MQTT_FLOOR_COUNT)) {
    $parsedFloors = 0
    if ([int]::TryParse($env:MQTT_FLOOR_COUNT, [ref]$parsedFloors)) { $floorCount = [Math]::Max(1, $parsedFloors) }
}
$qos = 1
if (-not [string]::IsNullOrWhiteSpace($env:MQTT_QOS)) {
    $parsedQos = 0
    if ([int]::TryParse($env:MQTT_QOS, [ref]$parsedQos)) { $qos = [Math]::Min([Math]::Max(0, $parsedQos), 2) }
}

Write-Host ("[i] MQTT Publisher -> {0}:{1}, Topic '{2}/<floor>/{3}', Interval {4}s" -f $broker, $port, $topicPrefix, $topicSuffix, $intervalSeconds)

while ($true) {
    foreach ($floor in 0..($floorCount - 1)) {
        $value = Get-Random -Minimum 0 -Maximum 101
        $topic = $topicPrefix
        if (-not [string]::IsNullOrWhiteSpace($topic)) { $topic = "$topic/$floor" }
        else { $topic = "$floor" }
        if (-not [string]::IsNullOrWhiteSpace($topicSuffix)) { $topic = "$topic/$topicSuffix" }

        try {
            mosquitto_pub -h $broker -p $port `
                          -t $topic -m $value -q $qos | Out-Null
            Write-Host "[✓] Publiziert -> $topic = $value"
        }
        catch {
            Write-Warning "MQTT publish fehlgeschlagen: $($_.Exception.Message)"
        }
    }
    Start-Sleep -Seconds $intervalSeconds
}