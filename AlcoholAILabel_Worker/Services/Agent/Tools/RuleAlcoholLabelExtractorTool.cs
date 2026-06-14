using AlcoholAILabel_Shared.Enums;
using AlcoholAILabel_Worker.Services.Agent.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AlcoholAILabel_Worker.Services.Agent.Tools;

public class RuleAlcoholLabelExtractorTool
{
    public AlcoholLabelAgentResult Extract(
        string rawText,
        AlcoholProductType productType,
        SourceProduct sourceProduct)
    {
        var result = new AlcoholLabelAgentResult
        {
            BrandName = ExtractBrandName(rawText),
            ClassTypeDesignation = ExtractClassTypeDesignation(rawText),
            AlcoholContent = ExtractAlcoholContent(rawText),
            NetContents = ExtractNetContents(rawText),
            BottlerProducerNameAddress = ExtractBottlerProducerAddress(rawText),
            CountryOfOrigin = ExtractCountryOfOrigin(rawText),
            GovernmentWarning = ExtractGovernmentWarning(rawText),
            UsedAi = false
        };

        result.Confidence = new AlcoholLabelConfidence
        {
            BrandName = result.BrandName is null ? 0.20 : 0.70,
            ClassTypeDesignation = result.ClassTypeDesignation is null ? 0.20 : 0.70,
            AlcoholContent = result.AlcoholContent is null ? 0.20 : 0.90,
            NetContents = result.NetContents is null ? 0.20 : 0.90,
            BottlerProducerNameAddress = result.BottlerProducerNameAddress is null ? 0.20 : 0.70,
            CountryOfOrigin = sourceProduct == SourceProduct.Imported
                ? result.CountryOfOrigin is null ? 0.20 : 0.85
                : 1.00,
            GovernmentWarning = result.GovernmentWarning is null ? 0.20 : 0.90
        };

        result.NeedsHumanReview = NeedsReview(result, sourceProduct);

        return result;
    }

    private static bool NeedsReview(
        AlcoholLabelAgentResult result,
        SourceProduct sourceProduct)
    {
        if (result.Confidence.BrandName < 0.80)
            return true;

        if (result.Confidence.AlcoholContent < 0.80)
            return true;

        if (result.Confidence.NetContents < 0.80)
            return true;

        if (result.Confidence.GovernmentWarning < 0.80)
            return true;

        if (sourceProduct == SourceProduct.Imported &&
            result.Confidence.CountryOfOrigin < 0.80)
            return true;

        return false;
    }

    private static string? ExtractBrandName(string rawText)
    {
        var lines = GetCleanLines(rawText);

        var ignoredWords = new[]
        {
            "government warning",
            "alc",
            "vol",
            "alcohol",
            "contains",
            "750",
            "ml",
            "product of",
            "imported",
            "bottled by",
            "produced by",
            "distilled by"
        };

        return lines.FirstOrDefault(line =>
            !ignoredWords.Any(word =>
                line.Contains(word, StringComparison.OrdinalIgnoreCase)));
    }

    private static string? ExtractClassTypeDesignation(string rawText)
    {
        var knownTypes = new[]
        {
            "tequila",
            "vodka",
            "rum",
            "gin",
            "whiskey",
            "whisky",
            "bourbon",
            "wine",
            "beer",
            "lager",
            "ale",
            "stout",
            "mezcal",
            "brandy",
            "cognac"
        };

        foreach (var type in knownTypes)
        {
            if (rawText.Contains(type, StringComparison.OrdinalIgnoreCase))
                return type.ToUpperInvariant();
        }

        return null;
    }

    private static string? ExtractAlcoholContent(string rawText)
    {
        var lines = GetCleanLines(rawText);

        return lines.FirstOrDefault(x =>
            x.Contains("ALC", StringComparison.OrdinalIgnoreCase) ||
            x.Contains("VOL", StringComparison.OrdinalIgnoreCase) ||
            x.Contains("%"));
    }

    private static string? ExtractNetContents(string rawText)
    {
        var lines = GetCleanLines(rawText);

        return lines.FirstOrDefault(x =>
            x.Contains("ML", StringComparison.OrdinalIgnoreCase) ||
            x.Contains("M1", StringComparison.OrdinalIgnoreCase) ||
            x.Contains("LITER", StringComparison.OrdinalIgnoreCase) ||
            x.Contains("LITRE", StringComparison.OrdinalIgnoreCase) ||
            x.Contains("OZ", StringComparison.OrdinalIgnoreCase));
    }

    private static string? ExtractBottlerProducerAddress(string rawText)
    {
        var lines = GetCleanLines(rawText);

        return lines.FirstOrDefault(x =>
            x.Contains("bottled by", StringComparison.OrdinalIgnoreCase) ||
            x.Contains("produced by", StringComparison.OrdinalIgnoreCase) ||
            x.Contains("distilled by", StringComparison.OrdinalIgnoreCase) ||
            x.Contains("imported by", StringComparison.OrdinalIgnoreCase));
    }

    private static string? ExtractCountryOfOrigin(string rawText)
    {
        var lines = GetCleanLines(rawText);

        var productOfLine = lines.FirstOrDefault(x =>
            x.Contains("product of", StringComparison.OrdinalIgnoreCase));

        if (productOfLine is not null)
        {
            return productOfLine
                .Replace("PRODUCT OF", "", StringComparison.OrdinalIgnoreCase)
                .Trim()
                .Trim('.', ',');
        }

        var countries = new[]
        {
            "Mexico",
            "France",
            "Italy",
            "Spain",
            "Canada",
            "Ireland",
            "Scotland",
            "Japan",
            "Germany",
            "Chile",
            "Argentina",
            "Peru"
        };

        return countries.FirstOrDefault(country =>
            rawText.Contains(country, StringComparison.OrdinalIgnoreCase));
    }

    private static string? ExtractGovernmentWarning(string rawText)
    {
        var warningIndex = rawText.IndexOf(
            "GOVERNMENT WARNING",
            StringComparison.OrdinalIgnoreCase);

        if (warningIndex < 0)
            return null;

        return rawText[warningIndex..].Trim();
    }

    private static List<string> GetCleanLines(string rawText)
    {
        return rawText
            .Split(
                new[] { "\r\n", "\n", "\r" },
                StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();
    }
}

//public class RuleAlcoholLabelExtractorTool
//{
//    public AlcoholLabelAgentResult Extract(
//        string rawText,
//        AlcoholProductType productType,
//        SourceProduct sourceProduct)
//    {
//        var result = new AlcoholLabelAgentResult
//        {
//            BrandName = ExtractBrandName(rawText),
//            ClassTypeDesignation = ExtractClassTypeDesignation(rawText),
//            AlcoholContent = ExtractAlcoholContent(rawText),
//            NetContents = ExtractNetContents(rawText),
//            BottlerProducerNameAddress = ExtractBottlerProducerAddress(rawText),
//            CountryOfOrigin = ExtractCountryOfOrigin(rawText),
//            GovernmentWarning = ExtractGovernmentWarning(rawText),
//            UsedAi = false
//        };

//        result.Confidence = new AlcoholLabelConfidence
//        {
//            BrandName = result.BrandName is null ? 0.20 : 0.70,
//            ClassTypeDesignation = result.ClassTypeDesignation is null ? 0.20 : 0.70,
//            AlcoholContent = result.AlcoholContent is null ? 0.20 : 0.90,
//            NetContents = result.NetContents is null ? 0.20 : 0.90,
//            BottlerProducerNameAddress = result.BottlerProducerNameAddress is null ? 0.20 : 0.70,
//            CountryOfOrigin = sourceProduct == SourceProduct.Imported
//                ? result.CountryOfOrigin is null ? 0.20 : 0.85
//                : 1.00,
//            GovernmentWarning = result.GovernmentWarning is null ? 0.20 : 0.90
//        };

//        result.NeedsHumanReview = NeedsReview(result, sourceProduct);

//        return result;
//    }

//    private static bool NeedsReview(
//        AlcoholLabelAgentResult result,
//        SourceProduct sourceProduct)
//    {
//        if (result.Confidence.BrandName < 0.80)
//            return true;

//        if (result.Confidence.AlcoholContent < 0.80)
//            return true;

//        if (result.Confidence.NetContents < 0.80)
//            return true;

//        if (result.Confidence.GovernmentWarning < 0.80)
//            return true;

//        if (sourceProduct == SourceProduct.Imported &&
//            result.Confidence.CountryOfOrigin < 0.80)
//            return true;

//        return false;
//    }

//    private static string? ExtractBrandName(string rawText)
//    {
//        var lines = GetCleanLines(rawText);

//        var ignoredWords = new[]
//        {
//            "government warning",
//            "alc",
//            "vol",
//            "alcohol",
//            "contains",
//            "750",
//            "ml",
//            "product of",
//            "imported",
//            "bottled by",
//            "produced by",
//            "distilled by"
//        };

//        return lines.FirstOrDefault(line =>
//            !ignoredWords.Any(word =>
//                line.Contains(word, StringComparison.OrdinalIgnoreCase)));
//    }

//    private static string? ExtractClassTypeDesignation(string rawText)
//    {
//        var knownTypes = new[]
//        {
//            "tequila",
//            "vodka",
//            "rum",
//            "gin",
//            "whiskey",
//            "whisky",
//            "bourbon",
//            "wine",
//            "beer",
//            "lager",
//            "ale",
//            "stout",
//            "mezcal",
//            "brandy",
//            "cognac"
//        };

//        foreach (var type in knownTypes)
//        {
//            if (rawText.Contains(type, StringComparison.OrdinalIgnoreCase))
//                return type.ToUpperInvariant();
//        }

//        return null;
//    }

//    private static string? ExtractAlcoholContent(string rawText)
//    {
//        var lines = GetCleanLines(rawText);

//        return lines.FirstOrDefault(x =>
//            x.Contains("ALC", StringComparison.OrdinalIgnoreCase) ||
//            x.Contains("VOL", StringComparison.OrdinalIgnoreCase) ||
//            x.Contains("%"));
//    }

//    private static string? ExtractNetContents(string rawText)
//    {
//        var lines = GetCleanLines(rawText);

//        return lines.FirstOrDefault(x =>
//            x.Contains("ML", StringComparison.OrdinalIgnoreCase) ||
//            x.Contains("LITER", StringComparison.OrdinalIgnoreCase) ||
//            x.Contains("LITRE", StringComparison.OrdinalIgnoreCase) ||
//            x.Contains("OZ", StringComparison.OrdinalIgnoreCase));
//    }

//    private static string? ExtractBottlerProducerAddress(string rawText)
//    {
//        var lines = GetCleanLines(rawText);

//        return lines.FirstOrDefault(x =>
//            x.Contains("bottled by", StringComparison.OrdinalIgnoreCase) ||
//            x.Contains("produced by", StringComparison.OrdinalIgnoreCase) ||
//            x.Contains("distilled by", StringComparison.OrdinalIgnoreCase) ||
//            x.Contains("imported by", StringComparison.OrdinalIgnoreCase));
//    }

//    private static string? ExtractCountryOfOrigin(string rawText)
//    {
//        var lines = GetCleanLines(rawText);

//        var productOfLine = lines.FirstOrDefault(x =>
//            x.Contains("product of", StringComparison.OrdinalIgnoreCase));

//        if (productOfLine is not null)
//        {
//            return productOfLine
//                .Replace("PRODUCT OF", "", StringComparison.OrdinalIgnoreCase)
//                .Trim()
//                .Trim('.', ',');
//        }

//        var countries = new[]
//        {
//            "Mexico",
//            "France",
//            "Italy",
//            "Spain",
//            "Canada",
//            "Ireland",
//            "Scotland",
//            "Japan",
//            "Germany",
//            "Chile",
//            "Argentina",
//            "Peru"
//        };

//        return countries.FirstOrDefault(country =>
//            rawText.Contains(country, StringComparison.OrdinalIgnoreCase));
//    }

//    private static string? ExtractGovernmentWarning(string rawText)
//    {
//        var warningIndex = rawText.IndexOf(
//            "GOVERNMENT WARNING",
//            StringComparison.OrdinalIgnoreCase);

//        if (warningIndex < 0)
//            return null;

//        return rawText[warningIndex..].Trim();
//    }

//    private static List<string> GetCleanLines(string rawText)
//    {
//        return rawText
//            .Split(
//                new[] { "\r\n", "\n", "\r" },
//                StringSplitOptions.RemoveEmptyEntries)
//            .Select(x => x.Trim())
//            .Where(x => !string.IsNullOrWhiteSpace(x))
//            .ToList();
//    }
//}