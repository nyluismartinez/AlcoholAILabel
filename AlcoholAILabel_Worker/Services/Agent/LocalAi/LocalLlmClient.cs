using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace AlcoholAILabel_Worker.Services.Agent.LocalAi;

public class LocalLlmClient
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LocalLlmClient> _logger;

    public LocalLlmClient(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<LocalLlmClient> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<string> AskJsonAsync(
        string prompt,
        CancellationToken cancellationToken = default)
    {
        var modelName =
            _configuration["LocalAiSettings:ModelName"]
            ?? "llama3.2:latest";

        var request = new
        {
            model = modelName,
            stream = false,
            format = "json",
            messages = new[]
            {
                new
                {
                    role = "system",
                    content = "You are an alcohol beverage label extraction assistant. Return only valid JSON."
                },
                new
                {
                    role = "user",
                    content = prompt
                }
            }
        };

        var response = await _httpClient.PostAsJsonAsync(
            "/api/chat",
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error =
                await response.Content.ReadAsStringAsync(cancellationToken);

            throw new Exception($"Local AI failed: {error}");
        }

        using var stream =
            await response.Content.ReadAsStreamAsync(cancellationToken);

        using var document =
            await JsonDocument.ParseAsync(
                stream,
                cancellationToken: cancellationToken);

        var content = document.RootElement
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new Exception("Local AI returned empty content.");
        }

        _logger.LogInformation(
            "Local AI response received.");

        return content;
    }
}
