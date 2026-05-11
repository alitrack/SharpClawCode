namespace SharpClaw.Code.Runtime.Workflow;

internal static class ScheduleCronExpression
{
    public static DateTimeOffset GetNextOccurrence(string expression, DateTimeOffset from)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expression);

        var normalized = expression.Trim();
        if (string.Equals(normalized, "@hourly", StringComparison.OrdinalIgnoreCase))
        {
            var candidate = new DateTimeOffset(from.Year, from.Month, from.Day, from.Hour, 0, 0, TimeSpan.Zero).AddHours(1);
            return candidate > from ? candidate : candidate.AddHours(1);
        }

        if (string.Equals(normalized, "@daily", StringComparison.OrdinalIgnoreCase))
        {
            var candidate = new DateTimeOffset(from.Year, from.Month, from.Day, 0, 0, 0, TimeSpan.Zero).AddDays(1);
            return candidate > from ? candidate : candidate.AddDays(1);
        }

        if (string.Equals(normalized, "@weekly", StringComparison.OrdinalIgnoreCase))
        {
            var start = new DateTimeOffset(from.Year, from.Month, from.Day, 0, 0, 0, TimeSpan.Zero);
            var daysUntilMonday = ((int)DayOfWeek.Monday - (int)start.DayOfWeek + 7) % 7;
            if (daysUntilMonday == 0)
            {
                daysUntilMonday = 7;
            }

            return start.AddDays(daysUntilMonday);
        }

        var parts = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 5)
        {
            throw new InvalidOperationException("Schedule cron expressions must use five fields or one of @hourly, @daily, or @weekly.");
        }

        var minute = ParseField(parts[0], 0, 59, "minute");
        var hour = ParseField(parts[1], 0, 23, "hour");
        var day = parts[2];
        var month = parts[3];
        var dayOfWeek = parts[4];

        if (!string.Equals(day, "*", StringComparison.Ordinal)
            || !string.Equals(month, "*", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Only '*' is currently supported for day-of-month and month in scheduled prompt cron expressions.");
        }

        var cursor = from.ToUniversalTime().AddMinutes(1);
        cursor = new DateTimeOffset(cursor.Year, cursor.Month, cursor.Day, cursor.Hour, cursor.Minute, 0, TimeSpan.Zero);

        for (var i = 0; i < 525600; i++)
        {
            if (Matches(hour, cursor.Hour) && Matches(minute, cursor.Minute) && MatchesDayOfWeek(dayOfWeek, cursor.DayOfWeek))
            {
                return cursor;
            }

            cursor = cursor.AddMinutes(1);
        }

        throw new InvalidOperationException($"Unable to compute the next occurrence for cron expression '{expression}'.");
    }

    private static CronField ParseField(string token, int min, int max, string fieldName)
    {
        if (string.Equals(token, "*", StringComparison.Ordinal))
        {
            return new CronField(null, null, IsWildcard: true);
        }

        if (token.StartsWith("*/", StringComparison.Ordinal))
        {
            if (!int.TryParse(token[2..], out var step) || step <= 0)
            {
                throw new InvalidOperationException($"Invalid {fieldName} step expression '{token}'.");
            }

            return new CronField(null, step, IsWildcard: false);
        }

        if (!int.TryParse(token, out var value) || value < min || value > max)
        {
            throw new InvalidOperationException($"Invalid {fieldName} value '{token}'.");
        }

        return new CronField(value, null, IsWildcard: false);
    }

    private static bool Matches(CronField field, int value)
    {
        if (field.IsWildcard)
        {
            return true;
        }

        if (field.Step is { } step)
        {
            return value % step == 0;
        }

        return field.Value == value;
    }

    private static bool MatchesDayOfWeek(string token, DayOfWeek value)
    {
        if (string.Equals(token, "*", StringComparison.Ordinal))
        {
            return true;
        }

        if (int.TryParse(token, out var numeric))
        {
            numeric = numeric == 7 ? 0 : numeric;
            return (int)value == numeric;
        }

        return token.Trim().ToLowerInvariant() switch
        {
            "sun" => value == DayOfWeek.Sunday,
            "mon" => value == DayOfWeek.Monday,
            "tue" or "tues" => value == DayOfWeek.Tuesday,
            "wed" => value == DayOfWeek.Wednesday,
            "thu" or "thur" or "thurs" => value == DayOfWeek.Thursday,
            "fri" => value == DayOfWeek.Friday,
            "sat" => value == DayOfWeek.Saturday,
            _ => throw new InvalidOperationException($"Invalid day-of-week value '{token}'."),
        };
    }

    private readonly record struct CronField(int? Value, int? Step, bool IsWildcard);
}
