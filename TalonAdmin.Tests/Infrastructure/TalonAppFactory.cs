using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TalonAdmin.Reporting;

namespace TalonAdmin.Tests.Infrastructure;

/// <summary>
/// Boots the real application against a throwaway SQLite database. Nothing is stubbed except the
/// PDF renderer: reports still run their Razor templates, but the HTML is captured instead of
/// being handed to Chromium, so the suite asserts on report content without a 150 MB download.
/// </summary>
public class TalonAppFactory : WebApplicationFactory<Program>
{
    private readonly string _dataDirectory =
        Path.Combine(Path.GetTempPath(), "talon-tests", Guid.NewGuid().ToString("n"));

    public CapturingPdfRenderer PdfRenderer { get; } = new();

    /// <summary>Seed data the application creates on its own: roles, the admin user, actions, menu.</summary>
    public const string AdminUserName = "admin";
    public const string AdminPassword = "Talon!23";

    /// <summary>
    /// The inbound webhooks refuse everything unless this is configured, so an application built
    /// for tests has to carry one exactly as a real deployment does.
    /// </summary>
    public const string SystemSecret = "integration-test-system-secret";

    /// <summary>
    /// Scenario tests build their own data and turn this off; contract tests want the sample
    /// programme so there is something to query.
    /// </summary>
    protected virtual bool SeedDemoData => false;

    /// <summary>
    /// Overridden to boot an application with no <c>SystemSecret</c>, which is the misconfiguration
    /// the inbound webhooks have to refuse.
    /// </summary>
    protected virtual string? ConfiguredSystemSecret => SystemSecret;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(_dataDirectory);

        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = "Sqlite",
                ["ConnectionStrings:AdminContext"] = $"Data Source={Path.Combine(_dataDirectory, "admin.db")}",
                ["ConnectionStrings:VoucherContext"] = $"Data Source={Path.Combine(_dataDirectory, "vouchers.db")}",
                ["Database:CreateAndSeedOnStartup"] = "true",

                ["Database:SeedDemoData"] = SeedDemoData ? "true" : "false",
                ["Database:SeedAdminPassword"] = AdminPassword,

                ["Jwt:SigningKey"] = "integration-test-signing-key-integration-test-signing-key",
                ["SystemSecret"] = ConfiguredSystemSecret,
                ["ActiveDirectory:Enabled"] = "false"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IPdfRenderer>();
            services.AddSingleton<IPdfRenderer>(PdfRenderer);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
        {
            return;
        }

        try
        {
            if (Directory.Exists(_dataDirectory))
            {
                Directory.Delete(_dataDirectory, recursive: true);
            }
        }
        catch (IOException)
        {
            // A held SQLite handle is not worth failing a test run over.
        }
    }
}

/// <summary>
/// Stands in for headless Chromium and keeps the HTML each report produced, so tests can assert on
/// what the Razor templates actually rendered.
/// </summary>
public class CapturingPdfRenderer : IPdfRenderer
{
    private readonly List<string> _rendered = new();
    private readonly object _sync = new();

    public IReadOnlyList<string> Rendered
    {
        get { lock (_sync) { return _rendered.ToArray(); } }
    }

    public string LastHtml
    {
        get
        {
            lock (_sync)
            {
                Assert.True(_rendered.Count > 0, "No report was rendered.");
                return _rendered[^1];
            }
        }
    }

    public void Clear()
    {
        lock (_sync) { _rendered.Clear(); }
    }

    public Task<byte[]> RenderAsync(string html, string paperSize = "A4", PdfOrientation orientation = PdfOrientation.Portrait)
    {
        lock (_sync) { _rendered.Add(html); }

        // Smallest thing that is still recognisably a PDF to the calling code.
        return Task.FromResult("%PDF-1.4\n%%EOF\n"u8.ToArray());
    }
}
