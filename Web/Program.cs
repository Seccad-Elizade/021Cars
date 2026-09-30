using System.Globalization;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Claims;
using EnterpriseAeroStudio.Data;
using EnterpriseAeroStudio.Data.Repositories;
using EnterpriseAeroStudio.Services;
using EnterpriseAeroStudio.Models;
using EnterpriseAeroStudio.Web;
using EnterpriseAeroStudio.Web.Components;
using EnterpriseAeroStudio.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Serilog;
using Serilog.Events;

// ============================================================================
//  Avtomobil Parkı v6.0 — VEB SERVER (Blazor Server / .NET 10)
// ----------------------------------------------------------------------------
//  WPF ("Autocode") layihəsi ilə EYNİ verilənlər bazasını və EYNİ Services
//  qatını istifadə edir. Masaüstü tətbiq və veb server eyni anda işləyə bilər.
//
//  İşə salma:            dotnet run
//  Yalnız bu kompüter:   http://localhost:5000
//  Lokal şəbəkə (WiFi):  http://<bu-kompüterin-IP>:5000   (aşağıda çap olunur)
// ============================================================================

var builder = WebApplication.CreateBuilder(args);

// Konsolda Azərbaycan hərfləri düzgün göstərilsin.
try
{
    Console.OutputEncoding = System.Text.Encoding.UTF8;
}
catch
{
    // Bəzi terminal növlərində dəstəklənmir — əhəmiyyətli deyil.
}

// ------------------------------------------------------- HTML kodlaşdırması
//  Blazor @dəyişən ifadələrini standart olaraq HTML entity-lərə çevirir:
//      "Aylıq Ödəniş"  ->  "Ayl&#x131;q &#xD6;d&#x259;ni&#x15F;"
//  Brauzer bunu düzgün göstərsə də, HTML-i şişirdir.
//  Azərbaycan hərflərini (ə, ı, ş, ğ, ç, ö, ü) və emojiləri birbaşa buraxırıq.
builder.Services.Configure<Microsoft.Extensions.WebEncoders.WebEncoderOptions>(o =>
{
    o.TextEncoderSettings = new System.Text.Encodings.Web.TextEncoderSettings(
        System.Text.Unicode.UnicodeRanges.All);
});

// ---------------------------------------------------------------- Konfiqurasiya
var section = builder.Configuration.GetSection("AvtoPark");
var port          = section.GetValue("Port", 5000);
var port80        = section.GetValue("AlsoListenPort80", false);
var publicDomain  = section.GetValue<string>("PublicDomain") ?? string.Empty;
var dnsEnabled    = section.GetValue("DnsEnabled", true);
var dnsPort       = section.GetValue("DnsPort", 53);
var dnsUpstream   = section.GetValue("DnsUpstream", "8.8.8.8") ?? "8.8.8.8";

// Bu kompüterin WiFi/LAN ünvanları — DNS cavabı və banner üçün.
var lanAddresses = GetLocalIPv4Addresses();
var lanIp = lanAddresses.Count > 0 ? lanAddresses[0] : string.Empty;

// Port 80 boşdursa onu da dinlə (URL-də ":5000" olmasın).
var canUsePort80 = false;
var requireLogin  = section.GetValue("RequireLogin", true);
var adminPassword = section.GetValue("AdminPassword", "021cars");
var sessionDays   = section.GetValue("SessionDays", 30);
var allowWrite    = section.GetValue("AllowWrite", true);
var siteTitle     = section.GetValue("SiteTitle", "Avtomobil Parkı");

var dataDirectory = section.GetValue<string>("DataDirectory");
if (string.IsNullOrWhiteSpace(dataDirectory))
{
    // Masaüstü tətbiq ilə EYNİ qovluq → eyni avtopark.db, eyni Media faylları.
    dataDirectory = Path.Combine(
        Cas0201.Kok.Qovluq,
        "EnterpriseAeroStudio");
}
Directory.CreateDirectory(dataDirectory);

var dbPath       = Path.Combine(dataDirectory, "avtopark.db");
var logDirectory = Path.Combine(dataDirectory, "Logs");
Directory.CreateDirectory(logDirectory);

builder.Services.Configure<AvtoParkOptions>(o =>
{
    o.SiteTitle    = siteTitle;
    o.AllowWrite   = allowWrite;
    o.RequireLogin = requireLogin;
});

// -------------------------------------------------------------------- Mədəniyyət
// Bütün tarixlər gg.aa.iiii formatında (masaüstü tətbiqlə eyni).
var culture = (CultureInfo)CultureInfo.GetCultureInfo("en-US").Clone();
culture.DateTimeFormat.ShortDatePattern = "dd.MM.yyyy";
culture.DateTimeFormat.DateSeparator = ".";
culture.NumberFormat.NumberDecimalSeparator = ".";
CultureInfo.DefaultThreadCurrentCulture = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;

// ----------------------------------------------------------------------- Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        Path.Combine(logDirectory, "avtopark-web-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 31,
        shared: true,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

builder.Logging.ClearProviders();
builder.Logging.AddSerilog(Log.Logger, dispose: true);

// ------------------------------------------------- Kestrel: bütün şəbəkə interfeysləri
// 0.0.0.0 → həm localhost, həm də WiFi/şəbəkə üzərindən əlçatan.
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(port);

    // 021cars.az üçün əlavə olaraq 80 portunda da dinlə (URL-də ":5000" olmasın).
    if (port80 && port != 80)
    {
        canUsePort80 = IsPortFree(80);

        if (canUsePort80)
        {
            options.ListenAnyIP(80);
        }
        else
        {
            Log.Warning("Port 80 başqa proqram tərəfindən tutulub — " +
                        "yalnız {Port} portu istifadə olunacaq.", port);
        }
    }
});

// ------------------------------------------- Reverse proxy (Cloudflare / nginx)
// Domen arxasında (tunnel, nginx, IIS) işləyərkən düzgün protokol/IP/host oxunsun.
builder.Services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor |
        Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto |
        Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedHost;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// ------------------------------------------------------------------ Blazor Server
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.Configure<Microsoft.AspNetCore.Components.Server.CircuitOptions>(o =>
{
    // WiFi-də telefon/planşet bağlantısı üçün səxavətli limitlər.
    o.DisconnectedCircuitMaxRetained = 100;
    o.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(3);
    o.JSInteropDefaultCallTimeout = TimeSpan.FromMinutes(2);
    o.MaxBufferedUnacknowledgedRenderBatches = 20;
});

// ------------------------------------------------------------------- Autentifikasiya
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath            = "/login";
        options.LogoutPath           = "/logout";
        options.AccessDeniedPath     = "/login";
        options.ExpireTimeSpan       = TimeSpan.FromDays(sessionDays);
        options.SlidingExpiration    = true;
        options.Cookie.Name          = "AvtoPark.Auth";
        options.Cookie.HttpOnly      = true;
        options.Cookie.IsEssential   = true;
        options.Cookie.SameSite      = SameSiteMode.Lax;
    });

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();

// --------------------------------------------------------------- Verilənlər bazası
// Transient — WPF tətbiqindəki ilə eyni davranış. Blazor Server sxemlərində
// (circuit) uzunömürlü Scoped DbContext təhlükəlidir; Transient isə təhlükəsizdir.
builder.Services.AddDbContext<AppDbContext>(
    options => options.UseSqlite($"Data Source={dbPath}"),
    ServiceLifetime.Transient);

// ------------------------------------------------------------------- Repositories
builder.Services.AddTransient(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddTransient<ICarRepository, CarRepository>();
builder.Services.AddTransient<IExpenseRepository, ExpenseRepository>();
builder.Services.AddTransient<ICreditRepository, CreditRepository>();
builder.Services.AddTransient<ISaleRepository, SaleRepository>();
builder.Services.AddTransient<IPartnerShareRepository, PartnerShareRepository>();
builder.Services.AddTransient<IPartnerRepository, PartnerRepository>();
builder.Services.AddTransient<IPartnerPaymentRepository, PartnerPaymentRepository>();
builder.Services.AddTransient<IOdenisMohletRepository, OdenisMohletRepository>();
builder.Services.AddTransient<IKassaHereketRepository, KassaHereketRepository>();


// ---------------------------------------------------------------------- Services
builder.Services.AddTransient<ICarService, CarService>();
builder.Services.AddTransient<IExpenseService, ExpenseService>();
builder.Services.AddTransient<IExpenseCatalogService, ExpenseCatalogService>();
builder.Services.AddTransient<IMediaService, MediaService>();
    builder.Services.AddTransient<ITrashService, TrashService>();
builder.Services.AddTransient<ICreditService, CreditService>();
builder.Services.AddTransient<ISaleService, SaleService>();
builder.Services.AddTransient<IPartnerService, PartnerService>();
builder.Services.AddTransient<IMohletService, MohletService>();
builder.Services.AddSingleton<IKassaService, KassaService>();

builder.Services.AddSingleton<IExportService, ExportService>();
builder.Services.AddTransient<DbInitializer>();
builder.Services.AddScoped<UiState>();

// ============================================================================
//   CANLI SİNXRONİZASİYA  —  masaüstü (WPF) tətbiq  ↔  veb sayt
// ----------------------------------------------------------------------------
//   Hər iki tətbiq EYNİ avtopark.db faylını işlədir. Bu izləyici faylı
//   nəzarətdə saxlayır və hər dəyişiklikdə bütün açıq səhifələrə xəbər verir —
//   beləliklə bir tərəfdə edilən dəyişiklik digər tərəfdə DƏRHAL görünür
//   ("Yenilə" düyməsini basmaq lazım deyil).
// ============================================================================
builder.Services.AddSingleton<IDataChangeWatcher>(sp =>
{
    var watcher = new DataChangeWatcher(
        dbPath,
        sp.GetRequiredService<ILogger<DataChangeWatcher>>());

    watcher.Start();
    return watcher;
});

// ============================================================================
//      DNS SERVERİ  —  WiFi-dəki cihazlar üçün "021cars.az" yönləndirməsi
// ----------------------------------------------------------------------------
//  Domen QEYDİYYATDA OLMAYA DA BİLƏR — bu server özü cavab verir.
//  Cihazların DNS-i bu kompüterə yönəldilərsə, 021cars.az avtomаtik açılır.
// ============================================================================
if (dnsEnabled && !string.IsNullOrWhiteSpace(lanIp))
{
    var dnsDomains = new List<string>
    {
        string.IsNullOrWhiteSpace(publicDomain) ? "021cars.az" : publicDomain,
        "avtopark.local"
    };

    if (!string.IsNullOrWhiteSpace(publicDomain))
    {
        dnsDomains.Add("www." + publicDomain);
    }

    var answerIp = IPAddress.Parse(lanIp);

    builder.Services.AddSingleton(sp => new MiniDnsServer(
        sp.GetRequiredService<ILogger<MiniDnsServer>>(),
        dnsPort,
        dnsUpstream,
        dnsDomains,
        answerIp));

    builder.Services.AddHostedService(sp => sp.GetRequiredService<MiniDnsServer>());
}

// ============================================================================
//                                  TƏTBİQ
// ============================================================================
var app = builder.Build();

// --- Giriş səhifəsi (statik HTML) bir dəfə oxunur ---
var webRoot = app.Environment.WebRootPath
              ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot");

var loginHtmlPath = Path.Combine(webRoot, "login.html");
var loginHtml = File.Exists(loginHtmlPath)
    ? await File.ReadAllTextAsync(loginHtmlPath)
    : "<h1>login.html tapılmadı</h1>";

// Giriş səhifəsindəki QR kod üçün bu kompüterin WiFi/LAN ünvanı.
// Port 80 aktivdirsə portsuz ünvan göstərilir (daha rahat yazılır).
var lanUrl = lanAddresses.Count == 0
    ? string.Empty
    : canUsePort80
        ? $"http://{lanAddresses[0]}"
        : $"http://{lanAddresses[0]}:{port}";
loginHtml = loginHtml.Replace("{{LAN_URL}}", lanUrl);

// Banner üçün DNS serverinin vəziyyəti.
var dnsServer = app.Services.GetService<MiniDnsServer>();

// ------------------------------------------------- Verilənlər bazasının hazırlanması
try
{
    using var scope = app.Services.CreateScope();
    var initializer = scope.ServiceProvider.GetRequiredService<DbInitializer>();
    await initializer.InitializeAsync();

    // SQLite-ı çox-prosesli istifadəyə (WPF + veb eyni anda) hazırla.
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
    await db.Database.ExecuteSqlRawAsync("PRAGMA busy_timeout=5000;");
}
catch (Exception ex)
{
    Log.Fatal(ex, "Verilənlər bazası hazırlana bilmədi.");
    throw;
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseForwardedHeaders();

// --- 1) wwwroot: app.css, app.js, login.html, favicon.svg, qrcode.min.js ---
app.UseStaticFiles();

// ===========================================================================
//  BLAZOR-UN MÜHƏRRİK FAYLLARI  (blazor.web.js)  —  KRİTİK!
// ---------------------------------------------------------------------------
//  <c>/_framework/blazor.web.js</c> .NET-in DAXİLİ statik aktividir və fiziki
//  olaraq NuGet paketində saxlanılır:
//      %USERPROFILE%\.nuget\packages\
//          microsoft.aspnetcore.app.internal.assets\<versiya>\_framework\
//
//  <c>dotnet run</c> zamanı tətbiq PRODUCTION rejimində işləyir və ASP.NET
//  bu qovluğu AVTOMATİK qoşmur (yalnız Development-də və ya `dotnet publish`
//  edilmiş çıxışda qoşulur). Nəticədə fayl 404 olur → brauzer Blazor-u
//  YÜKLƏMİR → səhifə görünür, AMMA HEÇ BİR DÜYMƏ İŞLƏMİR.
//
//  Ona görə bu qovluğu ƏL İLƏ qoşuruq — həm `dotnet run`,
//  həm də publish edilmiş çıxışda eyni cür işləyir.
// ===========================================================================
var frameworkAssets = FindFrameworkAssetsFolder();

if (frameworkAssets is not null)
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(frameworkAssets),
        RequestPath = "/_framework"
    });
}

app.UseAuthentication();
app.UseAuthorization();

// ---------------------------------------------------------------- Giriş nəzarəti
// Şifrə tələb olunursa, autentifikasiya olunmamış bütün sorğular /login-ə yönləndirilir.
if (requireLogin)
{
    app.Use(async (context, next) =>
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            await next();
            return;
        }

        var path = context.Request.Path.Value ?? "/";

        // Statik aktiv (uzantısı olan: .js, .css, .svg, .html…) heç bir şifrə
        // tələb etmir — Blazor-un öz faylları da bura düşür.
        if (IsPublicPath(path) || Path.HasExtension(path))
        {
            await next();
            return;
        }

        var target = context.Request.Path + context.Request.QueryString;
        context.Response.Redirect("/login?r=" + Uri.EscapeDataString(target));
    });
}

app.UseAntiforgery();

// ============================================================================
//                 STATİK AKTİVLƏR
// ----------------------------------------------------------------------------
//  wwwroot faylları  ->  yuxarıda app.UseStaticFiles() ilə verilir.
//  _framework (blazor.web.js) ->  yuxarıda FrameworkAssets üçün ayrıca
//                                 UseStaticFiles() ilə verilir.
//
//  QEYD: <c>MapStaticAssets()</c> İSTİFADƏ OLUNMUR, çünki o, yalnız
//  Development rejimində və ya `dotnet publish` edilmiş çıxışda işləyir.
//  Adi `dotnet run` (Production) zamanı bu fayl üçün
//      FileNotFoundException ("Could not find file ... wwwroot\_framework\...")
//  atır və nəticədə HEÇ BİR DÜYMƏ İŞLƏMİR.
// ============================================================================

// ----------------------------------------------------------------- Giriş / Çıxış
app.MapGet("/login", () =>
    Results.Content(loginHtml, "text/html; charset=utf-8")).DisableAntiforgery();

app.MapPost("/login", async (HttpContext context) =>
{
    var form = await context.Request.ReadFormAsync();
    var password = form["password"].ToString();
    var returnUrl = form["returnUrl"].ToString();

    var ok = !requireLogin || string.Equals(password, adminPassword, StringComparison.Ordinal);

    if (!ok)
    {
        Log.Warning("Uğursuz giriş cəhdi. IP: {Ip}", context.Connection.RemoteIpAddress);
        return Results.Redirect("/login?e=1");
    }

    var claims = new List<Claim>
    {
        new(ClaimTypes.Name, "İstifadəçi"),
        new(ClaimTypes.Role, "Admin")
    };

    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

    await context.SignInAsync(
        CookieAuthenticationDefaults.AuthenticationScheme,
        new ClaimsPrincipal(identity),
        new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddDays(sessionDays)
        });

    Log.Information("İstifadəçi daxil oldu. IP: {Ip}", context.Connection.RemoteIpAddress);

    var safe = !string.IsNullOrWhiteSpace(returnUrl)
               && returnUrl.StartsWith('/')
               && !returnUrl.StartsWith("//")
        ? returnUrl
        : "/";

    return Results.Redirect(safe);
}).DisableAntiforgery();

app.MapGet("/logout", async (HttpContext context) =>
{
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect(requireLogin ? "/login" : "/");
}).DisableAntiforgery();

// --------------------------------------------------------- CSV İXRACI (Excel)
app.MapGet("/api/export/cars", async (ICarService carsService, IExportService exportService) =>
{
    var list = await carsService.GetAllCarsAsync();
    var path = Path.Combine(Path.GetTempPath(), $"cars_{Guid.NewGuid():N}.csv");

    try
    {
        await exportService.ExportCarsAsync(list, path);
        var bytes = await File.ReadAllBytesAsync(path);
        return Results.File(bytes, "text/csv; charset=utf-8",
            $"avtomobiller_{DateTime.Now:yyyyMMdd}.csv");
    }
    finally
    {
        if (File.Exists(path)) File.Delete(path);
    }
});

app.MapGet("/api/export/expenses", async (IExpenseService expenseService, IExportService exportService) =>
{
    var list = await expenseService.GetExpensesAsync();
    var path = Path.Combine(Path.GetTempPath(), $"expenses_{Guid.NewGuid():N}.csv");

    try
    {
        await exportService.ExportExpensesAsync(list, path);
        var bytes = await File.ReadAllBytesAsync(path);
        return Results.File(bytes, "text/csv; charset=utf-8",
            $"xercler_{DateTime.Now:yyyyMMdd}.csv");
    }
    finally
    {
        if (File.Exists(path)) File.Delete(path);
    }
});

app.MapGet("/api/export/purchases", async (ICarService carsService, IExportService exportService) =>
{
    var list = await carsService.GetAllCarsAsync();
    var path = Path.Combine(Path.GetTempPath(), $"purchases_{Guid.NewGuid():N}.csv");

    try
    {
        await exportService.ExportPurchasesAsync(list, path);
        var bytes = await File.ReadAllBytesAsync(path);
        return Results.File(bytes, "text/csv; charset=utf-8",
            $"alislar_{DateTime.Now:yyyyMMdd}.csv");
    }
    finally
    {
        if (File.Exists(path)) File.Delete(path);
    }
});

// ------------------------------------------------------- SƏNƏD / MEDIA FAYLLARI
app.MapGet("/api/media/file/{id:int}", async (int id, IRepository<MediaAttachment> attachments) =>
{
    var item = await attachments.GetByIdAsync(id);
    if (item is null || string.IsNullOrWhiteSpace(item.StoredPath) || !File.Exists(item.StoredPath))
    {
        return Results.NotFound();
    }

    var contentType = item.Extension switch
    {
        "PDF" => "application/pdf",
        "JPG" or "JPEG" => "image/jpeg",
        "PNG" => "image/png",
        "GIF" => "image/gif",
        "BMP" => "image/bmp",
        "WEBP" => "image/webp",
        "TXT" => "text/plain; charset=utf-8",
        "CSV" => "text/csv; charset=utf-8",
        "DOC" => "application/msword",
        "DOCX" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "XLS" => "application/vnd.ms-excel",
        "XLSX" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        _ => "application/octet-stream"
    };

    var stream = File.OpenRead(item.StoredPath);
    return Results.File(stream, contentType, item.FileName);
});

// ------------------------------------------------------------ SAĞLAMLIQ YOXLAMASI
app.MapGet("/health", (IServiceProvider sp) =>
{
    var dataChanges = sp.GetService<IDataChangeWatcher>();

    return Results.Ok(new
    {
        status = "ok",
        time = DateTime.Now,

        // Canlı sinxronizasiya vəziyyəti.
        sinxronizasiya = new
        {
            isleyir = dataChanges?.IsRunning ?? false,
            deyisiklikSayi = dataChanges?.ChangeCount ?? 0,
            sonDeyisiklik = dataChanges?.LastChangeUtc
        }
    });
});

// ------------------------------------------------- DNS DURUMU (diaqnostika)
//  Brauzerde:  http://021cars.az/dns-durum
//  Göstərir: DNS serveri işləyirmi və cihazlardan sorğu gəlirmi.
//  QEYD: MiniDnsServer şərti qeydiyyatdan keçdiyi üçün parametr kimi
//  DİRƏKT istənilmir (DNS söndürüləndə startup çökərdi) — IServiceProvider
//  vasitəsilə təhlükəsiz şəkildə alınır.
app.MapGet("/dns-durum", (IServiceProvider sp) =>
{
    var dns = sp.GetService<MiniDnsServer>();

    return Results.Json(new
    {
        izah = "sorquSayi > 0 olarsa cihazlar bu servere qosulur",
        isleyir = dns?.IsRunning ?? false,
        port = dnsPort,
        sorquSayi = dns?.QueryCount ?? 0,
        cavabSayi = dns?.AnswerCount ?? 0,
        oturmeSayi = dns?.ForwardCount ?? 0,
        sonSorquDomeni = dns?.LastQueryName ?? string.Empty,
        sonSorquMusterisi = dns?.LastQueryClient ?? string.Empty,
        sonSorquVaxti = dns?.LastQueryTime,
        xeta = dns?.Error
    }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
}).DisableAntiforgery();

// ------------------------------------------------------------------- Blazor sxemi
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// ------------------------------------------------------------- İşə düşmə məlumatı
app.Lifetime.ApplicationStarted.Register(
    () => PrintStartupBanner(
        port, canUsePort80, publicDomain, requireLogin, adminPassword, dbPath,
        dnsServer, lanAddresses, dnsPort));

try
{
    await app.RunAsync();
}
catch (Exception ex) when (
    ex is IOException
    && (ex.Message.Contains("address already in use", StringComparison.OrdinalIgnoreCase)
        || ex.InnerException is Microsoft.AspNetCore.Connections.AddressInUseException
        || ex.InnerException?.Message.Contains("address already in use", StringComparison.OrdinalIgnoreCase) == true))
{
    var l = new string('=', 74);
    Console.WriteLine();
    Console.WriteLine(l);
    Console.WriteLine("   [XETA]  PORT ARTİQ İSTİFADƏDƏDİR!");
    Console.WriteLine(l);
    Console.WriteLine($"   {port} portu başqa bir proqram tərəfindən tutulub.");
    Console.WriteLine();
    Console.WriteLine("   ÇOX GÜMAN Kİ veb server artıq bir pəncərədə işləyir.");
    Console.WriteLine();
    Console.WriteLine("   HƏLLİ:");
    Console.WriteLine("     1) Bütün 'AvtoPark' pəncərələrini bağlayın (Ctrl+C)");
    Console.WriteLine("     2) her-seyi-baslat.bat faylını YENİDƏN işə salın");
    Console.WriteLine();
    Console.WriteLine($"   YA DA appsettings.json faylında Port dəyərini dəyişin:");
    Console.WriteLine($"        \"Port\": {port}   ->   \"Port\": {port + 1}");
    Console.WriteLine(l);
    Console.WriteLine();

    Log.Fatal("Port {Port} artıq istifadədədir.", port);
}
catch (Exception ex)
{
    Log.Fatal(ex, "Server gözlənilməz xəta ilə dayandı.");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}

// ============================================================================
//                                  KÖMƏKÇİLƏR
// ============================================================================

/// <summary>
/// Blazor-un mühərrik fayllarının (<c>blazor.web.js</c>, <c>blazor.server.js</c>)
/// fiziki qovluğunu tapır.
/// <para>
/// Axtarış sırası: publish edilmiş çıxış (wwwroot/_framework, _framework),
/// sonra işləyən runtime versiyasına uyğun NuGet paketi, sonra ən yeni paket.
/// </para>
/// </summary>
/// <returns>Tapılan qovluğun tam yolu; tapılmadıqda <c>null</c>.</returns>
static string? FindFrameworkAssetsFolder()
{
    // --- 1) Publish edilmiş çıxış (fayllar tətbiqin yanındadır) ---
    var baseDir = AppContext.BaseDirectory;

    foreach (var candidate in new[]
             {
                 Path.Combine(baseDir, "wwwroot", "_framework"),
                 Path.Combine(baseDir, "_framework")
             })
    {
        if (File.Exists(Path.Combine(candidate, "blazor.web.js")))
        {
            return candidate;
        }
    }

    // --- 2) NuGet paketi (dotnet run zamanı) ---
    try
    {
        var packagesRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".nuget",
            "packages",
            "microsoft.aspnetcore.app.internal.assets");

        if (!Directory.Exists(packagesRoot))
        {
            return null;
        }

        // Əvvəlcə işləyən runtime versiyasına TAM uyğun paket axtarılır.
        var exact = Path.Combine(packagesRoot, Environment.Version.ToString(), "_framework");

        if (File.Exists(Path.Combine(exact, "blazor.web.js")))
        {
            return exact;
        }

        // Tapılmadısa — versiyaya görə ən yenisi götürülür.
        var folders = Directory.GetDirectories(packagesRoot);

        Array.Sort(folders, (a, b) =>
        {
            var va = Version.TryParse(Path.GetFileName(a), out var x) ? x : new Version(0, 0);
            var vb = Version.TryParse(Path.GetFileName(b), out var y) ? y : new Version(0, 0);
            return vb.CompareTo(va);
        });

        foreach (var folder in folders)
        {
            var framework = Path.Combine(folder, "_framework");

            if (File.Exists(Path.Combine(framework, "blazor.web.js")))
            {
                return framework;
            }
        }
    }
    catch
    {
        // Axtarış xətası serveri dayandırmamalıdır.
    }

    return null;
}

/// <summary>Şifrəsiz əlçatan yollar (statik fayllar və giriş səhifəsi).</summary>
/// <remarks>
/// <b>KRİTİK:</b> <c>/_blazor</c> mütləq burada olmalıdır!
/// Blazor Server-in WebSocket/SignalR endpointi <c>/_blazor</c> ünvanındadır.
/// Əgər bu yol şifrə nəzarəti ilə <c>/login</c>-ə yönləndirilərsə, brauzer
/// circuit-i qura bilmir və nəticədə SƏHİFƏ GÖRÜNÜR, AMMA HEÇ BİR DÜYMƏ
/// İŞLƏMİR (statik prerender göstərilir, interaktivlik ölüdür).
/// Təhlükəsizlik itmir: circuit auth cookie ilə açılır və yalnız
/// autentifikasiyadan keçmiş sessiyalara xidmət edir.
/// </remarks>
static bool IsPublicPath(string path)
    => path.StartsWith("/login", StringComparison.OrdinalIgnoreCase)
    || path.StartsWith("/logout", StringComparison.OrdinalIgnoreCase)
    || path.StartsWith("/favicon", StringComparison.OrdinalIgnoreCase)
    || path.StartsWith("/app.css", StringComparison.OrdinalIgnoreCase)
    || path.StartsWith("/qrcode.min.js", StringComparison.OrdinalIgnoreCase)
    || path.StartsWith("/_blazor", StringComparison.OrdinalIgnoreCase)
    || path.StartsWith("/_framework", StringComparison.OrdinalIgnoreCase)
    || path.StartsWith("/_content", StringComparison.OrdinalIgnoreCase)
    || path.StartsWith("/health", StringComparison.OrdinalIgnoreCase);

/// <summary>Server işə düşdükdə bütün əlçatan ünvanları konsola çap edir.</summary>
static void PrintStartupBanner(
    int port, bool canUsePort80, string publicDomain, bool requireLogin, string adminPassword,
    string dbPath, MiniDnsServer? dns, List<string> lanIps, int dnsPort)
{
    var line = new string('=', 74);
    Console.WriteLine();
    Console.WriteLine(line);
    Console.WriteLine("   AVTOMOBIL PARKI v6.0  —  VEB SERVER işə düşdü");
    Console.WriteLine(line);

    // ------------------------- DOMEN ADI İLƏ GİRİŞ -------------------------
    if (!string.IsNullOrWhiteSpace(publicDomain))
    {
        Console.WriteLine();
        Console.WriteLine($"   >>>  http://{publicDomain}{(canUsePort80 ? string.Empty : ":" + port)}");
        Console.WriteLine("        (bu kompüterdə 'hosts' faylı ilə avtomatik işləyir)");

        if (dns is { IsRunning: true } && lanIps.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("   >>>  WiFi-dəki cihazlar üçün:");
            Console.WriteLine($"        Cihazın DNS-ini  {lanIps[0]}  etsəniz,");
            Console.WriteLine($"        http://{publicDomain}  avtomatik açılacaq.");
            Console.WriteLine($"        (Yerli DNS serveri UDP :{dnsPort} üzərində işləyir)");
        }
    }

    Console.WriteLine();
    Console.WriteLine($"   Bu kompüterdə :  http://localhost:{port}");

    if (canUsePort80)
    {
        Console.WriteLine($"   Port 80-də    :  http://localhost");
    }

    if (lanIps.Count > 0)
    {
        Console.WriteLine();
        Console.WriteLine("   Şəbəkədə (WiFi / LAN) — digər cihazlar bu ünvanı açır:");
        foreach (var ip in lanIps)
        {
            Console.WriteLine($"       ->  http://{ip}:{port}");
        }
    }
    else
    {
        Console.WriteLine("   [XEBARDARLIQ] Şəbəkə ünvanı tapılmadı — WiFi-ə qoşulduğunuzu yoxlayın.");
    }

    Console.WriteLine();
    Console.WriteLine($"   Verilənlər bazası : {dbPath}");
    Console.WriteLine($"   Giriş nəzarəti   : {(requireLogin ? "AKTİV (şifrə tələb olunur)" : "SÖNDÜRÜLMÜŞ")}");

    if (dns is not null)
    {
        Console.WriteLine($"   Yerli DNS serveri : {(dns.IsRunning
            ? $"AKTİV  ->  UDP :{dnsPort}"
            : "BAŞLADILA BİLMƏDİ (port 53 tutulub ola bilər)")}");
    }

    if (requireLogin && adminPassword == "021cars")
    {
        Console.WriteLine();
        Console.WriteLine("   [DİQQƏT] Standart şifrə istifadə olunur!");
        Console.WriteLine("            appsettings.json -> AvtoPark:AdminPassword dəyərini dəyişin.");
    }

    Console.WriteLine(line);
    Console.WriteLine("   Dayandırmaq üçün bu pəncərədə Ctrl+C basın.");
    Console.WriteLine(line);
    Console.WriteLine();
}

/// <summary>Port hazırda boşdurmı? (başqa proqram tutmayıb)</summary>
static bool IsPortFree(int port)
{
    try
    {
        var listener = new TcpListener(IPAddress.Any, port);
        listener.Start();
        listener.Stop();
        return true;
    }
    catch
    {
        return false;
    }
}

/// <summary>Aktiv şəbəkə interfeyslərinin IPv4 ünvanları (loopback xaric).</summary>
static List<string> GetLocalIPv4Addresses()
{
    var result = new List<string>();

    try
    {
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up)
            {
                continue;
            }

            if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback
                or NetworkInterfaceType.Tunnel)
            {
                continue;
            }

            // Virtual / VPN adapterlərini atla — real WiFi/LAN ünvanı qalsın.
            var name = (nic.Name + " " + nic.Description).ToLowerInvariant();
            if (name.Contains("vethernet")
                || name.Contains("virtual")
                || name.Contains("vmware")
                || name.Contains("virtualbox")
                || name.Contains("bluetooth")
                || name.Contains("loopback")
                || name.Contains("tunnel")
                || name.Contains("wsl")
                || name.Contains("docker"))
            {
                continue;
            }

            foreach (var address in nic.GetIPProperties().UnicastAddresses)
            {
                if (address.Address.AddressFamily != AddressFamily.InterNetwork
                    || IPAddress.IsLoopback(address.Address))
                {
                    continue;
                }

                var ip = address.Address.ToString();

                // 169.254.x.x = avtomatik (uğursuz DHCP) — istifadəçiyə yaramaz.
                if (ip.StartsWith("169.254.", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!result.Contains(ip))
                {
                    result.Add(ip);
                }
            }
        }
    }
    catch
    {
        // Şəbəkə məlumatı alına bilməzsə, sadəcə boş siyahı qaytarılır.
    }

    // Ən çox rast gəlinən ev/ofis şəbəkələri əvvəldə olsun.
    return result
        .OrderBy(ip => ip.StartsWith("192.168.", StringComparison.Ordinal) ? 0
                     : ip.StartsWith("10.", StringComparison.Ordinal) ? 1
                     : ip.StartsWith("172.", StringComparison.Ordinal) ? 2
                     : 3)
        .ToList();
}

