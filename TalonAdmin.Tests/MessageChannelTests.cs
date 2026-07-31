using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using TalonAdmin.Messaging;
using TalonAdmin.Messaging.Channels;
using TalonAdmin.Models.Admin;
using TalonAdmin.Models.Vouchers;
using TalonAdmin.Tests.Infrastructure;

namespace TalonAdmin.Tests;

/// <summary>
/// The Android gateway and Telegram channels. Both are adapters against <c>IMessageChannel</c>, so
/// what matters is the wire format they produce and how they classify a failure — a retry that
/// cannot succeed only delays the error staff need to act on.
/// </summary>
public class MessageChannelTests : IClassFixture<TalonAppFactory>
{
    private readonly TalonAppFactory _factory;

    public MessageChannelTests(TalonAppFactory factory)
    {
        _factory = factory;
        _ = factory.CreateClient();
    }

    // ---- Android gateway ---------------------------------------------------------------------

    [Theory]
    [InlineData("http://192.168.1.50:8080", "http://192.168.1.50:8080/message")]
    [InlineData("http://192.168.1.50:8080/", "http://192.168.1.50:8080/message")]
    [InlineData("https://api.sms-gate.app/3rdparty/v1/message", "https://api.sms-gate.app/3rdparty/v1/message")]
    public void The_gateway_url_accepts_either_the_api_root_or_the_send_endpoint(string configured, string expected)
    {
        // Both are plausible things to paste into a settings field.
        Assert.Equal(expected, AndroidGatewayChannel.BuildUrl(configured));
    }

    [Fact]
    public async Task The_gateway_is_sent_the_shape_the_android_app_expects()
    {
        var recorder = new RecordingHandler(System.Net.HttpStatusCode.Accepted, "{\"id\":\"gw-77\"}");
        var channel = new AndroidGatewayChannel(new HttpClient(recorder));

        var result = await channel.SendAsync(
            new OutboundMessage { To = "+962790000001", Body = "Voucher 123456" },
            new ChannelSettings {
                ServiceUrl = "http://192.168.1.50:8080",
                ServiceUser = "sms",
                ServicePassword = "device-secret"
            },
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("gw-77", result.ProviderMessageId);

        var body = JObject.Parse(recorder.LastBody!);
        Assert.Equal("Voucher 123456", body.Value<string>("message"));
        Assert.Equal("+962790000001", body["phoneNumbers"]!.Single().ToString());

        // The gateway is a send-anything endpoint on a phone with a real SIM; without auth it
        // would be an open relay.
        Assert.Equal("Basic", recorder.LastAuthScheme);
    }

    [Fact]
    public void A_gateway_without_a_password_cannot_serve_a_country()
    {
        var channel = new AndroidGatewayChannel(new HttpClient());

        Assert.False(channel.CanServe(new ChannelSettings { ServiceUrl = "http://phone:8080" }));
        Assert.False(channel.CanServe(new ChannelSettings { ServicePassword = "secret" }));
        Assert.True(channel.CanServe(new ChannelSettings {
            ServiceUrl = "http://phone:8080",
            ServicePassword = "secret"
        }));
    }

    [Theory]
    [InlineData(System.Net.HttpStatusCode.ServiceUnavailable, true)]   // handset asleep or out of signal
    [InlineData(System.Net.HttpStatusCode.RequestTimeout, true)]
    [InlineData(System.Net.HttpStatusCode.Unauthorized, false)]        // wrong device password
    [InlineData(System.Net.HttpStatusCode.BadRequest, false)]          // rejected number
    public async Task The_gateway_retries_only_what_another_attempt_could_fix(
        System.Net.HttpStatusCode status,
        bool retryable)
    {
        var channel = new AndroidGatewayChannel(new HttpClient(new RecordingHandler(status, "")));

        var result = await channel.SendAsync(
            new OutboundMessage { To = "+962790000001", Body = "Voucher" },
            new ChannelSettings { ServiceUrl = "http://phone:8080", ServicePassword = "secret" },
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(retryable, result.Retryable);
    }

    // ---- Telegram enrolment ------------------------------------------------------------------

    [Fact]
    public void An_enrolment_payload_round_trips_its_country_and_number()
    {
        var enrolment = NewEnrolment("a-signing-key");

        var payload = enrolment.CreatePayload(7, "+962 79 000 0001");

        Assert.True(enrolment.TryReadPayload(payload, out var countryId, out var number));
        Assert.Equal(7, countryId);
        Assert.Equal("962790000001", number);
    }

    [Fact]
    public void An_enrolment_payload_fits_telegrams_start_parameter()
    {
        // Telegram caps `start` at 64 characters and allows only A-Z a-z 0-9 _ and -.
        var payload = NewEnrolment("a-signing-key").CreatePayload(1234, "+9627912345678");

        Assert.True(payload.Length <= 64, $"Payload was {payload.Length} characters.");
        Assert.Matches("^[A-Za-z0-9_~-]+$", payload);
    }

    [Fact]
    public void A_tampered_or_foreign_enrolment_payload_is_rejected()
    {
        var ours = NewEnrolment("our-signing-key");
        var payload = ours.CreatePayload(1, "+962790000001");

        // Anyone who could mint a payload could point someone else's voucher codes at their own
        // Telegram account, so the tag has to be checked, not just parsed.
        var swapped = payload[..1] + (payload[1] == 'A' ? 'B' : 'A') + payload[2..];
        Assert.False(ours.TryReadPayload(swapped, out _, out _));

        Assert.False(ours.TryReadPayload(NewEnrolment("a-different-key").CreatePayload(1, "+962790000001"),
            out _, out _));

        Assert.False(ours.TryReadPayload("not-a-payload", out _, out _));
        Assert.False(ours.TryReadPayload("", out _, out _));
    }

    [Theory]
    [InlineData("+962790000001")]
    [InlineData("00962790000001")]
    [InlineData("962 79 000 0001")]
    public void Numbers_written_differently_normalise_to_the_same_contact(string written)
    {
        Assert.Equal("962790000001", TelegramEnrolment.Normalize(written));
    }

    // ---- Telegram delivery -------------------------------------------------------------------

    [Fact]
    public async Task Telegram_reports_an_unenrolled_recipient_as_unroutable_so_the_next_channel_tries()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<Context>();

        var channel = new TelegramChannel(new HttpClient(new RecordingHandler(System.Net.HttpStatusCode.OK, "{}")), context);

        var result = await channel.SendAsync(
            new OutboundMessage { To = "+962790009999", Body = "Voucher", CountryId = 1 },
            new ChannelSettings { ServicePassword = "bot-token" },
            CancellationToken.None);

        Assert.False(result.Succeeded);
        // Waiting will not enrol them, so this must not sit in the queue retrying — but an SMS
        // channel behind Telegram in the chain still can reach them, so it is not a rejection.
        Assert.False(result.Retryable);
        Assert.Equal(DeliveryOutcome.Unroutable, result.Outcome);
        Assert.Contains("has not enrolled", result.Error);
    }

    [Fact]
    public async Task Telegram_sends_to_the_chat_the_recipient_enrolled_with()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<Context>();

        context.TelegramContacts.Add(new TelegramContact
        {
            CountryId = 1,
            NormalizedNumber = "962790001234",
            MobileNumber = "+962790001234",
            ChatId = 555000111,
            EnrolledOn = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var recorder = new RecordingHandler(
            System.Net.HttpStatusCode.OK, "{\"ok\":true,\"result\":{\"message_id\":42}}");
        var channel = new TelegramChannel(new HttpClient(recorder), context);

        var result = await channel.SendAsync(
            new OutboundMessage { To = "+962 790 001 234", Body = "Voucher 654321", CountryId = 1 },
            new ChannelSettings { ServicePassword = "bot-token", ServiceUrl = "https://telegram.test" },
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("42", result.ProviderMessageId);
        Assert.Equal("https://telegram.test/botbot-token/sendMessage", recorder.LastUrl);

        var body = JObject.Parse(recorder.LastBody!);
        Assert.Equal(555000111, body.Value<long>("chat_id"));
        Assert.Equal("Voucher 654321", body.Value<string>("text"));
    }

    [Fact]
    public async Task A_blocked_bot_is_not_retried_but_a_rate_limit_is()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<Context>();

        context.TelegramContacts.Add(new TelegramContact
        {
            CountryId = 1,
            NormalizedNumber = "962790005678",
            MobileNumber = "+962790005678",
            ChatId = 555000222,
            EnrolledOn = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var message = new OutboundMessage { To = "+962790005678", Body = "Voucher", CountryId = 1 };
        var settings = new ChannelSettings { ServicePassword = "bot-token" };

        var blocked = await new TelegramChannel(
                new HttpClient(new RecordingHandler(System.Net.HttpStatusCode.Forbidden,
                    "{\"ok\":false,\"description\":\"Forbidden: bot was blocked by the user\"}")),
                context)
            .SendAsync(message, settings, CancellationToken.None);

        Assert.False(blocked.Retryable);
        Assert.Contains("blocked by the user", blocked.Error);

        var throttled = await new TelegramChannel(
                new HttpClient(new RecordingHandler(System.Net.HttpStatusCode.TooManyRequests,
                    "{\"ok\":false,\"description\":\"Too Many Requests: retry after 30\"}")),
                context)
            .SendAsync(message, settings, CancellationToken.None);

        Assert.True(throttled.Retryable);
    }

    // ---- the chain -----------------------------------------------------------------------------

    [Fact]
    public void A_country_without_a_chain_property_still_uses_its_single_backend()
    {
        // Every country configured before chains existed has to keep working untouched.
        var keys = MessageChannelRegistry.KeysFor(new CountrySettings
        {
            SmsBackendType = (int)SmsBackendType.Clickatell
        });

        Assert.Equal(new[] { ClickatellChannel.ChannelKey }, keys);
    }

    [Fact]
    public void A_chain_property_overrides_the_single_backend_and_keeps_its_order()
    {
        var settings = new CountrySettings { SmsBackendType = (int)SmsBackendType.Clickatell };
        settings.PropertyCollection.Add(new CountrySettingsProperty
        {
            Name = MessageChannelRegistry.ChainPropertyName,
            Value = "telegram, sms.android, telegram"
        });

        // Order is the whole point, and a key repeated by hand must not double-send.
        Assert.Equal(
            new[] { TelegramChannel.ChannelKey, AndroidGatewayChannel.ChannelKey },
            MessageChannelRegistry.KeysFor(settings));
    }

    [Fact]
    public void A_channel_falls_back_to_the_country_credentials_only_when_it_has_none_of_its_own()
    {
        var country = new CountrySettings
        {
            ServiceUrl = "https://primary.test",
            ServiceUser = "primary",
            ServicePassword = "primary-secret"
        };
        country.PropertyCollection.Add(new CountrySettingsProperty
        {
            Name = "Channel telegram Password",
            Value = "bot-token"
        });

        // Telegram brought its own, so it must not inherit the SMS gateway's URL as its API base.
        var telegram = ChannelSettings.For(country, TelegramChannel.ChannelKey);
        Assert.Equal("bot-token", telegram.ServicePassword);
        Assert.Null(telegram.ServiceUrl);

        // Clickatell brought none, so it uses the country's own three fields.
        var clickatell = ChannelSettings.For(country, ClickatellChannel.ChannelKey);
        Assert.Equal("https://primary.test", clickatell.ServiceUrl);
        Assert.Equal("primary-secret", clickatell.ServicePassword);
    }

    [Fact]
    public void A_chain_drops_channels_that_cannot_serve_and_says_why()
    {
        var registry = new MessageChannelRegistry(new IMessageChannel[]
        {
            new TelegramChannel(new HttpClient(), null),
            new ClickatellChannel(new HttpClient())
        });

        var country = new CountrySettings { ServiceUrl = "https://gateway.test/{0}/{1}" };
        country.PropertyCollection.Add(new CountrySettingsProperty
        {
            Name = MessageChannelRegistry.ChainPropertyName,
            Value = "telegram,sms.clickatell"
        });

        // Telegram is listed but has no bot token, so it is dropped rather than being tried and
        // failing every message on the way past.
        var chain = registry.ResolveChain(country, out var problems);

        Assert.Equal(new[] { ClickatellChannel.ChannelKey }, chain.Select(c => c.Key));
        Assert.Contains(problems, p => p.Contains("telegram") && p.Contains("incomplete"));
    }

    // ---- registry ------------------------------------------------------------------------------

    [Theory]
    [InlineData(SmsBackendType.Clickatell, ClickatellChannel.ChannelKey)]
    [InlineData(SmsBackendType.AndroidGateway, AndroidGatewayChannel.ChannelKey)]
    [InlineData(SmsBackendType.Telegram, TelegramChannel.ChannelKey)]
    public void Each_backend_value_maps_to_its_channel(SmsBackendType backend, string expectedKey)
    {
        Assert.Equal(expectedKey, MessageChannelRegistry.KeyFor(new CountrySettings
        {
            SmsBackendType = (int)backend
        }));
    }

    [Fact]
    public void A_removed_backend_maps_to_nothing_rather_than_falling_through_to_a_default()
    {
        // 0 and 1 belonged to gateways that have since been removed. Silently rerouting a country
        // left on one of them would send its voucher codes through the wrong gateway.
        Assert.Null(MessageChannelRegistry.KeyFor(new CountrySettings { SmsBackendType = 0 }));
        Assert.Null(MessageChannelRegistry.KeyFor(new CountrySettings { SmsBackendType = 1 }));
        Assert.Null(MessageChannelRegistry.KeyFor(new CountrySettings { SmsBackendType = 99 }));
    }

    // ---- helpers -------------------------------------------------------------------------------

    private static TelegramEnrolment NewEnrolment(string key) =>
        new(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:SigningKey"] = key })
            .Build());

    /// <summary>Captures what was actually put on the wire, and answers with a canned response.</summary>
    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly System.Net.HttpStatusCode _status;
        private readonly string _body;

        public RecordingHandler(System.Net.HttpStatusCode status, string body)
        {
            _status = status;
            _body = body;
        }

        public string? LastUrl { get; private set; }
        public string? LastBody { get; private set; }
        public string? LastAuthScheme { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastUrl = request.RequestUri?.ToString();
            LastAuthScheme = request.Headers.Authorization?.Scheme;
            LastBody = request.Content == null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(_status) { Content = new StringContent(_body) };
        }
    }
}
