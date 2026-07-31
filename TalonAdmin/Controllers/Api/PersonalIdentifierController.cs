using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TalonAdmin.Extensions;

namespace TalonAdmin.Controllers.Api
{
    /// <summary>
    /// Issues the identifier a beneficiary gives a vendor when redeeming a voucher.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The field was called NationalId and was expected to hold a government document number. That
    /// is a poor fit for the places Talon runs: many of the people a cash programme reaches have no
    /// such document, and writing a real one into a voucher database — where a vendor reads it out
    /// to redeem — creates a protection risk for no benefit. All the redemption check needs is a
    /// number the beneficiary knows and an attacker does not.
    /// </para>
    /// <para>
    /// So a programme can issue one instead. It is drawn from a cryptographic RNG and checked for
    /// collisions within the country, because two beneficiaries sharing an identifier would let a
    /// vendor redeem one person's voucher against the other's.
    /// </para>
    /// </remarks>
    [Authorize]
    [Route("api/PersonalIdentifier")]
    public class PersonalIdentifierController : ControllerBase
    {
        /// <summary>
        /// Short enough to read down a phone line and write on a card, long enough that guessing it
        /// is not worth trying. Ten digits is the default.
        /// </summary>
        private const int MinimumLength = 8;
        private const int MaximumLength = 10;
        private const int DefaultLength = 10;

        /// <summary>
        /// Bounded so a country whose identifier space is nearly full fails loudly rather than
        /// spinning. At eight digits with a realistic caseload, a collision is already rare.
        /// </summary>
        private const int MaximumDraws = 50;

        private readonly Models.Vouchers.Context _context;

        public PersonalIdentifierController(Models.Vouchers.Context context)
        {
            _context = context;
        }

        [HttpGet("Generate")]
        public async Task<IActionResult> Generate([FromQuery] int length = DefaultLength)
        {
            if (length < MinimumLength || length > MaximumLength)
            {
                return BadRequest(
                    $"An identifier must be between {MinimumLength} and {MaximumLength} digits.");
            }

            var countryId = this.GetCountryId();

            for (var attempt = 0; attempt < MaximumDraws; attempt++)
            {
                var candidate = Draw(length);

                var taken = await _context.Beneficiaries
                    .AsNoTracking()
                    .AnyAsync(b => b.CountryId == countryId && b.PersonalIdentifier == candidate);

                if (!taken)
                {
                    return new JsonResult(new { Identifier = candidate });
                }
            }

            return Conflict(
                "Could not find an unused identifier. Try a longer one, or check whether existing " +
                "records are sharing identifiers.");
        }

        /// <summary>
        /// A uniformly distributed number with exactly <paramref name="length"/> digits.
        /// </summary>
        /// <remarks>
        /// The range starts at the smallest number of that length rather than zero, so an identifier
        /// never comes back as "0000004217" — which is a valid draw but reads like a mistake and
        /// invites someone to trim the zeros. <see cref="RandomNumberGenerator.GetInt32"/> is
        /// uniform over the range and drawn from the OS entropy source.
        /// </remarks>
        private static string Draw(int length)
        {
            var lowest = (int)Math.Pow(10, length - 1);

            // GetInt32's exclusive bound is an int, so the top of a 10-digit range does not fit.
            // Drawing the leading digit and the remainder separately keeps every draw uniform.
            var leading = RandomNumberGenerator.GetInt32(1, 10);
            var rest = length == 1 ? 0 : RandomNumberGenerator.GetInt32(0, lowest);

            return $"{leading}{rest.ToString().PadLeft(length - 1, '0')}";
        }
    }
}
