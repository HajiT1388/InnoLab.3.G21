using System.Text.Json.Serialization;

namespace LiveFloorServer
{
    public sealed class ComfortEnabled
    {
        [JsonPropertyName("co2")]
        public bool Co2 { get; init; } = true;

        [JsonPropertyName("temp")]
        public bool Temp { get; init; } = false;

        [JsonPropertyName("rh")]
        public bool Rh { get; init; } = true;

        [JsonPropertyName("pres")]
        public bool Pres { get; init; } = true;
    }

    public sealed class ComfortPart
    {
        [JsonPropertyName("bad")]
        public double Bad { get; init; }

        [JsonPropertyName("score")]
        public int Score { get; init; }
    }

    public sealed class ComfortResult
    {
        [JsonPropertyName("score")]
        public int Score { get; init; }

        [JsonPropertyName("risk")]
        public double Risk { get; init; }

        [JsonPropertyName("label")]
        public string Label { get; init; } = "-";

        [JsonPropertyName("hint")]
        public string Hint { get; init; } = "Keine Werte aktiv.";

        [JsonPropertyName("parts")]
        public Dictionary<string, ComfortPart> Parts { get; init; } = new(StringComparer.OrdinalIgnoreCase);

        [JsonPropertyName("reasons")]
        public List<string> Reasons { get; init; } = new();
    }

    public sealed class ComfortInputs
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
    }

    public static class ComfortCalculator
    {
        private static double Clamp(double x, double a, double b) => Math.Min(b, Math.Max(a, x));

        private static double BadCO2(double ppm)
        {
            var p = Clamp(ppm, 0, 3000);
            if (p <= 600) return 0;
            var z = Clamp((p - 600) / 1400, 0, 1.6);
            return Clamp(1 - Math.Exp(-3.5 * Math.Pow(z, 3.0)), 0, 1);
        }

        private static double BadRH(double rh)
        {
            var h = Clamp(rh, 0, 100);
            if (h is >= 30 and <= 50) return 0;

            if (h < 30)
            {
                var d = 30 - h;
                var z = Clamp(d / 15, 0, 2.0);
                return Clamp(1 - Math.Exp(-3.0 * Math.Pow(z, 3.0)), 0, 1);
            }
            else
            {
                var d = h - 50;
                var z = Clamp(d / 20, 0, 2.0);
                return Clamp(1 - Math.Exp(-2.3 * Math.Pow(z, 3.0)), 0, 1);
            }
        }

        private static double BadTempC(double t)
        {
            var x = Clamp(t, 6, 40);
            var d = Math.Abs(x - 22.5);

            var z1 = Clamp(Math.Max(0, d - 2.5) / 10, 0, 2.0);
            var z2 = Clamp(Math.Max(0, d - 5.5) / 10, 0, 2.0);

            var baseVal = 1 - Math.Exp(-1.8 * Math.Pow(z1, 2.0));
            var extra = 1 - Math.Exp(-2.2 * Math.Pow(z2, 3.0));

            return Clamp(1 - (1 - baseVal) * (1 - extra), 0, 1);
        }

        private static double BadPressure(double p)
        {
            var x = Clamp(p, 950, 1070);
            var d = Math.Abs(x - 1013);
            if (d <= 15) return 0;
            var z = Clamp((d - 15) / 60, 0, 2.0);
            return Clamp(1 - Math.Exp(-1.6 * Math.Pow(z, 2.3)), 0, 0.85);
        }

        public static ComfortResult Compute(ComfortInputs inputs)
        {
            var enabled = inputs.Enabled ?? new ComfortEnabled();

            var weights = new Dictionary<string, double>
            {
                ["co2"] = 0.50,
                ["rh"] = 0.25,
                ["temp"] = 0.20,
                ["pres"] = 0.05
            };

            const double P = 2.2;
            const double K = 2.9;

            var parts = new Dictionary<string, ComfortPart>(StringComparer.OrdinalIgnoreCase);
            var reasons = new List<string>();

            double sumW = 0;
            double acc = 0;

            void PushPart(string key, double value, Func<double, double> badFn, Func<double, double, string> describeFn)
            {
                var b = Clamp(badFn(value), 0, 1);
                var w = weights.TryGetValue(key, out var weight) ? weight : 0;
                sumW += w;
                acc += w * Math.Pow(b, P);
                parts[key] = new ComfortPart { Bad = b, Score = (int)Math.Round(100 * (1 - b)) };
                var msg = describeFn(value, b);
                if (!string.IsNullOrWhiteSpace(msg))
                    reasons.Add(msg);
            }

            if (enabled.Co2)
            {
                var v = Clamp(inputs.Co2 ?? 0, 0, 3000);
                PushPart("co2", v, BadCO2, (ppm, _) =>
                {
                    if (ppm < 800) return "CO2 ist normal.";
                    if (ppm < 1200) return "CO2 ist erhöht.";
                    if (ppm < 1800) return "CO2 ist deutlich erhöht.";
                    return "CO2 ist sehr hoch.";
                });
            }

            if (enabled.Rh)
            {
                var v = Clamp(inputs.Rh ?? 0, 0, 100);
                PushPart("rh", v, BadRH, (rh, _) =>
                {
                    if (rh is >= 30 and <= 50) return "Luftfeuchtigkeit ist normal.";
                    if ((rh is >= 25 and < 30) || (rh is > 50 and <= 60)) return "Luftfeuchtigkeit ist leicht ungünstig.";
                    if ((rh is >= 20 and < 25) || (rh is > 60 and <= 70)) return "Luftfeuchtigkeit ist ungünstig.";
                    return "Luftfeuchtigkeit ist sehr ungünstig.";
                });
            }

            if (enabled.Temp)
            {
                var v = Clamp(inputs.Temp ?? 0, 6, 40);
                PushPart("temp", v, BadTempC, (t, _) =>
                {
                    if (t is >= 20 and <= 26) return "Temperatur ist im angenehmen Bereich.";
                    if ((t is >= 18 and < 20) || (t is > 26 and <= 28)) return "Temperatur ist außerhalb des angenehmen Bereichs.";
                    if ((t is >= 15 and < 18) || (t is > 28 and <= 30)) return "Temperatur ist deutlich außerhalb des Komfortbereichs.";
                    return "Temperatur ist extrem.";
                });
            }

            if (enabled.Pres)
            {
                var v = Clamp(inputs.Pres ?? 1013, 950, 1070);
                PushPart("pres", v, BadPressure, (p, _) =>
                {
                    if (Math.Abs(p - 1013) <= 15) return "Luftdruck ist normal.";
                    if (Math.Abs(p - 1013) <= 35) return "Luftdruck abweichend.";
                    return "Luftdruck stark abweichend.";
                });
            }

            if (sumW <= 0)
            {
                return new ComfortResult
                {
                    Score = 0,
                    Risk = 1,
                    Label = "-",
                    Hint = "Keine Werte aktiv.",
                    Parts = parts,
                    Reasons = new List<string> { "Keine Werte aktiv." }
                };
            }

            var normalized = acc / sumW;
            var risk = Clamp(1 - Math.Exp(-K * normalized), 0, 1);
            var score = (int)Math.Round(Clamp(100 * (1 - risk), 0, 100));

            var label = "Gute Luftqualität";
            var hint = "Werte im Optimalbereich.";
            if (score < 40) { label = "Gefahr"; hint = "Werte im schädlichen Bereich."; }
            else if (score < 60) { label = "Warnung"; hint = "Werte spürbar suboptimal."; }
            else if (score < 75) { label = "OK"; hint = "In Ordnung, aber nicht ideal."; }

            return new ComfortResult
            {
                Score = score,
                Risk = risk,
                Label = label,
                Hint = hint,
                Parts = parts,
                Reasons = reasons
            };
        }
    }
}
