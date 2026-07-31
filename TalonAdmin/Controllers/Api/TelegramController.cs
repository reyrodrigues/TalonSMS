using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using TalonAdmin.Auth;
using TalonAdmin.Extensions;
using TalonAdmin.Messaging;
using TalonAdmin.Models.Vouchers;

namespace TalonAdmin.Controllers.Api
{
    /// <summary>
    /// Receives Telegram Bot API updates.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Its job is enrolment. A bot cannot message anyone who has not written to it first, so a
    /// beneficiary opens a link that sends <c>/start &lt;payload&gt;</c>; this endpoint verifies
    /// the payload and records the chat id against their phone number, after which
    /// <see cref="Messaging.Channels.TelegramChannel"/> can reach them.
    /// </para>
    /// <para>
    /// The route mirrors the inbound SMS webhook — country in the path, shared secret in the query
    /// — because both are called by an outside party and are authenticated the same way. Telegram
    /// will only call a URL you register with <c>setWebhook</c>, and it must be HTTPS.
    /// </para>
    /// </remarks>
    [Route("api/Telegram")]
    public class TelegramController : ControllerBase
    {
        private readonly Context _context;
        private readonly Models.Admin.AdminContext _adminContext;
        private readonly TelegramEnrolment _enrolment;
        private readonly InboundMessageProcessor _inboundMessages;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<TelegramController> _logger;

        public TelegramController(
            Context context,
            Models.Admin.AdminContext adminContext,
            TelegramEnrolment enrolment,
            InboundMessageProcessor inboundMessages,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<TelegramController> logger)
        {
            _context = context;
            _adminContext = adminContext;
            _enrolment = enrolment;
            _inboundMessages = inboundMessages;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// The enrolment link for one recipient, for staff to pass on.
        /// </summary>
        /// <remarks>
        /// Returns 404 when the active country is not on Telegram or has no bot username recorded,
        /// which is what lets the UI show the button only where it means something. The bot
        /// username is a country setting property, <c>Telegram Bot Username</c>.
        /// </remarks>
        [HttpGet("EnrolmentLink")]
        [Authorize]
        public async Task<IActionResult> EnrolmentLink([FromQuery] string mobileNumber)
        {
            if (string.IsNullOrWhiteSpace(mobileNumber))
            {
                return BadRequest("A mobile number is required.");
            }

            var countryId = this.GetCountryId();

            var country = await _adminContext.Countries
                .AsNoTracking()
                .Include(c => c.Settings)
                .ThenInclude(s => s.PropertyCollection)
                .Where(c => c.Id == countryId)
                .FirstOrDefaultAsync();

            if (country?.Settings == null
                || country.Settings.SmsBackendType != (int)Models.Admin.SmsBackendType.Telegram)
            {
                return NotFound("This country does not deliver over Telegram.");
            }

            var botUserName = country.Settings.PropertyCollection
                .Where(p => p.Name == "Telegram Bot Username")
                .Select(p => p.Value)
                .FirstOrDefault();

            if (string.IsNullOrWhiteSpace(botUserName))
            {
                return NotFound("No Telegram bot username is configured for this country.");
            }

            var normalized = TelegramEnrolment.Normalize(mobileNumber);

            var enrolled = await _context.TelegramContacts
                .AsNoTracking()
                .AnyAsync(c => c.CountryId == countryId && c.NormalizedNumber == normalized);

            return new JsonResult(new
            {
                Link = _enrolment.CreateLink(botUserName.TrimStart('@'), countryId, mobileNumber),
                Enrolled = enrolled
            });
        }

        // Redemption is reachable from here as well as from the SMS webhook, so it carries the same
        // flood guard. Without it this route would be the unlimited way in to guessing voucher codes.
        [HttpPost("{countryCode}")]
        [AllowAnonymous]
        [EnableRateLimiting(RateLimitPolicies.SmsRedemption)]
        public async Task<IActionResult> Update(
            [FromBody] JObject update,
            string countryCode,
            [FromQuery] string secret = "")
        {
            var systemSecret = _configuration["SystemSecret"] ?? "";
            if (systemSecret != secret && !string.IsNullOrEmpty(systemSecret))
            {
                return BadRequest("Secret is invalid.");
            }

            var country = await _adminContext.Countries
                .AsNoTracking()
                .Where(c => c.IsoAlpha2.ToLower() == countryCode.ToLower().Trim())
                .Select(c => new { c.Id, c.Settings.ServicePassword, c.Settings.ServiceUrl })
                .FirstOrDefaultAsync();

            if (country == null)
            {
                return Ok("Not a valid country");
            }

            var text = update?["message"]?["text"]?.ToString();
            var chat = update?["message"]?["chat"];

            // Telegram sends many update kinds — edits, joins, callbacks. Anything without a chat
            // and a body is not something this bot acts on, and must still be answered with 200 or
            // Telegram will retry it indefinitely.
            if (chat == null || string.IsNullOrWhiteSpace(text))
            {
                return Ok();
            }

            var chatId = chat.Value<long>("id");
            var userName = chat.Value<string>("username");

            if (text.StartsWith("/start", StringComparison.OrdinalIgnoreCase))
            {
                var payload = text.Length > 6 ? text[6..].Trim() : "";
                var reply = await EnrolAsync(payload, country.Id, chatId, userName);

                await ReplyAsync(country.ServiceUrl, country.ServicePassword, chatId, reply);
                return Ok();
            }

            // Anything else is a redemption attempt, and goes through exactly the same rules as an
            // inbound SMS. The processor identifies the vendor by phone number, so the chat has to
            // be resolved back to one first — which is only possible for someone who enrolled.
            var contact = await _context.TelegramContacts
                .AsNoTracking()
                .Where(c => c.CountryId == country.Id && c.ChatId == chatId)
                .FirstOrDefaultAsync();

            if (contact == null)
            {
                await ReplyAsync(country.ServiceUrl, country.ServicePassword, chatId,
                    "This chat is not linked to a registered number. Ask your programme staff for "
                    + "an enrolment link.");

                return Ok();
            }

            var result = await _inboundMessages.ProcessAsync(
                contact.MobileNumber, text, country.Id);

            // A successful redemption already queues the country's own "Vendor Confirmed" template,
            // which comes back through this same chat. Replying here as well would say it twice, in
            // two different sets of words.
            if (!result.WasRedeemed)
            {
                await ReplyAsync(country.ServiceUrl, country.ServicePassword, chatId,
                    "That did not look like a voucher redemption. Send the voucher code followed by "
                    + "the beneficiary's identifier.");
            }

            return Ok();
        }

        private async Task<string> EnrolAsync(string payload, int countryId, long chatId, string userName)
        {
            if (!_enrolment.TryReadPayload(payload, out var payloadCountryId, out var number)
                || payloadCountryId != countryId)
            {
                _logger.LogWarning("Rejected a Telegram enrolment payload for country {CountryId}.", countryId);
                return "That enrolment link is not valid. Please ask your programme staff for a new one.";
            }

            var existing = await _context.TelegramContacts
                .Where(c => c.CountryId == countryId && c.NormalizedNumber == number)
                .FirstOrDefaultAsync();

            if (existing == null)
            {
                // OrganizationId is left at 0: enrolment is a property of the person and the
                // country, and the same number may be served by more than one programme.
                _context.TelegramContacts.Add(new TelegramContact
                {
                    CountryId = countryId,
                    NormalizedNumber = number,
                    MobileNumber = number,
                    ChatId = chatId,
                    UserName = userName,
                    EnrolledOn = DateTime.UtcNow
                });
            }
            else
            {
                // Re-enrolling moves the number to whichever account tapped the link most recently,
                // which is what happens when someone changes phone.
                existing.ChatId = chatId;
                existing.UserName = userName;
                existing.EnrolledOn = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            return "You are enrolled. Your voucher codes will arrive here.";
        }

        /// <summary>
        /// Best-effort acknowledgement. A failure here must not fail the webhook, or Telegram will
        /// redeliver the update and the enrolment will be applied again.
        /// </summary>
        private async Task ReplyAsync(string apiBase, string botToken, long chatId, string text)
        {
            if (string.IsNullOrWhiteSpace(botToken))
            {
                return;
            }

            var root = string.IsNullOrWhiteSpace(apiBase)
                ? Messaging.Channels.TelegramChannel.DefaultApiBase
                : apiBase.TrimEnd('/');

            try
            {
                var body = new JObject { ["chat_id"] = chatId, ["text"] = text };
                using var content = new StringContent(body.ToString(), Encoding.UTF8, "application/json");

                using var response = await _httpClientFactory.CreateClient()
                    .PostAsync($"{root}/bot{botToken}/sendMessage", content);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Could not acknowledge a Telegram update.");
            }
        }
    }
}
