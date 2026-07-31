using System;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
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
var provider = configuration["Database:Provider"] ?? "SqlServer";

void ConfigureProvider(DbContextOptionsBuilder options, string connectionStringName)
{
    var connectionString = configuration.GetConnectionString(connectionStringName);
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException(
            $"Connection string '{connectionStringName}' is not configured.");
    }

    if (string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
    {
        options.UseSqlite(ResolveSqlitePath(connectionString, builder.Environment.ContentRootPath));
    }
    else
    {
        options.UseSqlServer(connectionString);
    }
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

app.Run();

public partial class Program
{
}
