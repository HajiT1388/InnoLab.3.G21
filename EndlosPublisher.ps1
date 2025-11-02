while ($true) {
    0..6 | ForEach-Object {
        $value = Get-Random -Minimum 0 -Maximum 100
        mosquitto_pub -h test.mosquitto.org -p 1883 `
                      -t "building/floor/$_/airquality" `
                      -m $value -q 1
    }
    Start-Sleep -Seconds 3
}