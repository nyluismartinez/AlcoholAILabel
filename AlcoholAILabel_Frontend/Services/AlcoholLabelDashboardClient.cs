using AlcoholAILabel_Shared.Dtos;

namespace AlcoholAILabel_Frontend.Services;


public class AlcoholLabelDashboardClient
{
    private readonly HttpClient _httpClient;

    public AlcoholLabelDashboardClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<AlcoholLabelDashboardDto?> GetDashboardAsync(
        CancellationToken cancellationToken = default)
    {
        return await _httpClient.GetFromJsonAsync<AlcoholLabelDashboardDto>(
            "api/AlcoholLabelDashboard",
            cancellationToken);
    }
}
