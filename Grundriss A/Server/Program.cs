using LiveFloorServer;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
using System.Text;
using System.Threading;

static string GetListenerPrefix()
{
    const string defaultUrl = "http://+:8080/";
    var urls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
    if (string.IsNullOrWhiteSpace(urls))
        return defaultUrl;

    var first = urls.Split(';', StringSplitOptions.RemoveEmptyEntries)
                    .Select(u => u.Trim())
                    .FirstOrDefault(u => !string.IsNullOrWhiteSpace(u));

    if (string.IsNullOrEmpty(first))
        return defaultUrl;

    return first.EndsWith('/') ? first : $"{first}/";
}

static string GetEnv(string key, string fallback) =>
    string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(key))
        ? fallback
        : Environment.GetEnvironmentVariable(key)!;

static int GetEnvInt(string key, int fallback) =>
    int.TryParse(Environment.GetEnvironmentVariable(key), out var result) ? result : fallback;

static string NormalizeSegment(string value) =>
    string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().Trim('/');

static string CombineTopic(params string[] segments)
{
    var cleaned = segments
        .Where(s => !string.IsNullOrWhiteSpace(s))
        .Select(s => s!.Trim('/'));
    return string.Join('/', cleaned);
}

var prefix = GetListenerPrefix();
var webRoot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
var webSrv = new WebServer(prefix, webRoot);
_ = webSrv.RunAsync();

var mqttHost = GetEnv("MQTT_HOST", "test.mosquitto.org");
var mqttPort = GetEnvInt("MQTT_PORT", 1883);
var topicPrefix = NormalizeSegment(GetEnv("MQTT_TOPIC_PREFIX", "building/floor"));
var topicSuffix = NormalizeSegment(GetEnv("MQTT_TOPIC_SUFFIX", "airquality"));
var subscriptionTopic = CombineTopic(topicPrefix, "+", topicSuffix);

Console.ForegroundColor = ConsoleColor.Blue;
Console.WriteLine($"[i] MQTT-Ziel: {mqttHost}:{mqttPort}, Topic {subscriptionTopic}");
Console.ResetColor();

var factory = new MqttFactory();
var mqttOptions = new MqttClientOptionsBuilder()
    .WithTcpServer(mqttHost, mqttPort)
    .WithClientId($"livefloor-{Guid.NewGuid()}")
    .Build();

var client = factory.CreateMqttClient();
await client.ConnectAsync(mqttOptions, CancellationToken.None);
var connectionLock = new SemaphoreSlim(1, 1);

Console.ForegroundColor = ConsoleColor.Blue;
Console.WriteLine("[i] Verbunden mit test.mosquitto.org:1883");
Console.ResetColor();

var floorValues = new[] { 100, 100, 100, 100, 100, 100, 100 };

var knownRoomsPerFloor = new Dictionary<int, string[]>
{
    [2] = new[] { "A1.04B", "A2.07", "A2.12" },
    [3] = new[] { "A3.06", "A3.11" },
    [4] = new[] { "A4.36" },
    [5] = new[] { "A5.09", "A5.11", "A5.18" },
    [6] = new[] { "A6.09", "A6.23", "A6.28" }
};

var roomValues = new Dictionary<int, Dictionary<string, int>>();
foreach (var kv in knownRoomsPerFloor)
{
    var d = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    foreach (var r in kv.Value)
        d[r] = 100;
    roomValues[kv.Key] = d;
}

await webSrv.BroadcastAsync(floorValues, roomValues);

client.ApplicationMessageReceivedAsync += async e =>
{
    try
    {
        var topic = e.ApplicationMessage.Topic ?? string.Empty;
        var parts = topic.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var payload = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);

        if (parts.Length == 6 &&
            parts[0] == "building" &&
            parts[1] == "floor" &&
            int.TryParse(parts[2], out var floor) &&
            parts[3] == "room" &&
            parts[5] == "airquality")
        {
            if (floor < 2 || floor > 6)
                return;

            if (!int.TryParse(payload, out var value) || value is < 0 or > 100)
                return;

            var roomName = parts[4];

            if (!knownRoomsPerFloor.TryGetValue(floor, out var known) ||
                Array.IndexOf(known, roomName) < 0)
            {
                return;
            }

            roomValues[floor][roomName] = value;

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[V] {floor}OG, Raum {roomName}: {value}");
            Console.ResetColor();

            await webSrv.BroadcastAsync(floorValues, roomValues);
        }
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(ex);
        Console.ResetColor();
    }
};

var subOptions = factory.CreateSubscribeOptionsBuilder()
    .WithTopicFilter(f =>
    {
        f.WithTopic("building/floor/+/room/+/airquality");
        f.WithAtLeastOnceQoS();
    })
    .Build();

await client.SubscribeAsync(subOptions);

Console.ForegroundColor = ConsoleColor.Blue;
Console.WriteLine("[i] Abonniert: building/floor/+/room/+/airquality");
Console.ResetColor();
client.DisconnectedAsync += async e =>
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    var reason = e.ReasonString ?? e.Exception?.Message ?? "unbekannter Grund";
    Console.WriteLine($"[!] MQTT-Verbindung getrennt: {reason}");
    Console.ResetColor();

    await ConnectAndSubscribeAsync();
};

await ConnectAndSubscribeAsync();

Console.WriteLine();
Console.WriteLine("Server läuft... (STRG+C zum Beenden)");
await Task.Delay(-1);

async Task ConnectAndSubscribeAsync()
{
    await connectionLock.WaitAsync();
    try
    {
        if (client.IsConnected)
            return;

        await ConnectWithRetryAsync();
        await client.SubscribeAsync(subscriptionTopic, MqttQualityOfServiceLevel.AtLeastOnce);

        Console.ForegroundColor = ConsoleColor.Blue;
        Console.WriteLine($"[i] Abonniert: {subscriptionTopic}");
        Console.ResetColor();
    }
    finally
    {
        connectionLock.Release();
    }
}

async Task ConnectWithRetryAsync()
{
    var attempt = 0;
    var delay = TimeSpan.FromSeconds(2);

    while (true)
    {
        attempt++;
        try
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await client.ConnectAsync(mqttOptions, timeoutCts.Token);

            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine($"[i] Verbunden mit {mqttHost}:{mqttPort}");
            Console.ResetColor();
            return;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[!] MQTT-Verbindungsversuch {attempt} fehlgeschlagen: {ex.Message}");
            Console.ResetColor();

            await Task.Delay(delay);
            var nextSeconds = Math.Min(delay.TotalSeconds * 2, 30);
            delay = TimeSpan.FromSeconds(nextSeconds);
        }
    }
}