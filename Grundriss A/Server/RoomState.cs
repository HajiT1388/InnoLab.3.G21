using System.Text.Json.Serialization;

namespace LiveFloorServer
{
    public sealed class RoomState
    {
        public double? Co2 { get; set; }
        public double? Temp { get; set; }
        public double? Rh { get; set; }
        public double? Pres { get; set; }

        public bool EnableCo2 { get; set; } = true;
        public bool EnableTemp { get; set; } = false;
        public bool EnableRh { get; set; } = true;
        public bool EnablePres { get; set; } = true;

        public int? ManualScore { get; set; } = 100;
    }

    public sealed class RoomDetailPayload
    {
        [JsonPropertyName("co2")]
        public double? Co2 { get; init; }

        [JsonPropertyName("temp")]
        public double? Temp { get; init; }

        [JsonPropertyName("rh")]
        public double? Rh { get; init; }

        [JsonPropertyName("pres")]
        public double? Pres { get; init; }

        [JsonPropertyName("enabled")]
        public ComfortEnabled Enabled { get; init; } = new();

        [JsonPropertyName("result")]
        public ComfortResult? Result { get; init; }

        [JsonPropertyName("scoreSource")]
        public string? ScoreSource { get; init; }
    }
}