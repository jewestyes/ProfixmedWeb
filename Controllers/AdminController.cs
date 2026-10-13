using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.RateLimiting;
using ProfixmedWeb.Models;
using ProfixmedWeb.Services;

namespace ProfixmedWeb.Controllers;

/// <summary>
/// Hidden panel for editing events and news on the home page.
/// Routed only via the "admin" route at the configured Admin:Path (see Program.cs).
/// </summary>
[Authorize]
[AutoValidateAntiforgeryToken]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class AdminController : Controller
{
    private const long MaxUploadRequestBytes = MediaStorage.MaxPdfBytes + MediaStorage.MaxImageBytes + 1024 * 1024;

    private readonly ContentStore _store;
    private readonly MediaStorage _media;
    private readonly AdminOptions _options;
    private readonly ILogger<AdminController> _logger;

    public AdminController(ContentStore store, MediaStorage media, AdminOptions options, ILogger<AdminController> logger)
    {
        _store = store;
        _media = media;
        _options = options;
        _logger = logger;
    }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
        base.OnActionExecuting(context);
    }

    // ---------- Login ----------

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction(nameof(Index));

        return View(new AdminLoginForm { ReturnUrl = returnUrl });
    }

    [AllowAnonymous]
    [HttpPost]
    [EnableRateLimiting("admin-login")]
    public async Task<IActionResult> Login(AdminLoginForm form)
    {
        if (!ModelState.IsValid)
            return View(form);

        var usernameOk = AdminPassword.UsernameMatches(form.Username, _options.Username);
        var passwordOk = AdminPassword.Verify(form.Password!, _options.PasswordHash);
        if (!usernameOk || !passwordOk)
        {
            _logger.LogWarning("Failed admin login from {Ip}", HttpContext.Connection.RemoteIpAddress);
            ModelState.AddModelError("", "Неверный логин или пароль.");
            return View(form);
        }

        var identity = new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.Name, _options.Username) },
            CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(new ClaimsPrincipal(identity));

        _logger.LogInformation("Admin logged in from {Ip}", HttpContext.Connection.RemoteIpAddress);
        return Url.IsLocalUrl(form.ReturnUrl) ? LocalRedirect(form.ReturnUrl) : RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    // ---------- Overview ----------

    [HttpGet]
    public IActionResult Index() => View(_store.Current);

    // ---------- Events ----------

    [HttpGet]
    public IActionResult CreateEvent() => View("EventForm", new AdminEventForm());

    [HttpGet]
    public IActionResult EditEvent(Guid id)
    {
        var item = _store.Current.Events.FirstOrDefault(e => e.Id == id);
        if (item is null)
            return NotFound();

        return View("EventForm", new AdminEventForm
        {
            Id = item.Id,
            Date = item.Date,
            Location = item.Location,
            IsVisible = item.IsVisible,
            CurrentPdfUrl = item.PdfUrl,
            CurrentImageUrl = item.ImageUrl,
            CurrentImageIsAuto = item.ImageIsAuto,
        });
    }

    [HttpPost]
    [RequestSizeLimit(MaxUploadRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUploadRequestBytes)]
    public async Task<IActionResult> SaveEvent(AdminEventForm form)
    {
        var existing = form.Id is { } id ? _store.Current.Events.FirstOrDefault(e => e.Id == id) : null;
        if (form.Id is not null && existing is null)
            return NotFound();

        if (existing is null && form.Pdf is null)
            ModelState.AddModelError(nameof(form.Pdf), "Загрузите PDF приглашения");

        if (!ModelState.IsValid)
            return View("EventForm", FillCurrent(form, existing));

        var pdfUrl = existing?.PdfUrl;
        var imageUrl = existing?.ImageUrl;
        var imageIsAuto = existing?.ImageIsAuto ?? false;
        var created = new List<string>();
        var replaced = new List<string?>();
        var failedField = nameof(form.Pdf);

        try
        {
            if (form.Pdf is not null)
            {
                var newPdf = await _media.SavePdfAsync(form.Pdf);
                created.Add(newPdf);
                replaced.Add(pdfUrl);
                pdfUrl = newPdf;
            }

            if (form.Image is not null)
            {
                failedField = nameof(form.Image);
                var newImage = await _media.SaveImageAsync(form.Image);
                created.Add(newImage);
                replaced.Add(imageUrl);
                imageUrl = newImage;
                imageIsAuto = false;
            }
            else if (form.ResetImage || imageUrl is null || (form.Pdf is not null && imageIsAuto))
            {
                var preview = pdfUrl is null ? null : _media.TryRenderPreview(pdfUrl);
                if (preview is not null)
                {
                    replaced.Add(imageUrl);
                    imageUrl = preview;
                    imageIsAuto = true;
                }
                else
                {
                    TempData["Warning"] = "Не удалось сделать картинку из PDF — загрузите картинку для карточки вручную.";
                }
            }
        }
        catch (MediaValidationException ex)
        {
            created.ForEach(_media.Delete);
            ModelState.AddModelError(failedField, ex.Message);
            return View("EventForm", FillCurrent(form, existing));
        }

        await _store.UpdateAsync(content =>
        {
            var item = existing is null ? new SiteEvent() : content.Events.First(e => e.Id == existing.Id);
            item.Date = form.Date!.Value;
            item.Location = form.Location!.Trim();
            item.IsVisible = form.IsVisible;
            item.PdfUrl = pdfUrl;
            item.ImageUrl = imageUrl;
            item.ImageIsAuto = imageIsAuto;

            if (existing is null)
                content.Events.Insert(0, item);
        });

        replaced.ForEach(_media.Delete);
        TempData["Message"] = existing is null ? "Мероприятие добавлено." : "Мероприятие сохранено.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> ToggleEvent(Guid id)
    {
        await _store.UpdateAsync(content =>
        {
            var item = content.Events.FirstOrDefault(e => e.Id == id);
            if (item is not null)
                item.IsVisible = !item.IsVisible;
        });
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> MoveEvent(Guid id, int delta)
    {
        await _store.UpdateAsync(content => Move(content.Events, content.Events.FindIndex(e => e.Id == id), delta));
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> DeleteEvent(Guid id)
    {
        SiteEvent? removed = null;
        await _store.UpdateAsync(content =>
        {
            removed = content.Events.FirstOrDefault(e => e.Id == id);
            if (removed is not null)
                content.Events.Remove(removed);
        });

        if (removed is not null)
        {
            _media.Delete(removed.PdfUrl);
            _media.Delete(removed.ImageUrl);
            TempData["Message"] = "Мероприятие удалено.";
        }
        return RedirectToAction(nameof(Index));
    }

    // ---------- News ----------

    [HttpGet]
    public IActionResult CreateNews() => View("NewsForm", new AdminNewsForm { Date = DateOnly.FromDateTime(DateTime.Today) });

    [HttpGet]
    public IActionResult EditNews(Guid id)
    {
        var item = _store.Current.News.FirstOrDefault(n => n.Id == id);
        if (item is null)
            return NotFound();

        return View("NewsForm", new AdminNewsForm
        {
            Id = item.Id,
            Title = item.Title,
            Date = item.Date,
            Url = item.Url,
            IsVisible = item.IsVisible,
        });
    }

    [HttpPost]
    public async Task<IActionResult> SaveNews(AdminNewsForm form)
    {
        if (form.Id is { } id && _store.Current.News.All(n => n.Id != id))
            return NotFound();

        if (!ModelState.IsValid)
            return View("NewsForm", form);

        await _store.UpdateAsync(content =>
        {
            var item = form.Id is null ? new NewsItem() : content.News.First(n => n.Id == form.Id);
            item.Title = form.Title!.Trim();
            item.Date = form.Date!.Value;
            item.Url = form.Url!.Trim();
            item.IsVisible = form.IsVisible;

            if (form.Id is null)
                content.News.Insert(0, item);
        });

        TempData["Message"] = form.Id is null ? "Новость добавлена." : "Новость сохранена.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> ToggleNews(Guid id)
    {
        await _store.UpdateAsync(content =>
        {
            var item = content.News.FirstOrDefault(n => n.Id == id);
            if (item is not null)
                item.IsVisible = !item.IsVisible;
        });
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> MoveNews(Guid id, int delta)
    {
        await _store.UpdateAsync(content => Move(content.News, content.News.FindIndex(n => n.Id == id), delta));
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> DeleteNews(Guid id)
    {
        await _store.UpdateAsync(content => content.News.RemoveAll(n => n.Id == id));
        TempData["Message"] = "Новость удалена.";
        return RedirectToAction(nameof(Index));
    }

    // ---------- Helpers ----------

    private static AdminEventForm FillCurrent(AdminEventForm form, SiteEvent? existing)
    {
        form.CurrentPdfUrl = existing?.PdfUrl;
        form.CurrentImageUrl = existing?.ImageUrl;
        form.CurrentImageIsAuto = existing?.ImageIsAuto ?? false;
        return form;
    }

    private static void Move<T>(List<T> list, int index, int delta)
    {
        var target = index + Math.Sign(delta);
        if (index < 0 || target < 0 || target >= list.Count)
            return;

        (list[index], list[target]) = (list[target], list[index]);
    }
}
