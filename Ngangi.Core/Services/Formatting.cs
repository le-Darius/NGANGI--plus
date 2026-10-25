using System.Globalization;

namespace Ngangi.Core.Services;

/// <summary>Formats français indépendants de la culture du téléphone.</summary>
public static class Formatting
{
    private static readonly string[] Months =
        { "janv.", "févr.", "mars", "avr.", "mai", "juin", "juil.", "août", "sept.", "oct.", "nov.", "déc." };
    private static readonly string[] Days =
        { "Dimanche", "Lundi", "Mardi", "Mercredi", "Jeudi", "Vendredi", "Samedi" };

    /// <summary>1240000 devient « 1 240 000 ».</summary>
    public static string Number(long value) =>
        value.ToString("N0", CultureInfo.InvariantCulture).Replace(',', ' ');

    /// <summary>340000 devient « 340 000 FCFA ».</summary>
    public static string Fcfa(long value) => Number(value) + " FCFA";

    /// <summary>« 11 oct. »</summary>
    public static string DayMonth(DateTime d) => $"{d.Day} {Months[d.Month - 1]}";

    /// <summary>« 05 oct. 2026 »</summary>
    public static string DayMonthYear(DateTime d) => $"{d.Day:00} {Months[d.Month - 1]} {d.Year}";

    /// <summary>« Dimanche 11 oct. 2026 »</summary>
    public static string Long(DateTime d) => $"{Days[(int)d.DayOfWeek]} {d.Day} {Months[d.Month - 1]} {d.Year}";

    public static string MonthShort(DateTime d) => Months[d.Month - 1];
}
