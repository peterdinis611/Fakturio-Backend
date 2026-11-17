using System.Globalization;
using System.Net;
using System.Text;
using Fakturio.Api.Dtos;
using Fakturio.Api.Models;

namespace Fakturio.Api.Services.Email;

internal static class EmailTemplates
{
    public static (string Html, string Text) Invoice(
        string companyName,
        Invoice invoice,
        string? customMessage,
        string? publicUrl)
    {
        var total = invoice.Total.ToString("0.00", CultureInfo.InvariantCulture);
        var due = invoice.DueDate.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

        var sb = new StringBuilder();
        sb.Append("<div style=\"font-family:Georgia,serif;color:#1a1a1a;line-height:1.5\">");
        sb.Append($"<p>Dobrý deň{Greeting(invoice.CustomerName)},</p>");
        if (!string.IsNullOrWhiteSpace(customMessage))
            sb.Append($"<p>{WebUtility.HtmlEncode(customMessage.Trim())}</p>");
        else
            sb.Append($"<p>posielame doklad <strong>{WebUtility.HtmlEncode(invoice.Number)}</strong> od {WebUtility.HtmlEncode(companyName)}.</p>");

        sb.Append("<table style=\"border-collapse:collapse;margin:1.25rem 0;font-size:14px\">");
        Row(sb, "Doklad", invoice.Number);
        Row(sb, "Suma", $"{total} {invoice.Currency}");
        Row(sb, "Splatnosť", due);
        if (!string.IsNullOrWhiteSpace(invoice.VariableSymbol))
            Row(sb, "VS", invoice.VariableSymbol!);
        if (!string.IsNullOrWhiteSpace(invoice.BankIban))
            Row(sb, "IBAN", invoice.BankIban!);
        sb.Append("</table>");

        if (!string.IsNullOrWhiteSpace(publicUrl))
        {
            sb.Append(
                $"<p><a href=\"{WebUtility.HtmlEncode(publicUrl)}\" style=\"color:#0b5fff\">Otvoriť doklad online</a></p>");
        }

        sb.Append("<p style=\"color:#666;font-size:13px\">PDF je v prílohe (ak bolo priložené).</p>");
        sb.Append($"<p style=\"margin-top:2rem\">{WebUtility.HtmlEncode(companyName)}</p>");
        sb.Append("</div>");

        var text = new StringBuilder();
        text.AppendLine($"Dobrý deň{GreetingPlain(invoice.CustomerName)},");
        text.AppendLine();
        if (!string.IsNullOrWhiteSpace(customMessage))
            text.AppendLine(customMessage.Trim());
        else
            text.AppendLine($"posielame doklad {invoice.Number} od {companyName}.");
        text.AppendLine();
        text.AppendLine($"Doklad: {invoice.Number}");
        text.AppendLine($"Suma: {total} {invoice.Currency}");
        text.AppendLine($"Splatnosť: {due}");
        if (!string.IsNullOrWhiteSpace(publicUrl))
        {
            text.AppendLine();
            text.AppendLine($"Online: {publicUrl}");
        }
        text.AppendLine();
        text.AppendLine(companyName);

        return (sb.ToString(), text.ToString());
    }

    public static (string Html, string Text, string Subject) Reminder(
        string companyName,
        InvoiceReminderItemResponse reminder)
    {
        var total = reminder.Total.ToString("0.00", CultureInfo.InvariantCulture);
        var due = reminder.DueDate.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
        var subject = reminder.IsOverdue
            ? $"Upomienka · {reminder.Number} po splatnosti"
            : reminder.DaysUntilDue == 0
                ? $"Pripomienka · {reminder.Number} splatné dnes"
                : $"Pripomienka · {reminder.Number} splatnosť o {reminder.DaysUntilDue} d.";

        var lead = reminder.IsOverdue
            ? $"doklad <strong>{WebUtility.HtmlEncode(reminder.Number)}</strong> je po splatnosti ({due})."
            : reminder.DaysUntilDue == 0
                ? $"doklad <strong>{WebUtility.HtmlEncode(reminder.Number)}</strong> je splatný dnes."
                : $"doklad <strong>{WebUtility.HtmlEncode(reminder.Number)}</strong> je splatný o {reminder.DaysUntilDue} dní ({due}).";

        var html = $"""
            <div style="font-family:Georgia,serif;color:#1a1a1a;line-height:1.5">
              <p>Dobrý deň{Greeting(reminder.CustomerName)},</p>
              <p>pripomíname, že {lead}</p>
              <p>Suma: <strong>{total} {WebUtility.HtmlEncode(reminder.Currency)}</strong></p>
              <p style="margin-top:2rem">{WebUtility.HtmlEncode(companyName)}</p>
            </div>
            """;

        var text = $"""
            Dobrý deň{GreetingPlain(reminder.CustomerName)},

            pripomíname doklad {reminder.Number} — splatnosť {due}, suma {total} {reminder.Currency}.

            {companyName}
            """;

        return (html, text, subject);
    }

    public static (string Html, string Text) Test(string appName)
    {
        var html =
            $"<div style=\"font-family:Georgia,serif\"><p>Toto je skúšobný e-mail z <strong>{WebUtility.HtmlEncode(appName)}</strong>.</p><p>Ak ho vidíš, odosielanie funguje.</p></div>";
        var text = $"Toto je skúšobný e-mail z {appName}. Ak ho vidíš, odosielanie funguje.";
        return (html, text);
    }

    private static void Row(StringBuilder sb, string label, string value)
    {
        sb.Append("<tr>");
        sb.Append(
            $"<td style=\"padding:4px 16px 4px 0;color:#666\">{WebUtility.HtmlEncode(label)}</td>");
        sb.Append(
            $"<td style=\"padding:4px 0;font-family:ui-monospace,monospace\">{WebUtility.HtmlEncode(value)}</td>");
        sb.Append("</tr>");
    }

    private static string Greeting(string? name) =>
        string.IsNullOrWhiteSpace(name) ? "" : $", {WebUtility.HtmlEncode(name.Trim())}";

    private static string GreetingPlain(string? name) =>
        string.IsNullOrWhiteSpace(name) ? "" : $", {name.Trim()}";
}
