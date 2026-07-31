using System.Data;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using ClosedXML.Excel;
using TalonAdmin.Extensions;
using TalonAdmin.Models.Vouchers;
using TalonAdmin.Tests.Infrastructure;

namespace TalonAdmin.Tests;

/// <summary>
/// Covers the reporting and spreadsheet stacks, both of which changed wholesale in the migration:
/// RazorEngine to the built-in view engine, wkhtmltopdf to headless Chromium, and EPPlus to
/// ClosedXML.
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

        // Columns name the field underneath them. These two did not: "Id Number" sat over the
        // personal identifier and "Device Identifier" over a mobile number, on a document that
        // gets printed and signed.
        Assert.Contains("Personal Identifier", html);
        Assert.Contains("Mobile Number", html);
        Assert.DoesNotContain("Id Number", html);
        Assert.DoesNotContain("Device Identifier", html);

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
    /// The dashboard summary is cached, and it used to be cached to
    /// <c>{ContentRoot}/Dashboards/{organization}-{country}.json</c> — keyed by tenant, but not by
    /// database. Anything sharing the content root wrote the same files, so this test suite left
    /// its own figures where a developer's running instance would serve them for a completely
    /// different database. Nothing on disk should be able to answer for the data any more.
    /// </summary>
    [Fact]
    public async Task The_dashboard_ignores_a_summary_left_in_the_content_root()
    {
        await AuthenticateAsync();

        var contentRoot = _factory.Services
            .GetRequiredService<IWebHostEnvironment>()
            .ContentRootPath;

        var planted = Path.Combine(contentRoot, "Dashboards");
        var plantedTheDirectory = !Directory.Exists(planted);
        Directory.CreateDirectory(planted);

        var file = Path.Combine(planted, "1-1.json");
        await File.WriteAllTextAsync(
            file,
            """[{"Program":{"Id":99,"Name":"Figures from another database"},"TotalVouchers":4}]""");

        try
        {
            var dashboard = await _api.GetJsonAsync("/api/Dashboard/DashboardSummary");
            var json = dashboard.ToString();

            Assert.DoesNotContain("Figures from another database", json);

            // And it is answering from the database rather than simply returning nothing.
            Assert.Contains("Demo Cash Programme", json);
        }
        finally
        {
            // Leaving it behind would put the very directory this test exists to condemn back in
            // the working tree after every run.
            File.Delete(file);
            if (plantedTheDirectory) Directory.Delete(planted);
        }
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

    /// <summary>
    /// Exporting and importing without touching the file has to be a no-op. It was not: the export
    /// wrote "Female" for everyone, because it tested a value against 0 that the interface never
    /// writes, and the import mapped anything that was not "male" onto 1 — so one round trip
    /// through the documented bulk-edit workflow turned every woman in the register into a man.
    /// </summary>
    [Fact]
    public async Task Beneficiaries_export_and_re_import()
    {
        await AuthenticateAsync();

        async Task<Dictionary<int, int>> SexByBeneficiaryAsync()
        {
            using var scope = _factory.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<Context>()
                .Beneficiaries.AsNoTracking()
                .ToDictionaryAsync(b => b.Id, b => b.Sex);
        }

        var before = await SexByBeneficiaryAsync();
        Assert.NotEmpty(before);

        var exported = await _api.PostForBytesAsync(
            "/api/Excel/ExportBeneficiaries?organizationId=1&countryId=1", new { });

        AssertIsWorkbook(exported);

        using var workbook = new XLWorkbook(new MemoryStream(exported));
        var sheet = workbook.Worksheets.First();
        var used = sheet.RangeUsed();
        Assert.NotNull(used);
        Assert.True(used!.LastRow().RowNumber() > 1, "The export should contain at least one data row.");

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

        Assert.Equal(before, await SexByBeneficiaryAsync());
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
/// A DataTable with no columns is what an empty JSON collection turns into, so a deployment whose
/// metadata is not fully populated exports one. It has broken the whole export before — EPPlus 8
/// threw "Column out of range" where EPPlus 4 had quietly ignored it — so it stays covered across
/// the move to ClosedXML.
/// </summary>
public class ExcelExtensionsTests
{
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

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        Assert.Equal(2, workbook.Worksheets.Count);
        Assert.Equal("first", workbook.Worksheet("Populated").Cell(2, 1).GetString());
        Assert.Null(workbook.Worksheet("NoColumnsAtAll").RangeUsed());
    }

    [Fact]
    public void An_empty_table_on_its_own_still_produces_a_workbook()
    {
        var bytes = new DataTable("Empty").ToExcelSpreadsheet();

        Assert.NotEmpty(bytes);
    }

    /// <summary>
    /// What a cell comes back as is the one thing the move from EPPlus actually changed. EPPlus
    /// handed back the raw value — a date-formatted cell arrived as the serial number underneath
    /// it — where ClosedXML hands back a typed value. Import reads these, so the types matter.
    /// </summary>
    [Fact]
    public void A_written_row_reads_back_with_its_types_intact()
    {
        var table = new DataTable("Sheet1");
        table.Columns.Add("Name", typeof(string));
        table.Columns.Add("Count", typeof(int));
        table.Columns.Add("Disabled", typeof(bool));
        table.Columns.Add("CreatedDate", typeof(DateTime));
        table.Columns.Add("Missing", typeof(string));

        table.Rows.Add("Household", 3, true, new DateTime(2026, 7, 31), DBNull.Value);

        using var workbook = new XLWorkbook(new MemoryStream(table.ToExcelSpreadsheet()));
        var row = workbook.Worksheets.First().SheetToJson().Single();

        Assert.Equal("Household", row.Value<string>("Name"));
        Assert.Equal(3, row.Value<int>("Count"));
        Assert.True(row.Value<bool>("Disabled"));
        Assert.Equal(new DateTime(2026, 7, 31), row.Value<DateTime>("CreatedDate"));

        // A blank cell reads as the empty string rather than null, which is what the import's
        // "leave this property alone" checks are written against.
        Assert.Equal("", row.Value<string>("Missing"));

        // The row number is what an import reports a bad row by.
        Assert.Equal(2, row.Value<int>("__RowNumber"));
    }

    /// <summary>
    /// A date can arrive typed or as the serial number a plain-number column carries. Both have to
    /// land on the same instant, because which one you get depends on how whoever edited the sheet
    /// formatted the column.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_date_merges_whether_it_arrives_typed_or_as_a_serial_number(bool asSerialNumber)
    {
        var expected = new DateTime(2026, 7, 31, 0, 0, 0, DateTimeKind.Utc);

        var source = new JObject
        {
            ["When"] = asSerialNumber
                ? new JValue(expected.ToOADate())
                : new JValue(expected)
        };

        var target = new HasADate();
        source.MergeChangesInto(target);

        Assert.Equal(expected, target.When);
        Assert.Equal(DateTimeKind.Utc, target.When!.Value.Kind);
    }

    private class HasADate
    {
        public DateTime? When { get; set; }
    }
}
