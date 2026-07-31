using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TalonAdmin.Messaging;
using TalonAdmin.Models.Vouchers;
using TalonAdmin.Tests.Infrastructure;

namespace TalonAdmin.Tests;

/// <summary>
/// Redemption used to live inside <c>VoucherWorkflowController.ValidateTransactionSMS</c>, which
/// meant an SMS gateway posting to that one action was the only way to redeem a voucher. These
/// drive the extracted processor directly — the same path the Telegram webhook takes — to pin down
/// that the rules travel with it.
/// </summary>
public class InboundRedemptionTests : IClassFixture<SeededTalonAppFactory>
{
    private readonly SeededTalonAppFactory _factory;

    public InboundRedemptionTests(SeededTalonAppFactory factory)
    {
        _factory = factory;
        _ = factory.CreateClient();
    }

    [Fact]
    public async Task A_vendor_can_redeem_a_voucher_by_replying_to_the_bot()
    {
        // The point of pulling redemption out of the inbound-SMS controller action: the rules no
        // longer care what carried the message.
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<Context>();
        var processor = scope.ServiceProvider.GetRequiredService<InboundMessageProcessor>();

        var vendor = await context.Vendors.FirstAsync();
        var voucher = await context.Vouchers
            .Include(v => v.TransactionRecords).ThenInclude(t => t.Beneficiary)
            .Where(v => v.Status == 1 && v.TransactionRecords.Any())
            .FirstAsync();

        var beneficiary = voucher.IssuingTransactionRecord.Beneficiary;

        // Exactly what the Telegram webhook does once it has resolved the chat to a number.
        var result = await processor.ProcessAsync(
            vendor.MobileNumber,
            $"{voucher.VoucherCode} {beneficiary.PersonalIdentifier}",
            vendor.CountryId);

        Assert.True(result.WasRedeemed, result.Reason);
        Assert.Matches("^[0-9]{6}$", result.ConfirmationCode);

        var settled = await context.Vouchers
            .Include(v => v.TransactionRecords)
            .FirstAsync(v => v.Id == voucher.Id);

        Assert.Equal(3, settled.Status);
        Assert.Contains(settled.TransactionRecords, t => t.Type == 2 && t.VendorId == vendor.Id);
    }

    [Fact]
    public async Task A_redemption_with_the_wrong_identifier_is_refused_whatever_carried_it()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<Context>();
        var processor = scope.ServiceProvider.GetRequiredService<InboundMessageProcessor>();

        var vendor = await context.Vendors.FirstAsync();
        var voucher = await context.Vouchers
            .Where(v => v.Status == 1 && v.TransactionRecords.Any())
            .FirstAsync();

        var result = await processor.ProcessAsync(
            vendor.MobileNumber, $"{voucher.VoucherCode} 000000000", vendor.CountryId);

        Assert.False(result.WasRedeemed);

        var untouched = await context.Vouchers.FirstAsync(v => v.Id == voucher.Id);
        Assert.Equal(1, untouched.Status);
    }

}
