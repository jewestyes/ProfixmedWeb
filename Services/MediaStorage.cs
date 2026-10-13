using System.Security.Cryptography;

namespace ProfixmedWeb.Services;

public class MediaValidationException(string message) : Exception(message);

/// <summary>
/// Files uploaded from the admin panel. They are stored under random names in App_Data/uploads
/// and served at /uploads.
/// </summary>
public sealed class MediaStorage
{
    public const string UrlPrefix = "/uploads/";
    public const long MaxPdfBytes = 30 * 1024 * 1024;
    public const long MaxImageBytes = 15 * 1024 * 1024;

    // Card previews are rendered at roughly 150 dpi for A4 — sharp enough, and a few hundred KB as JPEG.
    private const int PreviewWidth = 1240;

    private readonly string _dir;
    private readonly string _webRoot;
    private readonly ILogger<MediaStorage> _logger;

    public MediaStorage(ContentPaths paths, IWebHostEnvironment environment, ILogger<MediaStorage> logger)
    {
        _dir = paths.Uploads;
        _webRoot = Path.GetFullPath(environment.WebRootPath);
        _logger = logger;
    }

    public Task<string> SavePdfAsync(IFormFile file)
    {
        if (file.Length > MaxPdfBytes)
            throw new MediaValidationException($"PDF больше {MaxPdfBytes / 1024 / 1024} МБ.");

        var header = ReadHeader(file, 5);
        if (!header.AsSpan().SequenceEqual("%PDF-"u8))
            throw new MediaValidationException("Файл не похож на PDF.");

        return SaveAsync(file, ".pdf");
    }

    public Task<string> SaveImageAsync(IFormFile file)
    {
        if (file.Length > MaxImageBytes)
            throw new MediaValidationException($"Картинка больше {MaxImageBytes / 1024 / 1024} МБ.");

        var extension = DetectImageExtension(ReadHeader(file, 12))
            ?? throw new MediaValidationException("Поддерживаются только картинки PNG, JPEG и WebP.");

        return SaveAsync(file, extension);
    }

    /// <summary>Renders the first page of a PDF (uploaded or in wwwroot) to JPEG. Returns null if rendering fails.</summary>
    public string? TryRenderPreview(string pdfUrl)
    {
        var pdfPath = ResolveUpload(pdfUrl) ?? ResolveWebRoot(pdfUrl);
        if (pdfPath is null || !File.Exists(pdfPath))
            return null;

        if (!(OperatingSystem.IsWindows() || OperatingSystem.IsLinux() || OperatingSystem.IsMacOS()))
            return null;

        var name = NewFileName(".jpg");
        try
        {
            using var pdf = File.OpenRead(pdfPath);
            PDFtoImage.Conversion.SaveJpeg(
                Path.Combine(_dir, name),
                pdf,
                page: 0,
                options: new PDFtoImage.RenderOptions(Width: PreviewWidth, WithAspectRatio: true));
            return UrlPrefix + name;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to render preview for {Pdf}", pdfUrl);
            File.Delete(Path.Combine(_dir, name));
            return null;
        }
    }

    /// <summary>Deletes a file previously saved here. URLs outside /uploads (e.g. wwwroot/files) are left alone.</summary>
    public void Delete(string? url)
    {
        var path = ResolveUpload(url);
        if (path is not null && File.Exists(path))
            File.Delete(path);
    }

    private string? ResolveUpload(string? url)
    {
        if (url is null || !url.StartsWith(UrlPrefix, StringComparison.Ordinal))
            return null;

        var name = url[UrlPrefix.Length..];
        return name.Length > 0 && Path.GetFileName(name) == name ? Path.Combine(_dir, name) : null;
    }

    private string? ResolveWebRoot(string url)
    {
        if (!url.StartsWith('/'))
            return null;

        var path = Path.GetFullPath(Path.Combine(_webRoot, Uri.UnescapeDataString(url.TrimStart('/'))));
        return path.StartsWith(_webRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal) ? path : null;
    }

    private async Task<string> SaveAsync(IFormFile file, string extension)
    {
        var name = NewFileName(extension);
        await using (var target = File.Create(Path.Combine(_dir, name)))
        {
            await file.CopyToAsync(target);
        }
        return UrlPrefix + name;
    }

    private static string NewFileName(string extension) =>
        $"{DateTime.UtcNow:yyyyMMdd}-{Convert.ToHexString(RandomNumberGenerator.GetBytes(6)).ToLowerInvariant()}{extension}";

    private static byte[] ReadHeader(IFormFile file, int count)
    {
        using var stream = file.OpenReadStream();
        var buffer = new byte[count];
        var read = stream.ReadAtLeast(buffer, count, throwOnEndOfStream: false);
        return buffer[..read];
    }

    private static string? DetectImageExtension(byte[] header)
    {
        ReadOnlySpan<byte> h = header;
        if (h.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47 })) return ".png";
        if (h.StartsWith(new byte[] { 0xFF, 0xD8, 0xFF })) return ".jpg";
        if (h.Length >= 12 && h[..4].SequenceEqual("RIFF"u8) && h[8..12].SequenceEqual("WEBP"u8)) return ".webp";
        return null;
    }
}
