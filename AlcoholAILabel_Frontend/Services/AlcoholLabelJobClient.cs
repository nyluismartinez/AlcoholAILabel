using AlcoholAILabel_Shared.Dtos;
using AlcoholAILabel_Shared.Enums;
using Microsoft.AspNetCore.Components.Forms;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace AlcoholAILabel_Frontend.Services;

public class AlcoholLabelJobClient
{
    private readonly HttpClient _httpClient;

    public AlcoholLabelJobClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<CreateAlcoholLabelJobResponse?> UploadAsync(
        string productName,
        AlcoholProductType productType,
        SourceProduct sourceProduct,
        IBrowserFile file,
        CancellationToken cancellationToken = default)
    {
        using var content = new MultipartFormDataContent();

        content.Add(new StringContent(productName), "productName");
        content.Add(new StringContent(((int)productType).ToString()), "productType");
        content.Add(new StringContent(((int)sourceProduct).ToString()), "sourceProduct");

        var stream = file.OpenReadStream(maxAllowedSize: 50_000_000, cancellationToken);
        var fileContent = new StreamContent(stream);

        fileContent.Headers.ContentType =
            new MediaTypeHeaderValue(file.ContentType);

        content.Add(fileContent, "labelImage", file.Name);

        var response = await _httpClient.PostAsync(
            "api/AlcoholLabelJobs/upload",
            content,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new Exception(error);
        }

        return await response.Content.ReadFromJsonAsync<CreateAlcoholLabelJobResponse>(
            cancellationToken);
    }

    public async Task<AlcoholLabelJobDetailsDto?> GetByIdAsync(
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        return await _httpClient.GetFromJsonAsync<AlcoholLabelJobDetailsDto>(
            $"api/AlcoholLabelJobs/{jobId}",
            cancellationToken);
    }

    public async Task<AlcoholLabelJobDetailsDto?> UpdateReviewAsync(
        Guid jobId,
        UpdateAlcoholLabelReviewRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync(
            $"api/AlcoholLabelJobs/{jobId}/review",
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new Exception(error);
        }

        return await response.Content.ReadFromJsonAsync<AlcoholLabelJobDetailsDto>(
            cancellationToken);
    }


    public async Task<List<RecentAlcoholLabelJobDto>> GetUploadedJobsAsync(
    CancellationToken cancellationToken = default)
    {
        return await _httpClient.GetFromJsonAsync<List<RecentAlcoholLabelJobDto>>(
            "api/AlcoholLabelJobs/uploaded",
            cancellationToken) ?? new();
    }

    public async Task SendForProcessingAsync(
        List<Guid> jobIds,
        CancellationToken cancellationToken = default)
    {
        var request = new SendAlcoholLabelJobsForProcessingRequest
        {
            JobIds = jobIds
        };

        var response = await _httpClient.PostAsJsonAsync(
            "api/AlcoholLabelJobs/send-for-processing",
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new Exception(error);
        }
    }

}