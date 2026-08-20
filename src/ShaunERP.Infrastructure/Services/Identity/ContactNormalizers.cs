namespace ShaunERP.Core.Identity;

public sealed class PhoneNormalizationResult
{
    public string? Original { get; init; }
    public string? NormalizedPhone { get; init; }
    public string? E164Phone { get; init; }
    public bool IsValidIndianMobile { get; init; }
    public bool IsEmpty => string.IsNullOrWhiteSpace(Original);
}

public interface IPhoneNormalizer
{
    PhoneNormalizationResult Normalize(string? raw, string defaultCountryCode = "91");
}

public sealed class PhoneNormalizer : IPhoneNormalizer
{
    public PhoneNormalizationResult Normalize(string? raw, string defaultCountryCode = "91")
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new PhoneNormalizationResult { Original = raw };
        }

        var original = raw.Trim();
        var digits = new string(original.Where(char.IsDigit).ToArray());
        if (digits.Length == 0)
        {
            return new PhoneNormalizationResult { Original = original };
        }

        // Strip leading trunk 0 for local numbers: 09876543210 → 9876543210
        if (digits.Length == 11 && digits.StartsWith('0'))
        {
            digits = digits[1..];
        }

        // 919876543210 → national 9876543210
        if (digits.Length == 12 && digits.StartsWith(defaultCountryCode))
        {
            digits = digits[defaultCountryCode.Length..];
        }

        // 0091... rare
        if (digits.Length > 12 && digits.StartsWith("00" + defaultCountryCode))
        {
            digits = digits[("00" + defaultCountryCode).Length..];
        }

        var isIndianMobile = digits.Length == 10 && digits[0] is >= '6' and <= '9';
        string? e164 = null;
        string? normalized = digits;

        if (isIndianMobile)
        {
            e164 = $"+{defaultCountryCode}{digits}";
            normalized = digits;
        }
        else if (digits.Length >= 8)
        {
            // Keep digits-only as normalized fallback for non-IN / landlines
            normalized = digits;
            if (!original.StartsWith('+') && digits.Length <= 15)
            {
                e164 = null;
            }
            else if (original.StartsWith('+'))
            {
                e164 = "+" + digits;
            }
        }

        return new PhoneNormalizationResult
        {
            Original = original,
            NormalizedPhone = normalized,
            E164Phone = e164,
            IsValidIndianMobile = isIndianMobile
        };
    }
}

public interface IEmailNormalizer
{
    string? Normalize(string? raw);
}

public sealed class EmailNormalizer : IEmailNormalizer
{
    public string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return raw.Trim().ToLowerInvariant();
    }
}
