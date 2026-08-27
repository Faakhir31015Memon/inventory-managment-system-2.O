using Microsoft.EntityFrameworkCore;
using SIOMS.Data;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
// Add services
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// Add DbContext
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ✅ Phase 4 - Live Exchange Rate: registers ExchangeRateService with its own HttpClient
builder.Services.AddHttpClient<SIOMS.Services.ExchangeRateService>();

// Add Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Admin/Login";
        options.Cookie.Name = "SIOMS.Auth";
    });

// Add Session
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Name = "SIOMS.Session";
});

var app = builder.Build();

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    context.Database.EnsureCreated();

    // Load the saved currency symbol (if any)
    var settings = context.Settings.FirstOrDefault();
    if (settings != null && !string.IsNullOrEmpty(settings.CurrencySymbol))
    {
        SIOMS.Helpers.CurrencyFormatter.Symbol = settings.CurrencySymbol;
    }
}

// Configure pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// Prevent the browser's back-forward cache (bfcache) from serving stale
// session-protected pages. Without this, hitting the Back button can show
// a frozen page that only recovers after a manual refresh, since the
// browser tries to restore the page from cache instead of re-requesting it.
app.Use(async (context, next) =>
{
    context.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
    context.Response.Headers["Pragma"] = "no-cache";
    context.Response.Headers["Expires"] = "-1";
    await next();
});

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.UseSession();

// Standard MVC Routing (No Areas)
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Admin}/{action=Login}/{id?}");

app.Run();