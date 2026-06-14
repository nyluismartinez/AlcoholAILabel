using AlcoholAILabel_Shared.Enums;
using AlcoholAILabel_Worker.Services.Agent.LocalAi;
using AlcoholAILabel_Worker.Services.Agent.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace AlcoholAILabel_Worker.Services.Agent.Tools;

public class AiAlcoholLabelExtractorTool
{
    private readonly LocalLlmClient _localLlmClient;

    public AiAlcoholLabelExtractorTool(LocalLlmClient localLlmClient)
    {
        _localLlmClient = localLlmClient;
    }

    public async Task<AlcoholLabelAgentResult> ExtractAsync(
        string rawText,
        AlcoholProductType productType,
        SourceProduct sourceProduct,
        CancellationToken cancellationToken = default)
    {
        var prompt = BuildPrompt(rawText, productType, sourceProduct);

        var json =
            await _localLlmClient.AskJsonAsync(
                prompt,
                cancellationToken);

        var result =
            JsonSerializer.Deserialize<AlcoholLabelAgentResult>(
                json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

        if (result is null)
            throw new Exception("AI result could not be parsed.");

        result.UsedAi = true;

        result.NeedsHumanReview =
            NeedsReview(result, sourceProduct);

        return result;
    }

    private static string BuildPrompt(
        string rawText,
        AlcoholProductType productType,
        SourceProduct sourceProduct)
    {
        return $$"""
        Extract alcohol beverage label metadata from the OCR text.

        Product Type:
        {{productType}}

        Source Product:
        {{sourceProduct}}

        Return only JSON using this exact structure:

        {
          "brandName": null,
          "classTypeDesignation": null,
          "alcoholContent": null,
          "netContents": null,
          "bottlerProducerNameAddress": null,
          "countryOfOrigin": null,
          "governmentWarning": null,
          "confidence": {
            "brandName": 0.0,
            "classTypeDesignation": 0.0,
            "alcoholContent": 0.0,
            "netContents": 0.0,
            "bottlerProducerNameAddress": 0.0,
            "countryOfOrigin": 0.0,
            "governmentWarning": 0.0
          },
          "usedAi": true,
          "needsHumanReview": true
        }

        Rules:
        - Use null if a field is missing.
        - Fix obvious OCR mistakes.
        - D0N JUL10 means Don Julio.
        - ALC/V0L means ALC/VOL.
        - M1 means ML when used for bottle size.
        - MEX1CO means Mexico.
        - WARNlNG means WARNING.
        - Confidence must be between 0 and 1.
        - Do not invent missing fields.
        - For imported products, countryOfOrigin is required.
        - Government warning should start with GOVERNMENT WARNING when present.
        - Return JSON only.

        OCR Text:

        {{rawText}}
        """;
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
}
