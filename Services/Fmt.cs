using System.Globalization;
using Microsoft.AspNetCore.Html;
using InvoiceManager.Models;

namespace InvoiceManager.Services;

/// <summary>Formato y etiquetas en español para las vistas.</summary>
public static class Fmt
{
    public static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-ES");

    public static string Eur(decimal v) => v.ToString("#,##0.00 €", Es);
    public static string EurShort(decimal v) => v.ToString("#,##0 €", Es);
    public static string Date(DateTime d) => d.ToString("dd/MM/yyyy", Es);
    public static string DateLong(DateTime d) => d.ToString("d 'de' MMMM 'de' yyyy", Es);
    public static string DateShort(DateTime d) => d.ToString("d MMM", Es).Replace(".", "");
    public static string Num(decimal v) => v.ToString("0.##", Es);
    public static string Pct(decimal v) => v.ToString("0.##", Es) + " %";

    public static string StatusLabel(InvoiceStatus s) => s switch
    {
        InvoiceStatus.Draft     => "Borrador",
        InvoiceStatus.Pending   => "Pendiente",
        InvoiceStatus.Paid      => "Cobrada",
        InvoiceStatus.Overdue   => "Vencida",
        InvoiceStatus.Cancelled => "Anulada",
        _ => s.ToString()
    };

    public static string StatusIcon(InvoiceStatus s) => s switch
    {
        InvoiceStatus.Draft     => "bi-pencil-square",
        InvoiceStatus.Pending   => "bi-clock",
        InvoiceStatus.Paid      => "bi-check-circle-fill",
        InvoiceStatus.Overdue   => "bi-exclamation-circle-fill",
        InvoiceStatus.Cancelled => "bi-x-circle",
        _ => "bi-circle"
    };

    public static string StatusCss(InvoiceStatus s) => "pill-" + s.ToString().ToLowerInvariant();

    /// <summary>Pastilla de estado (los textos son fijos, no proceden del usuario).</summary>
    public static IHtmlContent Pill(InvoiceStatus s, bool large = false) =>
        new HtmlString($"<span class=\"pill {StatusCss(s)}{(large ? " pill-lg" : "")}\"><i class=\"bi {StatusIcon(s)}\"></i>{StatusLabel(s)}</span>");

    public static string AvatarTone(int id) => id % 5 == 0 ? "" : $"tone-{id % 5}";

    /// <summary>"vence hoy", "vence en 5 días", "vencida hace 12 días".</summary>
    public static string DueText(Invoice i)
    {
        var days = (i.DueDate.Date - DateTime.Today).Days;
        return days switch
        {
            0 => "vence hoy",
            1 => "vence mañana",
            > 1 => $"vence en {days} días",
            -1 => "vencida ayer",
            _ => $"vencida hace {-days} días"
        };
    }
}
