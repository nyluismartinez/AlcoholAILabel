using AlcoholAILabel_Shared.Dtos;

namespace AlcoholAILabel_Frontend.Services;

public class AiTestClient
{
    private readonly HttpClient _httpClient;

    public AiTestClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<AiTestResponse?> AskAsync(
        string prompt,
        CancellationToken cancellationToken = default)
    {
        var request = new AiTestRequest
        {
            Prompt = prompt
        };

        var response = await _httpClient.PostAsJsonAsync(
            "api/AiTest",
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error =
                await response.Content.ReadAsStringAsync(cancellationToken);

            throw new Exception(error);
        }

        return await response.Content.ReadFromJsonAsync<AiTestResponse>(
            cancellationToken);
    }
}
