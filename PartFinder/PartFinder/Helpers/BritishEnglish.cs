using System.Text.RegularExpressions;

namespace PartFinder.Helpers;

/// <summary>Normalises common US spellings in user-visible text (e.g. API error messages).</summary>
internal static class BritishEnglish
{
    // "Catalogue" itself contains "Catalog", so a plain string replace is not idempotent
    // ("catalogs" -> "catalogues" -> "catalogueues"). Only rewrite "catalog" when it is not
    // already followed by "ue".
    private static readonly Regex CatalogRegex = new("([Cc])atalog(?!ue)", RegexOptions.Compiled);

    public static string ForDisplay(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        text = CatalogRegex.Replace(text, "$1atalogue");

        return text
            .Replace("Organizations", "Organisations")
            .Replace("organizations", "organisations")
            .Replace("Organization", "Organisation")
            .Replace("organization", "organisation")
            .Replace("Licenses", "Licences")
            .Replace("licenses", "licences")
            .Replace("License", "Licence")
            .Replace("license", "licence")
            .Replace("Favorites", "Favourites")
            .Replace("favorites", "favourites")
            .Replace("Favorite", "Favourite")
            .Replace("favorite", "favourite")
            .Replace("Optimization", "Optimisation")
            .Replace("optimization", "optimisation")
            .Replace("Synchronize", "Synchronise")
            .Replace("synchronize", "synchronise")
            .Replace("Analyze", "Analyse")
            .Replace("analyze", "analyse")
            .Replace("Authorized", "Authorised")
            .Replace("authorized", "authorised")
            .Replace("Unauthorized", "Unauthorised")
            .Replace("unauthorized", "unauthorised")
            .Replace("Defense", "Defence")
            .Replace("defense", "defence")
            .Replace("Finalize", "Finalise")
            .Replace("finalize", "finalise")
            .Replace("Customized", "Customised")
            .Replace("customized", "customised")
            .Replace("Customize", "Customise")
            .Replace("customize", "customise")
            .Replace("Personalized", "Personalised")
            .Replace("personalized", "personalised")
            .Replace("Recognized", "Recognised")
            .Replace("recognized", "recognised")
            .Replace("Recognize", "Recognise")
            .Replace("recognize", "recognise")
            .Replace("Modeled", "Modelled")
            .Replace("modeled", "modelled")
            .Replace("Modeling", "Modelling")
            .Replace("modeling", "modelling")
            .Replace("Canceled", "Cancelled")
            .Replace("canceled", "cancelled");
    }
}
