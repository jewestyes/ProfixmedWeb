using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.WebEncoders;
using ProfixmedWeb.Services;

if (args is ["hash-password", ..])
{
    AdminPassword.RunHashCommand();
    return;
}

var builder = WebApplication.CreateBuilder(args);

var admin = builder.Configuration.GetSection("Admin").Get<AdminOptions>() ?? new AdminOptions();
var adminPath = admin.NormalizedPath;
var paths = new ContentPaths(builder.Configuration, builder.Environment);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.Configure<RouteOptions>(options => options.LowercaseUrls = true);
// Output Cyrillic as is instead of &#x...; entities.
builder.Services.Configure<WebEncoderOptions>(options => options.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All));

builder.Services.AddSingleton(paths);
builder.Services.AddSingleton(admin);
builder.Services.AddSingleton<ContentStore>();
builder.Services.AddSingleton<MediaStorage>();

// Keys live next to the content so admin sessions survive deploys and restarts.
builder.Services.AddDataProtection()
    .SetApplicationName("ProfixmedWeb")
    .PersistKeysToFileSystem(new DirectoryInfo(paths.Keys));

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = $"/{adminPath}/login";
        options.AccessDeniedPath = $"/{adminPath}/login";
        options.Cookie.Name = "profixmed_admin";
        options.Cookie.Path = $"/{adminPath}";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
        options.SlidingExpiration = true;
    });

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("admin-login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(1) }));
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(paths.Uploads),
    RequestPath = "/uploads",
});

app.UseRouting();

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// The panel is reachable only at its configured path, and only once a password hash is set.
if (admin.IsEnabled)
{
    app.MapControllerRoute(
        name: "admin",
        pattern: adminPath + "/{action=Index}/{id?}",
        defaults: new { controller = "Admin" });
}
else
{
    app.Logger.LogWarning("Admin panel is disabled: Admin:PasswordHash is not configured");
}

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}",
    defaults: null,
    constraints: new { controller = "Home" });

// Load (or seed on first start) the content file before serving requests.
app.Services.GetRequiredService<ContentStore>();

app.Run();
