using System.Text.Encodings.Web;
using System.Text.Json;
using ProfixmedWeb.Models;

namespace ProfixmedWeb.Services;

/// <summary>
/// Locations of mutable data. On the server App_Data is a symlink into the shared folder (see deploy.yaml),
/// so it survives deploys; "Content:Path" in config overrides it.
/// </summary>
public sealed class ContentPaths
{
    public ContentPaths(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var configured = configuration["Content:Path"];
        Root = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(environment.ContentRootPath, "App_Data")
            : Path.GetFullPath(configured);

        Uploads = Path.Combine(Root, "uploads");
        Keys = Path.Combine(Root, "keys");
        DataFile = Path.Combine(Root, "content.json");

        Directory.CreateDirectory(Uploads);
        Directory.CreateDirectory(Keys);
    }

    public string Root { get; }
    public string Uploads { get; }
    public string Keys { get; }
    public string DataFile { get; }
}

/// <summary>
/// Keeps site content in memory and persists it to a JSON file. Readers get an object
/// that is never mutated afterwards: every update works on a copy and swaps it in.
/// </summary>
public sealed class ContentStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly string _file;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private SiteContent _current;

    public ContentStore(ContentPaths paths, ILogger<ContentStore> logger)
    {
        _file = paths.DataFile;

        if (File.Exists(_file))
        {
            _current = JsonSerializer.Deserialize<SiteContent>(File.ReadAllText(_file), JsonOptions) ?? new SiteContent();
        }
        else
        {
            logger.LogInformation("Content file {File} not found, creating it with default content", _file);
            _current = SiteContent.CreateDefault();
            Write(_current);
        }
    }

    public SiteContent Current => Volatile.Read(ref _current);

    public async Task UpdateAsync(Action<SiteContent> change)
    {
        await _lock.WaitAsync();
        try
        {
            var copy = Clone(_current);
            change(copy);
            Write(copy);
            Volatile.Write(ref _current, copy);
        }
        finally
        {
            _lock.Release();
        }
    }

    private void Write(SiteContent content)
    {
        var temp = _file + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(content, JsonOptions));
        File.Move(temp, _file, overwrite: true);
    }

    private static SiteContent Clone(SiteContent content) =>
        JsonSerializer.Deserialize<SiteContent>(JsonSerializer.Serialize(content, JsonOptions), JsonOptions)!;
}
