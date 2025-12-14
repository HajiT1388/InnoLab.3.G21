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
$intervalSeconds = 5
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

# Room setup for each floor
$rooms = @{
    2 = "A1.04B", "A2.07", "A2.12"
    3 = "A3.06", "A3.11"
    4 = "A4.36"
    5 = "A5.09", "A5.11", "A5.18"
    6 = "A6.09", "A6.23", "A6.28"
}

Write-Host ("[i] MQTT Publisher -> {0}:{1}, Topic '{2}/<floor>/{3}', Interval {4}s" -f $broker, $port, $topicPrefix, $topicSuffix, $intervalSeconds)

while ($true) {
    foreach ($floor in $rooms.Keys) {
        foreach ($room in $rooms[$floor]) {
            $value = Get-Random -Minimum 350 -Maximum 2000
            $topic = "$topicPrefix/$floor/$room/$topicSuffix"
            
            try {
                mosquitto_pub -h $broker -p $port `
                              -t $topic -m $value -q $qos | Out-Null
                Write-Host "[✓] Published -> $topic = $value"
            }
            catch {
                Write-Warning "MQTT publish failed: $($_.Exception.Message)"
            }
        }
    }
    Start-Sleep -Seconds $intervalSeconds
}