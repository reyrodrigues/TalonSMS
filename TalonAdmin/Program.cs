using System;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using TalonAdmin.Auth;
using TalonAdmin.Data;
using TalonAdmin.Hubs;
using TalonAdmin.Messaging;
using TalonAdmin.Messaging.Channels;
using TalonAdmin.Models.Admin;
using TalonAdmin.Reporting;

var builder = WebApplication.CreateBuilder(args);

var configuration = builder.Configuration;

// EPPlus 5+ refuses to open a package until a licence has been declared. The upgrade from the
// LGPL-era 4.0.3 therefore requires an explicit choice here.
// ACTION REQUIRED: confirm this matches how the organisation is licensed. If EPPlus is used
// commercially, set Epplus:LicenseKey instead of relying on the non-commercial declaration.
var epplusLicenseKey = configuration["Epplus:LicenseKey"];
if (!string.IsNullOrWhiteSpace(epplusLicenseKey))
{
    OfficeOpenXml.ExcelPackage.License.SetCommercial(epplusLicenseKey);
}
else
{
    OfficeOpenXml.ExcelPackage.License.SetNonCommercialOrganization(
        configuration["Epplus:Organization"] ?? "International Rescue Committee");
}
// Managed Postgres providers (Neon, Supabase, and the platforms' own add-ons) hand out a single
// connection URL and conventionally expose it as DATABASE_URL. Honouring that variable directly is
// what lets a cloud deploy configure storage with one setting instead of three: its presence
// selects Postgres and points *both* contexts at the same database. Sharing one database is safe
// because the two models share no table names — AdminContext owns Identity, organisations,
// countries and menus; the voucher context owns everything else.
var databaseUrl = configuration["DATABASE_URL"];
var hasDatabaseUrl = !string.IsNullOrWhiteSpace(databaseUrl);

var provider = hasDatabaseUrl
    ? "Postgres"
    : configuration["Database:Provider"] ?? "SqlServer";

var isPostgres = string.Equals(provider, "Postgres", StringComparison.OrdinalIgnoreCase)
    || string.Equals(provider, "PostgreSQL", StringComparison.OrdinalIgnoreCase)
    || string.Equals(provider, "Npgsql", StringComparison.OrdinalIgnoreCase);

void ConfigureProvider(DbContextOptionsBuilder options, string connectionStringName)
{
    var connectionString = hasDatabaseUrl
        ? databaseUrl
        : configuration.GetConnectionString(connectionStringName);

    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException(
            $"Connection string '{connectionStringName}' is not configured. Set it under " +
            "ConnectionStrings, or set DATABASE_URL to a Postgres URL to configure both contexts " +
            "at once.");
    }

    if (string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
    {
        options.UseSqlite(ResolveSqlitePath(connectionString, builder.Environment.ContentRootPath));
    }
    else if (isPostgres)
    {
        options.UseNpgsql(NormalizePostgresConnectionString(connectionString));
    }
    else
    {
        options.UseSqlServer(connectionString);
    }
}

// Npgsql does not accept the postgresql:// URL form that every managed provider prints on its
// dashboard, so translate it into the keyword form it does accept. A value that is not a URL is
// returned untouched, which leaves an operator free to supply a full Npgsql connection string.
static string NormalizePostgresConnectionString(string connectionString)
{
    connectionString = connectionString.Trim();

    var isUrl = connectionString.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
        || connectionString.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase);

    var connectionStringBuilder = isUrl
        ? BuildFromPostgresUrl(connectionString)
        : new Npgsql.NpgsqlConnectionStringBuilder(connectionString);

    // Npgsql defaults MaxPoolSize to 100, and the app opens two contexts — enough to exhaust the
    // connection allowance of every free Postgres tier under light load. A field deployment serves
    // a handful of concurrent staff, so cap it well below those limits unless told otherwise.
    // NpgsqlConnectionStringBuilder pre-populates every keyword, so ContainsKey cannot distinguish
    // "unset" from "set to the default" — the input text is what actually carries that intent.
    var compactedInput = connectionString.Replace(" ", string.Empty);
    var specifiesPoolSize =
        compactedInput.Contains("MaxPoolSize", StringComparison.OrdinalIgnoreCase)
        || compactedInput.Contains("MaximumPoolSize", StringComparison.OrdinalIgnoreCase);

    if (!specifiesPoolSize)
    {
        connectionStringBuilder.MaxPoolSize = 10;
    }

    return connectionStringBuilder.ToString();
}

static Npgsql.NpgsqlConnectionStringBuilder BuildFromPostgresUrl(string url)
{
    var uri = new Uri(url);
    var userInfo = uri.UserInfo.Split(':', 2);

    var connectionStringBuilder = new Npgsql.NpgsqlConnectionStringBuilder
    {
        Host = uri.Host,
        Port = uri.IsDefaultPort ? 5432 : uri.Port,
        Database = Uri.UnescapeDataString(uri.AbsolutePath).TrimStart('/'),
        Username = Uri.UnescapeDataString(userInfo[0]),
        Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : null,
    };

    // Carry the query string across, which is how the URL form conveys sslmode — Neon and Supabase
    // both require TLS and both express it that way.
    foreach (var parameter in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
    {
        var parts = parameter.Split('=', 2);
        var key = Uri.UnescapeDataString(parts[0]);
        var value = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty;

        try
        {
            connectionStringBuilder[key] = value;
        }
        catch (ArgumentException)
        {
            // Providers append parameters that belong to libpq rather than Npgsql — Neon's
            // channel_binding is the common one. Ignoring them beats refusing to start.
        }
    }

    return connectionStringBuilder;
}

// SQLite resolves a relative Data Source against the process working directory, which is not the
// same place depending on whether the app was started with `dotnet run` from the repository root,
// from the project directory, or as a published DLL in a container. Anchoring the path to the
// content root — and creating the directory, which is absent on a fresh clone because git does not
// store empty directories — makes all three behave the same instead of failing with
// "SQLite Error 10: 'disk I/O error'".
static string ResolveSqlitePath(string connectionString, string contentRootPath)
{
    var connectionStringBuilder = new SqliteConnectionStringBuilder(connectionString);
    if (string.IsNullOrWhiteSpace(connectionStringBuilder.DataSource)
        || connectionStringBuilder.DataSource == ":memory:"
        || Path.IsPathRooted(connectionStringBuilder.DataSource))
    {
        return connectionString;
    }

    var fullPath = Path.GetFullPath(Path.Combine(contentRootPath, connectionStringBuilder.DataSource));
    var directory = Path.GetDirectoryName(fullPath);
    if (!string.IsNullOrEmpty(directory))
    {
        Directory.CreateDirectory(directory);
    }

    connectionStringBuilder.DataSource = fullPath;

    return connectionStringBuilder.ToString();
}

builder.Services.AddDbContext<TalonAdmin.Models.Vouchers.Context>(o => ConfigureProvider(o, "VoucherContext"));
builder.Services.AddDbContext<AdminContext>(o => ConfigureProvider(o, "AdminContext"));

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.User.AllowedUserNameCharacters = null; // Identity 2 allowed non-alphanumeric names
        options.Password.RequiredLength = 6;
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
    })
    .AddEntityFrameworkStores<AdminContext>()
    .AddDefaultTokenProviders();

builder.Services.AddHttpClient();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<TalonUserAuthenticator>();
builder.Services.AddScoped<JwtTokenIssuer>();
builder.Services.AddScoped<RazorViewRenderer>();
builder.Services.AddSingleton<IPdfRenderer, ChromiumPdfRenderer>();
builder.Services.AddScoped<DatabaseInitializer>();

// Messaging. Every channel registers as an IMessageChannel and the registry picks between them per
// country, so adding one does not mean touching the workflow that sends. The dispatcher runs the
// queue in the background; enqueuing is only a database write, so nothing user-facing waits on a
// gateway or fails because one is down.
builder.Services.AddHttpClient<ClickatellChannel>()
    .ConfigureHttpClient(client => client.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddScoped<IMessageChannel>(sp => sp.GetRequiredService<ClickatellChannel>());

// A handset on a field-office LAN or behind a relay is slower and flakier than a hosted gateway,
// so it gets a longer timeout before the attempt is called a failure and retried.
builder.Services.AddHttpClient<AndroidGatewayChannel>()
    .ConfigureHttpClient(client => client.Timeout = TimeSpan.FromSeconds(60));
builder.Services.AddScoped<IMessageChannel>(sp => sp.GetRequiredService<AndroidGatewayChannel>());

builder.Services.AddHttpClient<TelegramChannel>()
    .ConfigureHttpClient(client => client.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddScoped<IMessageChannel>(sp => sp.GetRequiredService<TelegramChannel>());
builder.Services.AddSingleton<TelegramEnrolment>();
builder.Services.AddScoped<MessageChannelRegistry>();
builder.Services.AddScoped<IOutboundMessageQueue, OutboundMessageQueue>();
builder.Services.AddHostedService<OutboundMessageDispatcher>();

builder.Services
    // AddIdentity above points DefaultAuthenticateScheme/DefaultChallengeScheme at its cookie
    // handlers. All three have to be reset explicitly, or bearer tokens are ignored and an
    // unauthenticated API call answers with a 302 to a login page that does not exist.
    .AddAuthentication(options =>
    {
        options.DefaultScheme = AuthConstants.SmartScheme;
        options.DefaultAuthenticateScheme = AuthConstants.SmartScheme;
        options.DefaultChallengeScheme = AuthConstants.SmartScheme;
    })
    .AddPolicyScheme(AuthConstants.SmartScheme, AuthConstants.SmartScheme, options =>
    {
        // One endpoint set serves both bearer-token clients and the Basic-auth field devices.
        options.ForwardDefaultSelector = context =>
        {
            string header = context.Request.Headers.Authorization;
            if (!string.IsNullOrEmpty(header)
                && header.StartsWith(AuthConstants.BasicScheme + " ", StringComparison.OrdinalIgnoreCase))
            {
                return AuthConstants.BasicScheme;
            }

            return JwtBearerDefaults.AuthenticationScheme;
        };
    })
    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, BasicAuthenticationHandler>(
        AuthConstants.BasicScheme, null)
    .AddJwtBearer(options =>
    {
        // The AngularJS client sends "Authorization: Token <access_token>", not "Bearer".
        // Under OWIN that worked by accident: /Token also issued an authentication cookie, so the
        // admin UI was really authenticated by cookie and the header scheme was never inspected.
        // Accepting "Token" here keeps that client working unchanged. Vendor devices also use a
        // "Token" scheme but hit [AuthorizeVendor] endpoints, which do their own lookup against
        // Vendor.AuthorizationToken and never consult this handler.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                string header = context.Request.Headers.Authorization;
                if (!string.IsNullOrEmpty(header)
                    && header.StartsWith("Token ", StringComparison.OrdinalIgnoreCase))
                {
                    context.Token = header.Substring("Token ".Length).Trim();
                }

                return Task.CompletedTask;
            }
        };

        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = configuration["Jwt:Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = JwtTokenIssuer.GetSigningKey(configuration),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(5)
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .SetIsOriginAllowed(_ => true)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

var mvc = builder.Services
    .AddControllersWithViews()
    .AddNewtonsoftJson(options =>
    {
        options.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;

        // Web API 2 serialised with the CLR property names; ASP.NET Core's AddNewtonsoftJson
        // defaults to a camelCase resolver instead. That matters well beyond cosmetics: Breeze
        // publishes its metadata with the PascalCase names, and a Breeze client matches incoming
        // JSON against those, so camelCase payloads leave every property unset — grids render the
        // right number of rows with entirely blank cells.
        options.SerializerSettings.ContractResolver = new DefaultContractResolver();

        // Breeze resolves the type of an expanded entity from Newtonsoft's $type discriminator.
        // Web API 2 emitted these by default; without them the client cannot materialise a
        // reference navigation and throws "Cannot read properties of undefined (reading
        // 'entityState')" the moment any query uses $expand on one.
        options.SerializerSettings.TypeNameHandling = TypeNameHandling.Objects;
        options.SerializerSettings.TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple;

        // TypeNameHandling also applies when reading, which is a remote-code-execution vector
        // unless the types a payload may name are restricted. See TalonSerializationBinder.
        options.SerializerSettings.SerializationBinder = new TalonSerializationBinder();
    });

if (builder.Environment.IsDevelopment())
{
    mvc.AddRazorRuntimeCompilation();
}

builder.Services.AddSignalR();

// Nothing in the original guarded against repeated guessing, which matters here because the values
// being guessed are short: a voucher code is six digits by default, so the whole space is a million
// candidates, and the endpoint that redeems one is reachable without authentication. Limits are
// applied per policy below and partitioned by caller IP.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Sign-in endpoints. There is no legitimate reason for one address to attempt many passwords in
    // quick succession, so this is deliberately tight; Identity has no lockout configured, and this
    // is what stands in for it.
    options.AddPolicy(RateLimitPolicies.Credentials, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            ResolveClientAddress(context),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(5),
                QueueLimit = 0
            }));

    // The inbound SMS webhook, which is where a voucher code is exchanged for value. A real
    // distribution produces a burst of redemptions that all arrive from the gateway's address, so
    // this ceiling has to clear legitimate traffic — it is a flood guard, not the primary control.
    // The primary control is SystemSecret; see the warning logged at startup when it is unset.
    options.AddPolicy(RateLimitPolicies.SmsRedemption, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            ResolveClientAddress(context),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 120,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

// Behind Render's and Railway's proxies the socket address is the load balancer, so every caller
// would share one partition. X-Forwarded-For's first entry is the originating client.
static string ResolveClientAddress(HttpContext context)
{
    var forwardedFor = context.Request.Headers["X-Forwarded-For"].ToString();

    if (!string.IsNullOrWhiteSpace(forwardedFor))
    {
        return forwardedFor.Split(',')[0].Trim();
    }

    return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
}

// The Vue client is a static SPA published to wwwroot/app by `npm run build`; this lets "/app/"
// resolve to its index.html.
app.UseDefaultFiles();
var isDevelopment = app.Environment.IsDevelopment();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        // Vite fingerprints its asset filenames, but index.html keeps the same name, so a rebuilt
        // client is indistinguishable from the cached one. Force revalidation while developing.
        if (isDevelopment)
        {
            ctx.Context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
        }
    }
});
app.UseRouting();
app.UseCors();

// After UseRouting, so the endpoint's own [EnableRateLimiting] policy has been resolved, and before
// authentication, so rejected callers are turned away without a database round trip.
app.UseRateLimiter();

app.UseMiddleware<TenantFormMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "breeze",
    pattern: "breeze/{controller}/{action}");

app.MapControllerRoute(
    name: "api",
    pattern: "api/{controller}/{action}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapControllers();
app.MapHub<DashboardHub>("/signalr/dashboard");

// The client uses history-mode routing, so every path below /app/ has to serve its index.html
// rather than 404 — a deep link or a refresh on /app/programs/3 must reach the SPA.
app.MapFallbackToFile("/app/{*path:nonfile}", "app/index.html");

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync();
}

// ValidateTransactionSMS is the endpoint an SMS gateway posts a redemption to, and it is necessarily
// anonymous — a gateway cannot hold a user's credentials. SystemSecret is the shared secret that
// distinguishes the real gateway from anyone else who has found the URL, and the endpoint skips the
// check entirely when it is blank. The rate limiter caps how fast codes can be tried, but only this
// secret stops them being tried at all, so an unset value is worth saying out loud.
if (!app.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(app.Configuration["SystemSecret"]))
{
    app.Logger.LogWarning(
        "SystemSecret is not set, so the inbound SMS endpoint accepts redemptions from any caller. "
        + "Set it to a random value and append it to the gateway's callback URL as ?secret=<value>.");
}

app.Run();

public partial class Program
{
}
