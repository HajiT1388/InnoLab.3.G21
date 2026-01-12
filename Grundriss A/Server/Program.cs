using LiveFloorServer;
using MQTTnet;
using MQTTnet.Client;
using System.Globalization;
using System.Text;

const int FloorCount = 7;

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

var knownRoomsPerFloor = new Dictionary<int, string[]>
{
    [2] = new[] { "A1.04B", "A2.07", "A2.12" },
    [3] = new[] { "A3.06", "A3.11" },
    [4] = new[] { "A4.36" },
    [5] = new[] { "A5.09", "A5.11", "A5.18" },
    [6] = new[] { "A6.09", "A6.23", "A6.28" }
};

var roomStates = CreateRoomStates(knownRoomsPerFloor);

await BroadcastSnapshotAsync(webSrv, roomStates);

client.ApplicationMessageReceivedAsync += async e =>
{
    try
    {
        var topic = e.ApplicationMessage.Topic;
        var parts = topic.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var payload = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);

        if (!TryParseRoomTopic(parts, out var floor, out var roomName, out var metric))
            return;

        if (!knownRoomsPerFloor.TryGetValue(floor, out var known) ||
            Array.IndexOf(known, roomName) < 0)
        {
            return;
        }

        var state = GetOrCreateState(roomStates, floor, roomName);
        if (!UpdateStateFromMetric(state, metric, payload))
            return;

        LogUpdate(floor, roomName, metric, payload);

        await BroadcastSnapshotAsync(webSrv, roomStates);
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
        f.WithTopic("building/floor/+/room/+/+");
        f.WithAtLeastOnceQoS();
    })
    .Build();

await client.SubscribeAsync(subOptions);

Console.ForegroundColor = ConsoleColor.Blue;
Console.WriteLine("[i] Abonniert: building/floor/+/room/+/+");
Console.ResetColor();

Console.WriteLine();
Console.WriteLine("(ENTER), um das Programm zu beenden …");
Console.ReadLine();

static Dictionary<int, Dictionary<string, RoomState>> CreateRoomStates(Dictionary<int, string[]> knownRoomsPerFloor)
{
    var roomStates = new Dictionary<int, Dictionary<string, RoomState>>();
    foreach (var kv in knownRoomsPerFloor)
    {
        var floorMap = new Dictionary<string, RoomState>(StringComparer.OrdinalIgnoreCase);
        foreach (var room in kv.Value)
        {
            floorMap[room] = new RoomState();
        }
        roomStates[kv.Key] = floorMap;
    }

    return roomStates;
}

static bool TryParseRoomTopic(string[] parts, out int floor, out string roomName, out string metric)
{
    floor = 0;
    roomName = string.Empty;
    metric = string.Empty;

    if (parts.Length == 6 &&
        parts[0].Equals("building", StringComparison.OrdinalIgnoreCase) &&
        parts[1].Equals("floor", StringComparison.OrdinalIgnoreCase) &&
        int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out floor) &&
        parts[3].Equals("room", StringComparison.OrdinalIgnoreCase))
    {
        roomName = parts[4];
        metric = parts[5];
        return true;
    }

    return false;
}

static bool TryParseDouble(string payload, out double value)
{
    if (double.TryParse(payload, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        return true;

    if (double.TryParse(payload, NumberStyles.Float, CultureInfo.CurrentCulture, out value))
        return true;

    var normalized = payload.Replace(',', '.');
    return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
}

static bool UpdateStateFromMetric(RoomState state, string metric, string payload)
{
    var key = metric.ToLowerInvariant();
    switch (key)
    {
        case "airquality":
            if (!int.TryParse(payload, NumberStyles.Integer, CultureInfo.InvariantCulture, out var aq))
                return false;
            state.ManualScore = Math.Clamp(aq, 0, 100);
            return true;

        case "co2":
            if (TryParseDouble(payload, out var co2))
            {
                state.Co2 = Math.Clamp(co2, 0, 3000);
                state.EnableCo2 = true;
                return true;
            }
            return false;

        case "temp":
        case "temperature":
            if (TryParseDouble(payload, out var temp))
            {
                state.Temp = Math.Clamp(temp, 6, 40);
                state.EnableTemp = true;
                return true;
            }
            return false;

        case "rh":
        case "humidity":
            if (TryParseDouble(payload, out var rh))
            {
                state.Rh = Math.Clamp(rh, 0, 100);
                state.EnableRh = true;
                return true;
            }
            return false;

        case "pres":
        case "pressure":
            if (TryParseDouble(payload, out var pres))
            {
                state.Pres = Math.Clamp(pres, 950, 1070);
                state.EnablePres = true;
                return true;
            }
            return false;

        default:
            return false;
    }
}

static RoomState GetOrCreateState(Dictionary<int, Dictionary<string, RoomState>> roomStates, int floor, string roomName)
{
    if (!roomStates.TryGetValue(floor, out var floorMap))
    {
        floorMap = new Dictionary<string, RoomState>(StringComparer.OrdinalIgnoreCase);
        roomStates[floor] = floorMap;
    }

    if (!floorMap.TryGetValue(roomName, out var state))
    {
        state = new RoomState();
        floorMap[roomName] = state;
    }

    return state;
}

static async Task BroadcastSnapshotAsync(WebServer webSrv, Dictionary<int, Dictionary<string, RoomState>> roomStates)
{
    var snapshot = BuildSnapshot(roomStates);
    await webSrv.BroadcastAsync(snapshot.floors, snapshot.roomScores, snapshot.details);
}

static (int[] floors, Dictionary<int, Dictionary<string, int>> roomScores, Dictionary<int, Dictionary<string, RoomDetailPayload>> details)
    BuildSnapshot(Dictionary<int, Dictionary<string, RoomState>> roomStates)
{
    var roomScores = new Dictionary<int, Dictionary<string, int>>();
    var details = new Dictionary<int, Dictionary<string, RoomDetailPayload>>();

    foreach (var floorKv in roomStates)
    {
        var floorScores = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var floorDetails = new Dictionary<string, RoomDetailPayload>(StringComparer.OrdinalIgnoreCase);

        foreach (var roomKv in floorKv.Value)
        {
            var result = ComputeResult(roomKv.Value);
            var score = result?.Score ?? Math.Clamp(roomKv.Value.ManualScore ?? 0, 0, 100);

            floorScores[roomKv.Key] = score;
            floorDetails[roomKv.Key] = new RoomDetailPayload
            {
                Co2 = roomKv.Value.Co2,
                Temp = roomKv.Value.Temp,
                Rh = roomKv.Value.Rh,
                Pres = roomKv.Value.Pres,
                Enabled = new ComfortEnabled
                {
                    Co2 = roomKv.Value.EnableCo2 && roomKv.Value.Co2.HasValue,
                    Temp = roomKv.Value.EnableTemp && roomKv.Value.Temp.HasValue,
                    Rh = roomKv.Value.EnableRh && roomKv.Value.Rh.HasValue,
                    Pres = roomKv.Value.EnablePres && roomKv.Value.Pres.HasValue
                },
                Result = result,
                ScoreSource = result != null ? "sensor" : "manual"
            };
        }

        roomScores[floorKv.Key] = floorScores;
        details[floorKv.Key] = floorDetails;
    }

    var floorValues = Enumerable.Repeat(100, FloorCount).ToArray();
    foreach (var kv in roomScores)
    {
        if (kv.Key >= 0 && kv.Key < FloorCount && kv.Value.Count > 0)
        {
            floorValues[kv.Key] = (int)Math.Round(kv.Value.Values.Average());
        }
    }

    return (floorValues, roomScores, details);
}

static ComfortResult? ComputeResult(RoomState state)
{
    var enabled = new ComfortEnabled
    {
        Co2 = state.EnableCo2 && state.Co2.HasValue,
        Temp = state.EnableTemp && state.Temp.HasValue,
        Rh = state.EnableRh && state.Rh.HasValue,
        Pres = state.EnablePres && state.Pres.HasValue
    };

    if (!enabled.Co2 && !enabled.Temp && !enabled.Rh && !enabled.Pres)
        return null;

    var inputs = new ComfortInputs
    {
        Co2 = state.Co2,
        Temp = state.Temp,
        Rh = state.Rh,
        Pres = state.Pres,
        Enabled = enabled
    };

    return ComfortCalculator.Compute(inputs);
}

static void LogUpdate(int floor, string roomName, string metric, string payload)
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.Write("[V] ");
    Console.ResetColor();
    Console.WriteLine($"{floor}OG, Raum {roomName}, {metric}: {payload}");
}