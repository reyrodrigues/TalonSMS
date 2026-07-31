using TalonAdmin.Messaging;
using TalonAdmin.Models.Vouchers;

namespace TalonAdmin.Tests;

/// <summary>
/// The message-template renderer, and the worked examples the documentation tells people to copy.
/// </summary>
/// <remarks>
/// Templates are the only part of the product an operator authors, they are written once before a
/// distribution, and a mistake in one is invisible: an expression naming a field that is not there
/// renders as nothing and the message still sends. So the trap is pinned here deliberately rather
/// than left as an implementation detail, and the examples in <c>docs/MESSAGING.md</c> are rendered
/// rather than trusted — a documented template that silently drops its voucher code would be worse
/// than no documentation at all.
/// </remarks>
public class MessageTemplateTests
{
    private static object IssueModel() => new
    {
        Voucher = new Voucher { VoucherCode = "482913", Value = 50m },
        Vendor = new Vendor { Name = "Al Mahatta Grocery" },
        Beneficiary = new Beneficiary { FirstName = "Tariq", LastName = "Jaber" }
    };

    [Fact]
    public void The_documented_voucher_message_renders_its_code_and_value()
    {
        var body = VoucherNotifications.Render(
            "@Model.Beneficiary.FirstName: your voucher code is @Model.Voucher.VoucherCode, " +
            "worth @Model.Voucher.Value. Show this code at a participating shop.",
            IssueModel());

        Assert.Equal(
            "Tariq: your voucher code is 482913, worth 50. Show this code at a participating shop.",
            body);
    }

    [Fact]
    public void The_documented_confirmation_names_the_vendor()
    {
        var body = VoucherNotifications.Render(
            "Your voucher @Model.Voucher.VoucherCode was used at @Model.Vendor.Name. Thank you.",
            IssueModel());

        Assert.Equal("Your voucher 482913 was used at Al Mahatta Grocery. Thank you.", body);
    }

    [Fact]
    public void Beneficiary_Name_joins_the_two_parts()
    {
        Assert.Equal("Tariq Jaber", VoucherNotifications.Render("@Model.Beneficiary.Name", IssueModel()));
    }

    [Fact]
    public void The_parenthesised_form_ends_the_expression_where_the_bracket_does()
    {
        // Without the brackets the trailing text would be read as part of the field name.
        Assert.Equal(
            "Code 482913.",
            VoucherNotifications.Render("Code @(Model.Voucher.VoucherCode).", IssueModel()));
    }

    [Fact]
    public void A_misspelled_field_renders_as_nothing_rather_than_failing()
    {
        // The documented trap: the field is VoucherCode, not Code. Nothing rejects this template,
        // and every household gets a message with no code in it.
        Assert.Equal(
            "Your voucher code is .",
            VoucherNotifications.Render("Your voucher code is @Model.Voucher.Code.", IssueModel()));
    }

    [Fact]
    public void A_relationship_the_workflow_did_not_load_renders_as_nothing()
    {
        // Why the documentation says to stay on the record's own fields: lazy loading is off, so a
        // navigation the query did not Include is simply null by the time a template sees it.
        Assert.Equal(
            "Programme: .",
            VoucherNotifications.Render("Programme: @Model.Voucher.Distribution.Title.", IssueModel()));
    }

    [Fact]
    public void A_record_the_message_does_not_carry_renders_as_nothing()
    {
        // The shape Invalid Voucher Message is sent with: no voucher, no beneficiary.
        var model = new { Voucher = new { }, Vendor = new Vendor { Name = "Al Mahatta Grocery" }, Beneficiary = new { }, VoucherCode = "999999" };

        Assert.Equal(
            "Hello , code  was not recognised.",
            VoucherNotifications.Render(
                "Hello @Model.Beneficiary.FirstName, code @Model.Voucher.VoucherCode was not recognised.",
                model));

        // Which is why that template has to reach for the code the vendor typed instead.
        Assert.Equal(
            "Code 999999 was not recognised.",
            VoucherNotifications.Render("Code @Model.VoucherCode was not recognised.", model));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void An_unwritten_template_stays_empty_so_the_queue_can_fail_it(string? template)
    {
        // An empty body is what OutboundMessageQueue turns into a visible failure, rather than
        // sending a blank text message.
        Assert.True(string.IsNullOrEmpty(VoucherNotifications.Render(template, IssueModel())));
    }

    [Fact]
    public void Text_around_the_expressions_is_left_alone()
    {
        // There is no Razor here: an @ that is not one of these expressions is just a character.
        Assert.Equal(
            "Questions? mail help@example.org — 100% free.",
            VoucherNotifications.Render("Questions? mail help@example.org — 100% free.", IssueModel()));
    }
}
