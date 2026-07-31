using System.Data;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using OfficeOpenXml;
using TalonAdmin.Extensions;
using TalonAdmin.Models.Vouchers;
using TalonAdmin.Tests.Infrastructure;

namespace TalonAdmin.Tests;

/// <summary>
/// Covers the reporting and spreadsheet stacks, both of which changed wholesale in the migration:
/// RazorEngine to the built-in view engine, wkhtmltopdf to headless Chromium, and EPPlus 4 to 8.
/// </summary>
public class ReportingAndExportTests : IClassFixture<SeededTalonAppFactory>
{
    private const string SpreadsheetContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly SeededTalonAppFactory _factory;
    private readonly TalonApiClient _api;

    public ReportingAndExportTests(SeededTalonAppFactory factory)
    {
        _factory = factory;
        _api = new TalonApiClient(factory.CreateClient());
        _api.UseTenant(1, 1);
    }

    private async Task AuthenticateAsync() =>
        await _api.AuthenticateAsync(TalonAppFactory.AdminUserName, TalonAppFactory.AdminPassword);

    private object ReportRequest() => new
    {
        DistributionId = 1,
        ProgramId = 1,
        VendorId = 1,
        CountryId = 1,
        OrganizationId = 1,
        PaperSize = "A4",
        TimezoneOffset = 0d,
        PeriodStart = "2000-01-01",
        PeriodEnd = "2100-01-01"
    };

    [Theory]
    [InlineData("DistributionReport")]
    [InlineData("ProgramClosureReport")]
    [InlineData("VendorProgramFinancialReport")]
    public async Task Pdf_reports_render_their_razor_template(string report)
    {
        await AuthenticateAsync();
        _factory.PdfRenderer.Clear();

        var pdf = await _api.PostForBytesAsync($"/api/Reports/{report}", ReportRequest());

        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(pdf[..4]));

        var html = _factory.PdfRenderer.LastHtml;
        Assert.False(string.IsNullOrWhiteSpace(html));
        Assert.Contains("Demo Cash Programme", html);
    }

    [Fact]
    public async Task The_distribution_report_lists_the_beneficiaries_and_their_totals()
    {
        await AuthenticateAsync();
        _factory.PdfRenderer.Clear();

        await _api.PostForBytesAsync("/api/Reports/DistributionReport", ReportRequest());

        var html = _factory.PdfRenderer.LastHtml;

        Assert.Contains("Distribution Report", html);
        Assert.Contains("Beneficiary 01", html);
        // The seeded personal identifier; the field no longer holds a document number.
        Assert.Contains("1000000001", html);

        // Five vouchers at 25 each were seeded.
        Assert.Contains("125", html);
    }

    [Fact]
    public async Task The_payment_schedule_is_a_spreadsheet()
    {
        await AuthenticateAsync();

        using var response = await _api.SendAsync(HttpMethod.Post, "/api/Reports/PaymentScheduleReport", ReportRequest());

        Assert.True(response.IsSuccessStatusCode);
        Assert.Equal(SpreadsheetContentType, response.Content.Headers.ContentType?.MediaType);
        AssertIsWorkbook(await response.Content.ReadAsByteArrayAsync());
    }

    /// <summary>
    /// The ordering the reconciliation screen has to respect. Reconciling records who agreed to
    /// pay; it deliberately does not finalise. Only the vendor receipt finalises, and the payment
    /// schedule reports nothing but finalised transactions — so a screen that offers reconciling
    /// and the payment schedule, but no way to issue the receipt, produces an empty schedule and
    /// no explanation. That is exactly what it used to do.
    /// </summary>
    [Fact]
    public async Task Only_the_vendor_receipt_finalises_what_reconciliation_agreed_to_pay()
    {
        await AuthenticateAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<Context>();

            var redemptions = await context.VoucherTransactionRecords
                .Where(v => v.Type == 2 && v.VendorId == 1)
                .ToListAsync();

            Assert.NotEmpty(redemptions);

            foreach (var redemption in redemptions)
            {
                redemption.ReconciledOn = DateTime.UtcNow;
                redemption.ReconciledBy = TalonAppFactory.AdminUserName;
                redemption.IsFinalized = null;
            }

            await context.SaveChangesAsync();

            // Agreeing to pay is not the same as having paid: the schedule still ignores these.
            Assert.All(redemptions, r => Assert.NotEqual(true, r.IsFinalized));
        }

        await _api.PostForBytesAsync("/api/Reports/VendorProgramFinancialReport", ReportRequest());

        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<Context>();

            var reconciled = await context.VoucherTransactionRecords
                .Where(v => v.Type == 2 && v.VendorId == 1 && v.ReconciledOn != null)
                .ToListAsync();

            Assert.All(reconciled, r => Assert.True(r.IsFinalized));

            // Nothing that was never reconciled gets swept along with it.
            var untouched = await context.VoucherTransactionRecords
                .Where(v => v.ReconciledOn == null)
                .ToListAsync();

            Assert.All(untouched, r => Assert.NotEqual(true, r.IsFinalized));
        }
    }

    [Fact]
    public async Task Beneficiaries_export_and_re_import()
    {
        await AuthenticateAsync();

        var exported = await _api.PostForBytesAsync(
            "/api/Excel/ExportBeneficiaries?organizationId=1&countryId=1", new { });

        AssertIsWorkbook(exported);

        using var package = new ExcelPackage(new MemoryStream(exported));
        var sheet = package.Workbook.Worksheets.First();
        Assert.NotNull(sheet.Dimension);
        Assert.True(sheet.Dimension!.End.Row > 1, "The export should contain at least one data row.");

        var upload = new MultipartFormDataContent();
        var file = new ByteArrayContent(exported);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(SpreadsheetContentType);
        upload.Add(file, "file", "beneficiaries.xlsx");

        var request = new HttpRequestMessage(HttpMethod.Post,
            "/api/Excel/ImportBeneficiaries?organizationId=1&countryId=1") { Content = upload };
        request.Headers.TryAddWithoutValidation("X-Tenant-Country", "1");
        request.Headers.TryAddWithoutValidation("X-Tenant-Organization", "1");
        request.Headers.TryAddWithoutValidation("Authorization", await AuthorizationHeaderAsync());

        using var response = await _factory.CreateClient().SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode, $"Import failed ({(int)response.StatusCode}): {body}");
        Assert.Empty(JObject.Parse(body)["Errors"]!);
    }

    [Fact]
    public async Task Metadata_exports_even_though_some_of_its_tables_are_empty()
    {
        await AuthenticateAsync();

        using var response = await _api.SendAsync(HttpMethod.Get, "/api/Excel/ExportMetadata?organizationId=1&countryId=1");

        Assert.True(response.IsSuccessStatusCode,
            $"ExportMetadata failed ({(int)response.StatusCode}): {await response.Content.ReadAsStringAsync()}");

        AssertIsWorkbook(await response.Content.ReadAsByteArrayAsync());
    }

    private async Task<string> AuthorizationHeaderAsync()
    {
        using var response = await _factory.CreateClient().PostAsync("/Token", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["username"] = TalonAppFactory.AdminUserName,
                ["password"] = TalonAppFactory.AdminPassword
            }));

        return "Token " + JObject.Parse(await response.Content.ReadAsStringAsync()).Value<string>("access_token");
    }

    private static void AssertIsWorkbook(byte[] bytes)
    {
        Assert.True(bytes.Length > 0, "Expected a spreadsheet, got nothing.");

        // xlsx is a zip; "PK" is the local file header.
        Assert.Equal("PK", Encoding.ASCII.GetString(bytes[..2]));
    }
}

/// <summary>
/// EPPlus 4 quietly ignored a DataTable with no columns; EPPlus 8 throws "Column out of range".
/// A table is column-less whenever it came from an empty JSON array, so a single empty collection
/// used to fail the whole metadata export.
/// </summary>
public class ExcelExtensionsTests
{
    static ExcelExtensionsTests()
    {
        ExcelPackage.License.SetNonCommercialOrganization("Talon integration tests");
    }

    [Fact]
    public void A_dataset_containing_an_empty_table_still_produces_a_workbook()
    {
        var populated = new DataTable("Populated");
        populated.Columns.Add("Name", typeof(string));
        populated.Rows.Add("first");

        var dataSet = new DataSet();
        dataSet.Tables.Add(populated);
        dataSet.Tables.Add(new DataTable("NoColumnsAtAll"));

        var bytes = dataSet.ToExcelSpreadsheet();

        Assert.NotEmpty(bytes);

        using var package = new ExcelPackage(new MemoryStream(bytes));
        Assert.Equal(2, package.Workbook.Worksheets.Count);
        Assert.Equal("first", package.Workbook.Worksheets["Populated"].Cells[2, 1].Value);
        Assert.Null(package.Workbook.Worksheets["NoColumnsAtAll"].Dimension);
    }

    [Fact]
    public void An_empty_table_on_its_own_still_produces_a_workbook()
    {
        var bytes = new DataTable("Empty").ToExcelSpreadsheet();

        Assert.NotEmpty(bytes);
    }
}
