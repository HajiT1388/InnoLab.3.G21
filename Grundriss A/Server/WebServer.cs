using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LiveFloorServer
{
    public sealed class WebServer
    {
        private readonly string _prefix;
        private readonly string _webRoot;

        private readonly ConcurrentDictionary<Guid, WebSocket> _clients = new();

        private const int FloorCount = 7;
        private int[] _currentFloors = Enumerable.Repeat(100, FloorCount).ToArray();
        private Dictionary<int, Dictionary<string, int>> _currentRooms = new();
        private Dictionary<int, Dictionary<string, RoomDetailPayload>> _currentRoomDetails = new();

        private readonly SemaphoreSlim _broadcastLock = new(1, 1);

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private static readonly Dictionary<string, string> _mime = new(StringComparer.OrdinalIgnoreCase)
        {
            [".html"] = "text/html; charset=utf-8",
            [".css"] = "text/css;  charset=utf-8",
            [".js"] = "text/javascript; charset=utf-8",
            [".png"] = "image/png",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".gif"] = "image/gif",
            [".svg"] = "image/svg+xml",
            [".glb"] = "model/gltf-binary",
            [".gltf"] = "model/gltf+json",
            [".json"] = "application/json",
            [".wasm"] = "application/wasm",
            [".ico"] = "image/x-icon"
        };

        public WebServer(string prefix, string webRoot)
        {
            _prefix = prefix;
            _webRoot = webRoot;
            Directory.CreateDirectory(_webRoot);
        }

        public async Task RunAsync()
        {
            Log("Starte HTTP-Server unter " + _prefix);
            Log("Webroot-Ordner: " + _webRoot);

            using var listener = new HttpListener();
            listener.Prefixes.Add(_prefix);
            listener.Start();

            while (true)
            {
                var ctx = await listener.GetContextAsync();

                if (ctx.Request.IsWebSocketRequest &&
                    ctx.Request.RawUrl!.Equals("/ws", StringComparison.OrdinalIgnoreCase))
                {
                    _ = HandleWebSocketAsync(ctx);
                }
                else
                {
                    _ = ServeStaticAsync(ctx);
                }
            }
        }

        public async Task BroadcastAsync(
            int[] floors,
            Dictionary<int, Dictionary<string, int>> rooms,
            Dictionary<int, Dictionary<string, RoomDetailPayload>> roomDetails)
        {
            await _broadcastLock.WaitAsync();
            try
            {
                _currentFloors = floors.ToArray();
                _currentRooms = rooms.ToDictionary(
                    x => x.Key,
                    x => x.Value.ToDictionary(i => i.Key, i => i.Value, StringComparer.OrdinalIgnoreCase)
                );
                _currentRoomDetails = roomDetails.ToDictionary(
                    x => x.Key,
                    x => x.Value.ToDictionary(i => i.Key, i => i.Value, StringComparer.OrdinalIgnoreCase)
                );

                foreach (var kv in _clients)
                    await SendAsync(kv.Value, _currentFloors, _currentRooms, _currentRoomDetails);
            }
            finally
            {
                _broadcastLock.Release();
            }
        }

        private async Task HandleWebSocketAsync(HttpListenerContext ctx)
        {
            WebSocket ws;
            try
            {
                ws = (await ctx.AcceptWebSocketAsync(null)).WebSocket;
            }
            catch
            {
                ctx.Response.StatusCode = 500;
                ctx.Response.Close();
                return;
            }

            var id = Guid.NewGuid();
            _clients.TryAdd(id, ws);
            Log($"WebSocket-Client verbunden ({_clients.Count})");

            await SendAsync(ws, _currentFloors, _currentRooms, _currentRoomDetails);

            var buffer = new byte[1];
            try
            {
                while (ws.State == WebSocketState.Open)
                    await ws.ReceiveAsync(buffer, CancellationToken.None);
            }
            catch
            {
            }
            finally
            {
                _clients.TryRemove(id, out _);
                try { await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None); } catch { }
                Log($"WebSocket-Client getrennt ({_clients.Count})");
            }
        }

        private static async Task SendAsync(
            WebSocket ws,
            int[] floors,
            Dictionary<int, Dictionary<string, int>> rooms,
            Dictionary<int, Dictionary<string, RoomDetailPayload>> roomDetails)
        {
            if (ws.State != WebSocketState.Open) return;

            var payload = new
            {
                values = floors,
                rooms,
                roomDetails
            };

            var json = JsonSerializer.Serialize(payload, JsonOptions);
            var bytes = Encoding.UTF8.GetBytes(json);

            try
            {
                await ws.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch
            {
            }
        }

        private async Task ServeStaticAsync(HttpListenerContext ctx)
        {
            var urlPath = ctx.Request.Url!.AbsolutePath;
            if (urlPath == "/" || string.IsNullOrWhiteSpace(urlPath))
                urlPath = "/index.html";

            var safePath = urlPath.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
            var fullPath = Path.GetFullPath(Path.Combine(_webRoot, safePath));

            if (!fullPath.StartsWith(_webRoot, StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath))
            {
                ctx.Response.StatusCode = 404;
                ctx.Response.Close();
                return;
            }

            try
            {
                if (_mime.TryGetValue(Path.GetExtension(fullPath), out var ct))
                    ctx.Response.ContentType = ct;

                await using var fs = File.OpenRead(fullPath);
                ctx.Response.ContentLength64 = fs.Length;
                await fs.CopyToAsync(ctx.Response.OutputStream);
                ctx.Response.StatusCode = 200;
            }
            catch
            {
                ctx.Response.StatusCode = 500;
            }
            finally
            {
                ctx.Response.Close();
            }
        }

        private static void Log(string txt)
        {
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.Write("[i] ");
            Console.ResetColor();
            Console.WriteLine(txt);
        }
    }
}