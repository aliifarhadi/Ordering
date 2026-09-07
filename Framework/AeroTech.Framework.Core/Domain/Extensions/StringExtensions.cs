using System.Globalization;

namespace AeroTech.Framework.Core.Domain.Extensions;

public static class StringExtensions
{
    // ================= long =================
    public static long? TryParseNullableLong(this string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return long.Parse(value);
    }

    // ================= int =================
    public static int? TryParseNullableInt(this string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : int.Parse(value);

    // ================= Enum =================
    public static TEnum? TryParseNullableEnum<TEnum>(this string? value) where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return Enum.Parse<TEnum>(value, true);
    }

    // ================= Boolean =================
    public static bool? TryParseNullableBool(this string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return Convert.ToBoolean(value);
    }

    // ================= DateOnly =================
    public static DateOnly? TryParseNullableDateOnly(this string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return DateOnly.TryParseExact(
            value,
            new[] { "yyyy-MM-dd", "yyyy-M-d", "yyyy-MM-d", "yyyy-M-dd" },
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var date)
            ? date
            : null;
    }

    // ================= DateTimeOffset =================
    public static DateTimeOffset? TryParseNullableDateTimeOffset(this string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return DateTimeOffset.Parse(value);
    }

    public static string ToCamelCase(this string text)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            text = text.TrimStart();
            return char.ToLower(text[0]) + text.Substring(1);
        }

        return text;
    }
}
