using Fakturio.Api.Common;
using Fakturio.Api.Dtos;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Services.Common;

namespace Fakturio.Api.Services.Calendar;

/// <summary>
/// SK/CZ public holidays + weekends → next / add business days (local, no remote API).
/// </summary>
public sealed class BusinessDayService : IBusinessDayService
{
    public ServiceResult<BusinessDayResponse> AddBusinessDays(
        DateOnly from,
        int addDays,
        string? country = "SK")
    {
        var cc = string.IsNullOrWhiteSpace(country)
            ? "SK"
            : country.Trim().ToUpperInvariant();

        if (cc is not ("SK" or "CZ"))
        {
            return ServiceResult<BusinessDayResponse>.BadRequest(
                "Business-day calendar supports SK and CZ only.",
                ErrorCodes.UnsupportedCountry);
        }

        if (addDays is < 0 or > 365)
        {
            return ServiceResult<BusinessDayResponse>.BadRequest(
                "addDays must be between 0 and 365.",
                ErrorCodes.BadRequest);
        }

        var cursor = from;
        var skipped = new List<string>();
        var remaining = addDays;
        var adjusted = false;

        // Always land on a business day (even when addDays == 0).
        while (!IsBusinessDay(cursor, cc, out var reason))
        {
            if (reason is not null)
                skipped.Add($"{cursor:yyyy-MM-dd}:{reason}");
            cursor = cursor.AddDays(1);
            adjusted = true;
        }

        while (remaining > 0)
        {
            cursor = cursor.AddDays(1);
            if (IsBusinessDay(cursor, cc, out var reason))
            {
                remaining--;
            }
            else
            {
                if (reason is not null)
                    skipped.Add($"{cursor:yyyy-MM-dd}:{reason}");
                adjusted = true;
            }
        }

        return ServiceResult<BusinessDayResponse>.Ok(new BusinessDayResponse(
            Country: cc,
            From: from,
            AddDays: addDays,
            Result: cursor,
            Adjusted: adjusted || cursor != from.AddDays(addDays),
            HolidaysSkipped: skipped.Distinct().Take(20).ToList(),
            Source: "local-calendar"));
    }

    private static bool IsBusinessDay(DateOnly date, string country, out string? skipReason)
    {
        if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            skipReason = "weekend";
            return false;
        }

        if (IsHoliday(date, country, out var name))
        {
            skipReason = name;
            return false;
        }

        skipReason = null;
        return true;
    }

    private static bool IsHoliday(DateOnly date, string country, out string name)
    {
        foreach (var (month, day, label) in FixedHolidays(country))
        {
            if (date.Month == month && date.Day == day)
            {
                name = label;
                return true;
            }
        }

        var easter = EasterSunday(date.Year);
        foreach (var (offset, label) in MovableHolidays(country))
        {
            if (date == easter.AddDays(offset))
            {
                name = label;
                return true;
            }
        }

        name = "";
        return false;
    }

    private static IEnumerable<(int Month, int Day, string Name)> FixedHolidays(string country)
    {
        // Shared / overlapping
        yield return (1, 1, "new-year");
        yield return (5, 1, "labour-day");
        yield return (5, 8, "liberation-day");
        yield return (12, 24, "christmas-eve");
        yield return (12, 25, "christmas");
        yield return (12, 26, "stephen");

        if (country == "SK")
        {
            yield return (1, 6, "epiphany");
            yield return (7, 5, "cyril-methodius");
            yield return (8, 29, "slovak-national-uprising");
            yield return (9, 1, "constitution-day");
            yield return (9, 15, "our-lady-of-sorrows");
            yield return (11, 1, "all-saints");
            yield return (11, 17, "struggle-for-freedom");
        }
        else // CZ
        {
            yield return (7, 5, "cyril-methodius");
            yield return (7, 6, "jan-hus");
            yield return (9, 28, "czech-statehood");
            yield return (10, 28, "independent-czechoslovak-state");
            yield return (11, 17, "struggle-for-freedom");
        }
    }

    private static IEnumerable<(int Offset, string Name)> MovableHolidays(string country)
    {
        // Good Friday is a public holiday in SK (and banking holiday useful for due dates in CZ too).
        yield return (-2, "good-friday");
        yield return (1, "easter-monday");
        _ = country;
    }

    /// <summary>Anonymous Gregorian algorithm.</summary>
    private static DateOnly EasterSunday(int year)
    {
        var a = year % 19;
        var b = year / 100;
        var c = year % 100;
        var d = b / 4;
        var e = b % 4;
        var f = (b + 8) / 25;
        var g = (b - f + 1) / 3;
        var h = (19 * a + b - d - g + 15) % 30;
        var i = c / 4;
        var k = c % 4;
        var l = (32 + 2 * e + 2 * i - h - k) % 7;
        var m = (a + 11 * h + 22 * l) / 451;
        var month = (h + l - 7 * m + 114) / 31;
        var day = (h + l - 7 * m + 114) % 31 + 1;
        return new DateOnly(year, month, day);
    }
}
