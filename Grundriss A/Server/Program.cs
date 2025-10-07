using LiveFloorServer;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
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
await client.ConnectAsync(mqttOptions, CancellationToken.None);

Console.ForegroundColor = ConsoleColor.Blue;
Console.WriteLine("[i] Verbunden mit test.mosquitto.org:1883");
Console.ResetColor();

var values = new[] { 100, 100, 100, 100, 100, 100, 100 };

client.ApplicationMessageReceivedAsync += async e =>
{
    try
    {
        var topic = e.ApplicationMessage.Topic;
        var parts = topic.Split('/');

        if (parts.Length >= 3 &&
            int.TryParse(parts[2], out var floor) && floor is >= 0 and <= 6)
        {
            var payload = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);
            if (int.TryParse(payload, out var value) && value is >= 0 and <= 100)
            {
                values[floor] = value;

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[✓] MQTT empfangen -> Stockwerk {floor}, Wert {value}");
                Console.ResetColor();

                await webSrv.BroadcastAsync(values);
            }
        }
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(ex);
        Console.ResetColor();
    }
};

await client.SubscribeAsync("building/floor/+/airquality",
                            MqttQualityOfServiceLevel.AtLeastOnce);

Console.ForegroundColor = ConsoleColor.Blue;
Console.WriteLine("[i] Abonniert: building/floor/+/airquality");
Console.ResetColor();

Console.WriteLine();
Console.WriteLine("(ENTER), um das Programm zu beenden …");
Console.ReadLine();
