using Ganss.Xss;

namespace Fakturio.Api.Services.Notes;

/// <summary>Sanitize TipTap HTML note bodies before persistence.</summary>
public static class NoteHtmlSanitizer
{
    private static readonly HtmlSanitizer Sanitizer = Create();

    public static string Sanitize(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        return Sanitizer.Sanitize(html.Trim());
    }

    private static HtmlSanitizer Create()
    {
        var sanitizer = new HtmlSanitizer();

        sanitizer.AllowedTags.Clear();
        foreach (var tag in new[]
        {
            "p", "br", "strong", "b", "em", "i", "u", "s", "strike",
            "code", "pre", "blockquote", "ul", "ol", "li",
            "h1", "h2", "h3", "a", "img",
            "table", "thead", "tbody", "tr", "th", "td", "hr",
            "span", "div", "aside", "sub", "sup", "mark",
        })
            sanitizer.AllowedTags.Add(tag);

        sanitizer.AllowedAttributes.Clear();
        foreach (var attr in new[]
        {
            "href", "target", "rel", "src", "alt", "title", "class", "style",
            "colspan", "rowspan", "data-type", "data-variant", "data-checked", "data-color",
        })
            sanitizer.AllowedAttributes.Add(attr);

        sanitizer.AllowedSchemes.Clear();
        sanitizer.AllowedSchemes.Add("http");
        sanitizer.AllowedSchemes.Add("https");
        sanitizer.AllowedSchemes.Add("mailto");
        sanitizer.AllowedSchemes.Add("tel");

        sanitizer.AllowDataAttributes = false;

        return sanitizer;
    }
}
