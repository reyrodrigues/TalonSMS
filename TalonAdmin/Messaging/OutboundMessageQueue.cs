using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TalonAdmin.Models.Vouchers;

namespace TalonAdmin.Messaging
{
    /// <summary>
    /// Accepts a message for delivery. Enqueuing is a database write and nothing more, so the
    /// request that triggered it never waits on a gateway and never fails because one is down.
    /// </summary>
    public interface IOutboundMessageQueue
    {
        Task<OutboundMessage> EnqueueAsync(
            string to,
            string recipientName,
            string body,
            int countryId,
            int organizationId,
            CancellationToken cancellationToken = default);
    }

    public class OutboundMessageQueue : IOutboundMessageQueue
    {
        private readonly Context _context;
        private readonly Models.Admin.AdminContext _adminContext;
        private readonly MessageChannelRegistry _registry;

        public OutboundMessageQueue(
            Context context,
            Models.Admin.AdminContext adminContext,
            MessageChannelRegistry registry)
        {
            _context = context;
            _adminContext = adminContext;
            _registry = registry;
        }

        public async Task<OutboundMessage> EnqueueAsync(
            string to,
            string recipientName,
            string body,
            int countryId,
            int organizationId,
            CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;

            var message = new OutboundMessage
            {
                To = to,
                RecipientName = recipientName,
                Body = body,
                CountryId = countryId,
                OrganizationId = organizationId,
                State = OutboundMessageState.Pending,
                CreatedOn = now,
                NextAttemptOn = now
            };

            // The channel is resolved now rather than at send time so that a misconfigured country
            // shows up in the delivery log immediately, next to the message it stopped.
            var settings = await _adminContext.Countries
                .AsNoTracking()
                .Where(c => c.Id == countryId)
                .Select(c => c.Settings)
                .FirstOrDefaultAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(to))
            {
                // A beneficiary or vendor with no mobile number on file. Worth surfacing: the
                // record needs fixing, and nothing is going to arrive until it is.
                message.State = OutboundMessageState.Failed;
                message.LastError = "The recipient has no mobile number on file.";
            }
            else if (string.IsNullOrWhiteSpace(body))
            {
                // Almost always a message template that was never filled in. Sending an empty SMS
                // is never the right answer, and the template is what needs attention.
                message.State = OutboundMessageState.Failed;
                message.LastError =
                    "The message body is empty. Check the message templates under " +
                    "Organization Administration → Country Settings.";
            }
            else if (_registry.TryResolve(settings, out var channel, out var problem))
            {
                message.ChannelKey = channel.Key;
            }
            else
            {
                message.State = OutboundMessageState.Failed;
                message.LastError = problem;
            }

            _context.OutboundMessages.Add(message);
            await _context.SaveChangesAsync(cancellationToken);

            return message;
        }
    }
}
