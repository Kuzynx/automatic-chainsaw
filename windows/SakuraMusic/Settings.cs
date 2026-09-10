using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SakuraMusic;

public enum PetalDensity { Off = 0, Light, Normal, Heavy }
public enum Blush { Off = 0, Soft, Deep }

/// <summary>User preferences, stored as JSON under %APPDATA%\SakuraMusic.</summary>
public sealed class Settings
{
    public static Settings Current { get; } = Load();

    private bool _persist;

    public event Action? Changed;

    private bool _enabled = true;
    private PetalDensity _petals = PetalDensity.Normal;
    private Blush _blush = Blush.Soft;
    private bool _showBranch = true;
    private bool _alwaysShow;

    public bool Enabled { get => _enabled; set { _enabled = value; Save(); } }
    public PetalDensity Petals { get => _petals; set { _petals = value; Save(); } }
    public Blush Blush { get => _blush; set { _blush = value; Save(); } }
    public bool ShowBranch { get => _showBranch; set { _showBranch = value; Save(); } }

    /// <summary>Keep the overlay up even when another window overlaps Music.</summary>
    public bool AlwaysShow { get => _alwaysShow; set { _alwaysShow = value; Save(); } }

    [JsonIgnore]
    public double PetalMultiplier => _petals switch
    {
        PetalDensity.Off => 0,
        PetalDensity.Light => 0.45,
        PetalDensity.Normal => 1,
        PetalDensity.Heavy => 2.2,
        _ => 1,
    };

    [JsonIgnore]
    public double BlushOpacity => _blush switch
    {
        Blush.Off => 0,
        Blush.Soft => 0.55,
        Blush.Deep => 1,
        _ => 0.55,
    };

    public static string Title(PetalDensity d) => d switch
    {
        PetalDensity.Off => "Off",
        PetalDensity.Light => "Light breeze",
        PetalDensity.Normal => "Gentle fall",
        PetalDensity.Heavy => "Full bloom",
        _ => d.ToString(),
    };

    public static string Title(Blush b) => b switch
    {
        Blush.Off => "Off",
        Blush.Soft => "Soft",
        Blush.Deep => "Deep",
        _ => b.ToString(),
    };

    private static readonly string FilePath = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SakuraMusic", "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var loaded = JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath), JsonOptions) ?? new Settings();
                loaded._persist = true;
                return loaded;
            }
        }
        catch
        {
            // Corrupt file: fall back to defaults and overwrite on next save.
        }
        return new Settings { _persist = true };
    }

    private void Save()
    {
        // Setters also run during JSON deserialization; don't write or notify then.
        if (!_persist) return;
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch
        {
            // Persisting is best effort; the in-memory value still applies.
        }
        Changed?.Invoke();
    }
}
