using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using TalonAdmin.Auth;
using TalonAdmin.Tests.Infrastructure;

namespace TalonAdmin.Tests;

/// <summary>
/// An application configured exactly like the others except that <c>SystemSecret</c> is absent.
/// </summary>
public class UnsecuredTalonAppFactory : SeededTalonAppFactory
{
    protected override string? ConfiguredSystemSecret => null;
}

/// <summary>
/// The two inbound webhooks are anonymous — a gateway cannot hold a user's credentials — and what
/// arrives on them redeems value. The shared secret is the only thing standing between the two.
///
/// It used to be skipped when unset: <c>if (expected != presented &amp;&amp; expected != "")</c>
/// meant a deployment that had never configured a secret accepted a redemption from anyone who
/// found the URL. These tests pin the fail-closed behaviour that replaced it.
/// </summary>
public class InboundWebhookSecretTests
{
    private static StringContent Body() =>
        new(new JObject
        {
            ["to"] = "+962790000001",
            ["from"] = "+962790000001",
            ["message"] = "000000 000000"
        }.ToString(), Encoding.UTF8, "application/json");

    private static IConfiguration Configured(string? secret) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["SystemSecret"] = secret })
            .Build();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void An_unset_secret_is_not_a_check_that_passes(string? configured)
    {
        // Whatever a caller presents, including the empty string that would have matched.
        Assert.Equal(
            InboundWebhookSecret.Result.NotConfigured,
            InboundWebhookSecret.Check(Configured(configured), ""));

        Assert.Equal(
            InboundWebhookSecret.Result.NotConfigured,
            InboundWebhookSecret.Check(Configured(configured), "anything"));
    }

    [Fact]
    public void A_configured_secret_admits_only_itself()
    {
        var configuration = Configured("the-real-secret");

        Assert.Equal(InboundWebhookSecret.Result.Ok,
            InboundWebhookSecret.Check(configuration, "the-real-secret"));

        Assert.Equal(InboundWebhookSecret.Result.Invalid,
            InboundWebhookSecret.Check(configuration, "the-real-secre"));

        Assert.Equal(InboundWebhookSecret.Result.Invalid,
            InboundWebhookSecret.Check(configuration, "THE-REAL-SECRET"));

        Assert.Equal(InboundWebhookSecret.Result.Invalid,
            InboundWebhookSecret.Check(configuration, ""));
    }

    public class WhenConfigured : IClassFixture<SeededTalonAppFactory>
    {
        private readonly SeededTalonAppFactory _factory;

        public WhenConfigured(SeededTalonAppFactory factory) => _factory = factory;

        [Fact]
        public async Task The_sms_webhook_refuses_a_wrong_secret()
        {
            using var response = await _factory.CreateClient().PostAsync(
                "/api/VoucherWorkflow/ValidateTransactionSMS/jo?secret=not-the-secret", Body());

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task The_sms_webhook_refuses_a_missing_secret()
        {
            using var response = await _factory.CreateClient().PostAsync(
                "/api/VoucherWorkflow/ValidateTransactionSMS/jo", Body());

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task The_telegram_webhook_refuses_a_wrong_secret()
        {
            using var response = await _factory.CreateClient().PostAsync(
                "/api/Telegram/jo?secret=not-the-secret", Body());

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task The_right_secret_gets_through_to_the_rules()
        {
            // The voucher code is nonsense, so nothing is redeemed. What matters is that the answer
            // comes from the redemption rules rather than the guard: a 200 carrying their verdict,
            // not a rejection.
            using var response = await _factory.CreateClient().PostAsync(
                $"/api/VoucherWorkflow/ValidateTransactionSMS/jo?secret={TalonAppFactory.SystemSecret}",
                Body());

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var body = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain(InboundWebhookSecret.InvalidMessage, body);
            Assert.DoesNotContain(InboundWebhookSecret.NotConfiguredMessage, body);
        }
    }

    public class WhenNotConfigured : IClassFixture<UnsecuredTalonAppFactory>
    {
        private readonly UnsecuredTalonAppFactory _factory;

        public WhenNotConfigured(UnsecuredTalonAppFactory factory) => _factory = factory;

        [Fact]
        public async Task The_sms_webhook_refuses_everything()
        {
            using var response = await _factory.CreateClient().PostAsync(
                "/api/VoucherWorkflow/ValidateTransactionSMS/jo", Body());

            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        }

        [Fact]
        public async Task The_sms_webhook_refuses_an_empty_secret_that_would_once_have_matched()
        {
            using var response = await _factory.CreateClient().PostAsync(
                "/api/VoucherWorkflow/ValidateTransactionSMS/jo?secret=", Body());

            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        }

        [Fact]
        public async Task The_telegram_webhook_refuses_everything()
        {
            using var response = await _factory.CreateClient().PostAsync(
                "/api/Telegram/jo", Body());

            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        }
    }
}
