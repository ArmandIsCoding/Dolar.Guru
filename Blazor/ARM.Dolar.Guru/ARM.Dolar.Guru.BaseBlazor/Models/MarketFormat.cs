using System.Globalization;

namespace ARM.Dolar.Guru.BaseBlazor.Models;

public static class MarketFormat
{
    public static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("es-AR");
    private static readonly TimeZoneInfo Argentina = TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires");
    public static string Money(decimal value) => "$ " + value.ToString("N2", Culture);
    public static string Date(DateTime value) => value == DateTime.MinValue ? "Fecha no disponible" :
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(value, DateTimeKind.Utc), Argentina).ToString("dd/MM · HH:mm", Culture) + " ARG";
    public static string SafeUrl(string? url) => Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && uri.Scheme is "https" or "http" ? uri.AbsoluteUri : "/news";
}
