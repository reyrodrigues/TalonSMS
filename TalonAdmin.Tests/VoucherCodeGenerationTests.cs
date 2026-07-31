using TalonAdmin.Controllers.Api;

namespace TalonAdmin.Tests;

/// <summary>
/// Voucher codes are bearer instruments — holding one is what entitles someone to redeem it — so
/// they have to be unpredictable and spread evenly over the whole code space. The original
/// generator rendered eight cryptographic bytes as a decimal string and cut a substring out of it,
/// which skewed the distribution and threw outright at longer code lengths. These tests pin the
/// properties that made it worth replacing; each one fails against that implementation.
/// </summary>
public class VoucherCodeGenerationTests
{
    /// <summary>
    /// The old generator drew its bytes with GetNonZeroBytes and inherited the leading-digit skew of
    /// the resulting decimal rendering: over this same sample, 6-digit codes beginning with '1' came
    /// up 13.3% of the time against the 10% a uniform draw gives. Every digit narrower than uniform
    /// is search space an attacker does not have to cover, so the tolerance here is deliberately
    /// tight — the replacement lands inside 0.5 points, and the old code was more than six times
    /// outside it.
    /// </summary>
    [Theory]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(8)]
    public void Leading_digits_are_uniformly_distributed(int digits)
    {
        const int draws = 200_000;
        var counts = new int[10];

        for (var i = 0; i < draws; i++)
        {
            var code = RandomNumber.RandomLong(digits).ToString("D" + digits);
            counts[code[0] - '0']++;
        }

        for (var digit = 0; digit < 10; digit++)
        {
            var percentage = counts[digit] * 100.0 / draws;

            Assert.True(
                Math.Abs(percentage - 10.0) < 0.5,
                $"Codes of length {digits} began with '{digit}' {percentage:F2}% of the time; "
                + "a uniform draw gives 10% and the generator should stay within half a point.");
        }
    }

    /// <summary>
    /// A code length is only useful if it can actually produce every code of that length. The
    /// substring approach could not reach the ends of its range, so this also guards the formatting
    /// contract the callers rely on: the value always renders to exactly <c>digits</c> characters,
    /// including the leading-zero codes that a naive implementation would skip.
    /// </summary>
    [Fact]
    public void Every_code_in_a_small_space_is_reachable_and_correctly_formatted()
    {
        const int digits = 4;
        var seen = new HashSet<long>();

        for (var i = 0; i < 500_000; i++)
        {
            var value = RandomNumber.RandomLong(digits);
            Assert.InRange(value, 0, 9_999);
            Assert.Equal(digits, value.ToString("D" + digits).Length);
            seen.Add(value);
        }

        Assert.Equal(10_000, seen.Count);
    }

    /// <summary>
    /// The old implementation computed its substring offset as <c>Length - digits - 1</c>, which goes
    /// negative as the requested length approaches the rendering's own length: at 18 digits it threw
    /// ArgumentOutOfRangeException on about 5% of calls, so a programme configured with a long code
    /// would fail partway through generating a distribution.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(6)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(18)]
    public void Supported_code_lengths_never_throw(int digits)
    {
        for (var i = 0; i < 20_000; i++)
        {
            var value = RandomNumber.RandomLong(digits);

            Assert.InRange(value, 0, long.MaxValue);
            Assert.Equal(digits, value.ToString("D" + digits).Length);
        }
    }

    /// <summary>
    /// Beyond 18 digits a code no longer fits in the long the callers use, and the old code answered
    /// that by throwing an exception about a substring index. Rejecting the argument explicitly is
    /// what turns a confusing failure into a configuration error an operator can act on.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(19)]
    [InlineData(64)]
    public void Unsupported_code_lengths_are_rejected(int digits)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => RandomNumber.RandomLong(digits));

        Assert.Equal("digits", exception.ParamName);
    }
}
