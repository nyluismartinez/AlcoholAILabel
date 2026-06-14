using AlcoholAILabel_API.Services.LocalAi;
using AlcoholAILabel_Shared.Dtos;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AlcoholAILabel_API.Controllers;


[ApiController]
[Route("api/[controller]")]
public class AiTestController : ControllerBase
{
    private readonly ApiLocalLlmClient _localLlmClient;

    public AiTestController(ApiLocalLlmClient localLlmClient)
    {
        _localLlmClient = localLlmClient;
    }

    [HttpPost]
    public async Task<ActionResult<AiTestResponse>> TestAsync(
        [FromBody] AiTestRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            return BadRequest("Prompt is required.");
        }

        var aiPrompt = BuildAlcoholLabelPrompt(request.Prompt);

        var responseJson =
            await _localLlmClient.AskJsonAsync(
                aiPrompt,
                cancellationToken);

        return Ok(new AiTestResponse
        {
            ResponseJson = responseJson
        });
    }

    private static string BuildAlcoholLabelPrompt(string ocrText)
    {
        return $$"""
        Extract alcohol beverage label fields from this OCR text.

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
          }
        }

        Rules:
        - Use null when a value is missing.
        - Fix obvious OCR mistakes.
        - D0N JUL10 means Don Julio.
        - ALC/V0L means ALC/VOL.
        - M1 means ML when used for bottle size.
        - Confidence must be between 0 and 1.
        - Do not invent missing fields.
        - Return JSON only.

        OCR Text:

        {{ocrText}}
        """;
    }
}