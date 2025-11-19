using LiveFloorServer;
using MQTTnet;
using MQTTnet.Client;
using System.Text;

var prefix = "http://localhost:5000/";
var webRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
var webSrv = new WebServer(prefix, webRoot);
_ = webSrv.RunAsync();

var factory = new MqttFactory();
var mqttOptions = new MqttClientOptionsBuilder()
    .WithTcpServer("test.mosquitto.org", 1883)
    .WithClientId($"livefloor-{Guid.NewGuid()}")
    .Build();

var client = factory.CreateMqttClient();
await client.ConnectAsync(mqttOptions);

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
        var topic = e.ApplicationMessage.Topic;
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

Console.WriteLine();
Console.WriteLine("(ENTER), um das Programm zu beenden …");
Console.ReadLine();
