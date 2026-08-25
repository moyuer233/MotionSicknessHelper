using System.Text.Json;
using System.Text.Json.Serialization;

namespace MotionSicknessHelper;

public enum ShapeKind
{
    Triangle,
    Bar
}

public enum AnchorPosition
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
    Left,
    Top,
    Right,
    Bottom
}

public sealed class ShapeConfig
{
    [JsonPropertyName("position")]
    public AnchorPosition Position { get; set; } = AnchorPosition.TopLeft;

    [JsonPropertyName("shape")]
    public ShapeKind Shape { get; set; } = ShapeKind.Triangle;

    /// <summary>Length in pixels from the screen edge/corner toward the center.</summary>
    [JsonPropertyName("size")]
    public int Size { get; set; } = 240;

    /// <summary>Triangle base width or bar thickness in pixels.</summary>
    [JsonPropertyName("thickness")]
    public int Thickness { get; set; } = 50;

    /// <summary>HTML color without alpha, e.g. "#00FF00".</summary>
    [JsonPropertyName("color")]
    public string Color { get; set; } = "#00FF00";

    /// <summary>When true, this shape alternates between Color and Color2.</summary>
    [JsonPropertyName("flashEnabled")]
    public bool FlashEnabled { get; set; }

    /// <summary>Second HTML color used when flashing is enabled.</summary>
    [JsonPropertyName("color2")]
    public string Color2 { get; set; } = "#FF0000";

    /// <summary>0 = invisible, 255 = fully opaque.</summary>
    [JsonPropertyName("opacity")]
    public int Opacity { get; set; } = 140;

    public ShapeConfig Clone() => (ShapeConfig)MemberwiseClone();
}

public sealed class OverlayConfig
{
    [JsonPropertyName("shapes")]
    public List<ShapeConfig> Shapes { get; set; } = new();

    /// <summary>Distance in pixels from the exact screen edge/corner.</summary>
    [JsonPropertyName("edgeInset")]
    public int EdgeInset { get; set; } = 8;

    /// <summary>Milliseconds between the two flashing colors when at least one shape has FlashEnabled.</summary>
    [JsonPropertyName("flashIntervalMs")]
    public int FlashIntervalMs { get; set; } = 500;

    public static OverlayConfig CreateDefault()
    {
        return new OverlayConfig
        {
            EdgeInset = 8,
            Shapes = new List<ShapeConfig>
            {
                new() { Position = AnchorPosition.TopLeft, Shape = ShapeKind.Triangle, Size = 240, Thickness = 50, Color = "#00FF00", Opacity = 140 },
                new() { Position = AnchorPosition.TopRight, Shape = ShapeKind.Triangle, Size = 240, Thickness = 50, Color = "#00FF00", Opacity = 140 },
                new() { Position = AnchorPosition.BottomLeft, Shape = ShapeKind.Triangle, Size = 240, Thickness = 50, Color = "#00FF00", Opacity = 140 },
                new() { Position = AnchorPosition.BottomRight, Shape = ShapeKind.Triangle, Size = 240, Thickness = 50, Color = "#00FF00", Opacity = 140 }
            }
        };
    }

    public static OverlayConfig Load(string path)
    {
        if (!File.Exists(path))
            return CreateDefault();

        try
        {
            var options = new JsonSerializerOptions { ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
            var config = JsonSerializer.Deserialize<OverlayConfig>(File.ReadAllText(path), options);
            config?.Shapes.RemoveAll(s => s is null);
            if (config is null || config.Shapes.Count == 0)
                return CreateDefault();

            // Keep invalid values from crashing the app.
            foreach (var s in config.Shapes)
            {
                if (s.Size <= 0) s.Size = 240;
                if (s.Thickness <= 0) s.Thickness = 50;
                if (s.Opacity < 0) s.Opacity = 0;
                if (s.Opacity > 255) s.Opacity = 255;
                if (string.IsNullOrWhiteSpace(s.Color)) s.Color = "#00FF00";
                try
                {
                    var c = ColorTranslator.FromHtml(s.Color);
                    s.Color = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
                }
                catch
                {
                    s.Color = "#00FF00";
                }

                if (string.IsNullOrWhiteSpace(s.Color2)) s.Color2 = "#FF0000";
                try
                {
                    var c2 = ColorTranslator.FromHtml(s.Color2);
                    s.Color2 = $"#{c2.R:X2}{c2.G:X2}{c2.B:X2}";
                }
                catch
                {
                    s.Color2 = "#FF0000";
                }
            }

            if (config.EdgeInset < 0) config.EdgeInset = 0;
            if (config.FlashIntervalMs < 50) config.FlashIntervalMs = 50;
            if (config.FlashIntervalMs > 10000) config.FlashIntervalMs = 10000;
            return config;
        }
        catch
        {
            // Fall back to defaults on malformed config; the settings window will fix it.
            return CreateDefault();
        }
    }

    public void Save(string path)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(path, JsonSerializer.Serialize(this, options));
    }
}
