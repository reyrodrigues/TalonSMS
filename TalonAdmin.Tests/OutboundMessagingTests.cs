using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TalonAdmin.Messaging;
using TalonAdmin.Messaging.Channels;
using TalonAdmin.Models.Admin;
using TalonAdmin.Models.Vouchers;
using TalonAdmin.Tests.Infrastructure;

namespace TalonAdmin.Tests;

/// <summary>
/// Outbound messaging used to be a hundred-line if/else inside the workflow controller, fired from
/// an unawaited task with every failure caught and discarded. These tests pin down what replaced
/// it: a message is persisted before anything is attempted, a transient failure is retried with
/// backoff, a rejection is not, and a country with no usable channel produces a visible failure
/// rather than silence.
/// </summary>
public class OutboundMessagingTests : IClassFixture<TalonAppFactory>
{
    private readonly TalonAppFactory _factory;

    private const string GatewayTemplate = "https://gateway.test/send?to={0}&content={1}";

    public OutboundMessagingTests(TalonAppFactory factory)
    {
        _factory = factory;
        // Realise the host so the databases exist and the seed has run.
        _ = factory.CreateClient();
    }

    /// <summary>
    /// The seed deliberately leaves ServiceUrl empty — a fresh install has no gateway account, and
    /// the queue is expected to say so rather than pretend. Tests that need a working channel
    /// configure one.
    /// </summary>
    private async Task ConfigureGatewayAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var admin = scope.ServiceProvider.GetRequiredService<AdminContext>();

        var settings = await admin.CountrySettings.FirstAsync();
        settings.SmsBackendType = (int)SmsBackendType.Clickatell;
        settings.ServiceUrl = GatewayTemplate;
        await admin.SaveChangesAsync();
    }

    // ---- the URL template, which is what a deployed country actually has configured ----------

    [Fact]
    public void A_clickatell_url_is_built_from_the_configured_template()
    {
        var url = ClickatellChannel.BuildUrl(
            "https://platform.clickatell.com/messages/http/send?apiKey=k&to={0}&content={1}",
            "+962790000001",
            "Voucher 123456");

        Assert.Contains("to=%2B962790000001", url);
        Assert.Contains("content=Voucher%20123456", url);
        Assert.DoesNotContain("unicode=1", url);
    }

    [Fact]
    public void Non_latin_text_is_sent_as_ucs2_hex_and_flagged_unicode()
    {
        // An Arabic voucher message arrives as mojibake unless the gateway is told it is not GSM-7.
        var url = ClickatellChannel.BuildUrl(
            "https://gateway.test/send?to={0}&content={1}",
            "+962790000001",
            "قسيمة");

        Assert.EndsWith("&unicode=1", url);

        // UCS-2 big-endian hex: five Arabic characters, four hex digits each.
        var content = url.Split("content=")[1].Split('&')[0];
        Assert.Equal(20, content.Length);
        Assert.Matches("^[0-9A-F]+$", content);
    }

    // ---- retry classification ---------------------------------------------------------------

    [Fact]
    public async Task A_gateway_outage_is_retried_but_a_rejection_is_not()
    {
        var settings = new CountrySettings { ServiceUrl = "https://gateway.test/send?to={0}&content={1}" };

        var outage = await SendThroughStubbedGatewayAsync(settings, System.Net.HttpStatusCode.ServiceUnavailable);
        Assert.False(outage.Succeeded);
        Assert.True(outage.Retryable, "A 503 is transient and should be retried.");

        var rejected = await SendThroughStubbedGatewayAsync(settings, System.Net.HttpStatusCode.Unauthorized);
        Assert.False(rejected.Succeeded);
        Assert.False(rejected.Retryable, "Bad credentials cannot be fixed by trying again.");
    }

    [Fact]
    public async Task A_channel_without_a_service_url_cannot_serve_the_country()
    {
        var channel = new ClickatellChannel(new HttpClient());

        Assert.False(channel.CanServe(new CountrySettings()));
        Assert.True(channel.CanServe(new CountrySettings { ServiceUrl = "https://gateway.test/{0}/{1}" }));

        await Task.CompletedTask;
    }

    // ---- the queue and the dispatcher --------------------------------------------------------

    [Fact]
    public async Task A_message_is_persisted_before_it_is_attempted()
    {
        await ConfigureGatewayAsync();

        using var scope = _factory.Services.CreateScope();
        var queue = scope.ServiceProvider.GetRequiredService<IOutboundMessageQueue>();
        var context = scope.ServiceProvider.GetRequiredService<Context>();

        var message = await queue.EnqueueAsync("+962790000009", "Test Recipient", "Voucher 424242", 1, 1);

        var stored = await context.OutboundMessages.FindAsync(message.Id);

        Assert.NotNull(stored);
        Assert.Equal("+962790000009", stored!.To);
        Assert.Equal("Voucher 424242", stored.Body);
        // The seeded country points at Clickatell, so the channel resolves at enqueue time.
        Assert.Equal(ClickatellChannel.ChannelKey, stored.ChannelKey);
        Assert.Equal(OutboundMessageState.Pending, stored.State);
        Assert.Equal(0, stored.Attempts);
    }

    [Fact]
    public async Task A_country_whose_gateway_is_not_configured_yet_fails_visibly()
    {
        using var scope = _factory.Services.CreateScope();
        var admin = scope.ServiceProvider.GetRequiredService<AdminContext>();
        var context = scope.ServiceProvider.GetRequiredService<Context>();
        var queue = scope.ServiceProvider.GetRequiredService<IOutboundMessageQueue>();

        var settings = await admin.CountrySettings.FirstAsync();
        var original = settings.ServiceUrl;

        try
        {
            // The state a fresh install is in: a backend is selected but no account is configured.
            settings.ServiceUrl = null;
            await admin.SaveChangesAsync();

            var message = await queue.EnqueueAsync("+962790000013", "Test", "Voucher", 1, 1);
            var stored = await context.OutboundMessages.FindAsync(message.Id);

            Assert.Equal(OutboundMessageState.Failed, stored!.State);
            Assert.Contains("country settings are incomplete", stored.LastError);
        }
        finally
        {
            settings.ServiceUrl = original;
            await admin.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task A_country_with_no_usable_channel_fails_visibly_instead_of_silently()
    {
        using var scope = _factory.Services.CreateScope();
        var admin = scope.ServiceProvider.GetRequiredService<AdminContext>();
        var context = scope.ServiceProvider.GetRequiredService<Context>();
        var queue = scope.ServiceProvider.GetRequiredService<IOutboundMessageQueue>();

        var settings = await admin.CountrySettings.FirstAsync();
        var original = settings.SmsBackendType;

        try
        {
            // 99 is not a channel any build knows about — the shape a country left on a removed
            // gateway (RescueSMS, TurboSMS) ends up in.
            settings.SmsBackendType = 99;
            await admin.SaveChangesAsync();

            var message = await queue.EnqueueAsync("+962790000010", "Test", "Voucher", 1, 1);
            var stored = await context.OutboundMessages.FindAsync(message.Id);

            Assert.Equal(OutboundMessageState.Failed, stored!.State);
            Assert.Contains("No messaging channel is configured", stored.LastError);
        }
        finally
        {
            settings.SmsBackendType = original;
            await admin.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task The_dispatcher_marks_a_sent_message_and_leaves_nothing_pending()
    {
        await ConfigureGatewayAsync();

        // A gateway that always accepts, so the batch runs to completion.
        var dispatcher = BuildDispatcher(System.Net.HttpStatusCode.OK, "ID: abc123");

        using (var scope = _factory.Services.CreateScope())
        {
            var queue = scope.ServiceProvider.GetRequiredService<IOutboundMessageQueue>();
            await queue.EnqueueAsync("+962790000011", "Test", "Voucher 999", 1, 1);
        }

        await dispatcher.DispatchBatchAsync(CancellationToken.None);

        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<Context>();
            var message = await context.OutboundMessages
                .Where(m => m.To == "+962790000011")
                .OrderByDescending(m => m.Id)
                .FirstAsync();

            Assert.Equal(OutboundMessageState.Sent, message.State);
            Assert.Equal(1, message.Attempts);
            Assert.NotNull(message.SentOn);
            Assert.Null(message.LastError);
            Assert.Equal("abc123", message.ProviderMessageId);
        }
    }

    [Fact]
    public async Task A_transient_failure_is_left_pending_with_a_backoff_and_the_reason()
    {
        await ConfigureGatewayAsync();

        var dispatcher = BuildDispatcher(System.Net.HttpStatusCode.ServiceUnavailable, "upstream down");

        using (var scope = _factory.Services.CreateScope())
        {
            var queue = scope.ServiceProvider.GetRequiredService<IOutboundMessageQueue>();
            await queue.EnqueueAsync("+962790000012", "Test", "Voucher 888", 1, 1);
        }

        var before = DateTime.UtcNow;
        await dispatcher.DispatchBatchAsync(CancellationToken.None);

        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<Context>();
            var message = await context.OutboundMessages
                .Where(m => m.To == "+962790000012")
                .OrderByDescending(m => m.Id)
                .FirstAsync();

            Assert.Equal(OutboundMessageState.Pending, message.State);
            Assert.Equal(1, message.Attempts);
            Assert.True(message.NextAttemptOn > before, "The retry should be pushed into the future.");
            Assert.Contains("503", message.LastError);
        }
    }

    // ---- helpers -----------------------------------------------------------------------------

    private static async Task<DeliveryResult> SendThroughStubbedGatewayAsync(
        CountrySettings settings,
        System.Net.HttpStatusCode status)
    {
        var channel = new ClickatellChannel(new HttpClient(new StubHandler(status, "")));

        return await channel.SendAsync(
            new OutboundMessage { To = "+962790000001", Body = "Voucher" },
            settings,
            CancellationToken.None);
    }

    /// <summary>
    /// A dispatcher wired to the application's own services but with the gateway stubbed, so a
    /// batch can be driven directly without waiting on the hosted service's poll loop.
    /// </summary>
    private OutboundMessageDispatcher BuildDispatcher(System.Net.HttpStatusCode status, string body)
    {
        var services = new ServiceCollection();

        services.AddScoped(_ => _factory.Services.CreateScope().ServiceProvider.GetRequiredService<Context>());
        services.AddScoped(_ => _factory.Services.CreateScope().ServiceProvider.GetRequiredService<AdminContext>());
        services.AddScoped<IMessageChannel>(_ =>
            new ClickatellChannel(new HttpClient(new StubHandler(status, body))));
        services.AddScoped<MessageChannelRegistry>();

        return new OutboundMessageDispatcher(
            services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            new ConfigurationBuilder().Build(),
            NullLogger<OutboundMessageDispatcher>.Instance);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly System.Net.HttpStatusCode _status;
        private readonly string _body;

        public StubHandler(System.Net.HttpStatusCode status, string body)
        {
            _status = status;
            _body = body;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(_status) { Content = new StringContent(_body) });
    }
}
