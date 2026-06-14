using AlcoholAILabel_Shared.Enums;
using AlcoholAILabel_Worker.Services.Agent.Models;
using AlcoholAILabel_Worker.Services.Agent.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AlcoholAILabel_Worker.Services.Agent;

public class AlcoholLabelExtractionAgent
{
    private readonly RuleAlcoholLabelExtractorTool _ruleTool;
    private readonly AiAlcoholLabelExtractorTool _aiTool;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AlcoholLabelExtractionAgent> _logger;

    public AlcoholLabelExtractionAgent(
        RuleAlcoholLabelExtractorTool ruleTool,
        AiAlcoholLabelExtractorTool aiTool,
        IConfiguration configuration,
        ILogger<AlcoholLabelExtractionAgent> logger)
    {
        _ruleTool = ruleTool;
        _aiTool = aiTool;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<AlcoholLabelAgentResult> ExtractAsync(
        string rawText,
        AlcoholProductType productType,
        SourceProduct sourceProduct,
        CancellationToken cancellationToken = default)
    {
        var rulesResult =
            _ruleTool.Extract(
                rawText,
                productType,
                sourceProduct);

        var aiEnabled =
            _configuration.GetValue<bool>(
                "LocalAiSettings:Enabled");

        var threshold =
            _configuration.GetValue<double>(
                "LocalAiSettings:ConfidenceThreshold",
                0.80);

        if (!aiEnabled)
        {
            _logger.LogInformation(
                "AI disabled. Using rules only.");

            return rulesResult;
        }

        if (!ShouldUseAi(
                rulesResult,
                threshold,
                sourceProduct))
        {
            _logger.LogInformation(
                "Rules confidence is good. AI not needed.");

            return rulesResult;
        }

        try
        {
            _logger.LogInformation(
                "Rules confidence is low. Calling local AI.");

            var aiResult =
                await _aiTool.ExtractAsync(
                    rawText,
                    productType,
                    sourceProduct,
                    cancellationToken);

            return aiResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "AI failed. Falling back to rules.");

            rulesResult.NeedsHumanReview = true;

            return rulesResult;
        }
    }

    private static bool ShouldUseAi(
        AlcoholLabelAgentResult result,
        double threshold,
        SourceProduct sourceProduct)
    {
        if (result.Confidence.BrandName < threshold)
            return true;

        if (result.Confidence.ClassTypeDesignation < threshold)
            return true;

        if (result.Confidence.AlcoholContent < threshold)
            return true;

        if (result.Confidence.NetContents < threshold)
            return true;

        if (result.Confidence.GovernmentWarning < threshold)
            return true;

        if (sourceProduct == SourceProduct.Imported &&
            result.Confidence.CountryOfOrigin < threshold)
            return true;

        return false;
    }
}
