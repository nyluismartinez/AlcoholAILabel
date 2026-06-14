using AlcoholAILabel_Data.Database;
using AlcoholAILabel_Data.Entities;
using AlcoholAILabel_Shared.Enums;
using AlcoholAILabel_Worker.Services.Ocr;
using Microsoft.EntityFrameworkCore;

namespace AlcoholAILabel.Worker.Services;

public class AlcoholLabelJobProcessor
{
    private readonly AlcoholLabelDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AlcoholLabelJobProcessor> _logger;
    private readonly IOcrService _ocrService;

    public AlcoholLabelJobProcessor(
        AlcoholLabelDbContext dbContext,
        IConfiguration configuration,
        ILogger<AlcoholLabelJobProcessor> logger,
        IOcrService ocrService)
    {
        _dbContext = dbContext;
        _configuration = configuration;
        _logger = logger;
        _ocrService = ocrService;
    }

    public async Task ProcessNextJobAsync(CancellationToken cancellationToken)
    {
        var workerName =
            _configuration["WorkerSettings:WorkerName"]
            ?? "AlcoholLabelWorker";

        var workerStatus =
            await GetOrCreateWorkerStatusAsync(
                workerName,
                cancellationToken);

        var job = await _dbContext.AlcoholLabelJobs
            .Include(x => x.ExtractionResult)
            .Where(x => x.Status == AlcoholLabelJobStatus.Queued)
            .OrderBy(x => x.CreatedDate)
            .FirstOrDefaultAsync(cancellationToken);

        if (job is null)
        {
            await UpdateWorkerIdleAsync( workerStatus, cancellationToken);
            return;
        }

        try
        {
            await MarkWorkerBusyAsync( workerStatus, job.Id, cancellationToken);
            _logger.LogInformation( "Job {JobId} entered queue.", job.Id);


            // STAGE 1
            // QUEUED 
            job.PercentComplete = 0;

            await _dbContext.SaveChangesAsync(cancellationToken);

            // Keep QUEUED visible for 4 seconds
            await Task.Delay(TimeSpan.FromSeconds(4), cancellationToken);

            //
            // STAGE 2 
            // PROCESSING wait 4 seconds
            job.Status = AlcoholLabelJobStatus.Processing;
            job.StartedDate = DateTime.UtcNow;
            job.PercentComplete = 10;
            job.ErrorMessage = null;

            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Job {JobId} entered processing.",job.Id);


            await Task.Delay(TimeSpan.FromSeconds(4), cancellationToken);
            // STAGE 3
            // OCR + EXTRACTION Wait 4 seconds
            await ExtractRealOcrAndCreateResultAsync(job,cancellationToken);

            job.PercentComplete = 90;
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Job {JobId} OCR completed.",job.Id);



            await Task.Delay(TimeSpan.FromSeconds(4),cancellationToken);
            // STAGE 4
            // COMPLETED OR NEEDS REVIEW
            if (job.ExtractionResult?.NeedsHumanReview == true)
            {
                job.Status =AlcoholLabelJobStatus.NeedsHumanReview;
            }
            else
            {
                job.Status =AlcoholLabelJobStatus.Completed;
            }

            job.PercentComplete = 100;

            job.CompletedDate = DateTime.UtcNow;


            // UPDATE WORKER
            workerStatus.Status = "Idle";
            workerStatus.CurrentJobId = null;
            workerStatus.FilesProcessing = 0;
            workerStatus.TotalJobsProcessed += 1;
            workerStatus.LastHeartbeatUtc =DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Job {JobId} finished with status {Status}.",job.Id,job.Status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,"Failed processing job {JobId}",job.Id);

            job.Status =AlcoholLabelJobStatus.Failed;
            job.PercentComplete = 100;
            job.CompletedDate =DateTime.UtcNow;
            job.ErrorMessage =ex.Message;

            workerStatus.Status = "Idle";
            workerStatus.CurrentJobId = null;
            workerStatus.FilesProcessing = 0;
            workerStatus.LastHeartbeatUtc =DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task ExtractRealOcrAndCreateResultAsync(
        AlcoholLabelJob job,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(job.UploadedFilePath))
        {
            throw new FileNotFoundException(
                "Uploaded label image was not found.",
                job.UploadedFilePath);
        }

        job.PercentComplete = 25;
        await _dbContext.SaveChangesAsync(cancellationToken);

        var rawOcrText = await _ocrService.ExtractTextAsync(
            job.UploadedFilePath,
            cancellationToken);

        job.PercentComplete = 60;
        await _dbContext.SaveChangesAsync(cancellationToken);

        var result = new AlcoholLabelExtractionResult
        {
            Id = Guid.NewGuid(),
            JobId = job.Id,

            RawOcrText = rawOcrText,

            BrandName = ExtractBrandName(rawOcrText),
            ClassTypeDesignation = ExtractClassTypeDesignation(rawOcrText),
            AlcoholContent = ExtractAlcoholContent(rawOcrText),
            NetContents = ExtractNetContents(rawOcrText),
            BottlerProducerNameAddress = ExtractBottlerProducerAddress(rawOcrText),
            CountryOfOrigin = ExtractCountryOfOrigin(rawOcrText),
            GovernmentWarning = ExtractGovernmentWarning(rawOcrText),

            ConfidenceJson = CalculateSimpleConfidenceJson(rawOcrText),

            NeedsHumanReview = ShouldRequireHumanReview(rawOcrText),

            CreatedDate = DateTime.UtcNow
        };

        _dbContext.AlcoholLabelExtractionResults.Add(result);

        job.PercentComplete = 90;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string? ExtractBrandName(string rawText)
    {
        var lines = GetCleanLines(rawText);

        var ignoredWords = new[]
        {
            "government warning",
            "alc/vol",
            "alcohol",
            "contains",
            "750",
            "ml",
            "product of",
            "imported"
        };

        var possibleBrand = lines.FirstOrDefault(line =>
            !ignoredWords.Any(word =>
                line.Contains(word, StringComparison.OrdinalIgnoreCase)));

        return possibleBrand;
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

        var line = lines.FirstOrDefault(x =>
            x.Contains("ALC", StringComparison.OrdinalIgnoreCase) ||
            x.Contains("VOL", StringComparison.OrdinalIgnoreCase) ||
            x.Contains("%"));

        return line;
    }

    private static string? ExtractNetContents(string rawText)
    {
        var lines = GetCleanLines(rawText);

        var line = lines.FirstOrDefault(x =>
            x.Contains("ML", StringComparison.OrdinalIgnoreCase) ||
            x.Contains("LITER", StringComparison.OrdinalIgnoreCase) ||
            x.Contains("LITRE", StringComparison.OrdinalIgnoreCase) ||
            x.Contains("OZ", StringComparison.OrdinalIgnoreCase));

        return line;
    }

    private static string? ExtractBottlerProducerAddress(string rawText)
    {
        var lines = GetCleanLines(rawText);

        var startLine = lines.FirstOrDefault(x =>
            x.Contains("bottled by", StringComparison.OrdinalIgnoreCase) ||
            x.Contains("produced by", StringComparison.OrdinalIgnoreCase) ||
            x.Contains("distilled by", StringComparison.OrdinalIgnoreCase) ||
            x.Contains("imported by", StringComparison.OrdinalIgnoreCase));

        return startLine;
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

        foreach (var country in countries)
        {
            if (rawText.Contains(country, StringComparison.OrdinalIgnoreCase))
                return country;
        }

        return null;
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

    private static string CalculateSimpleConfidenceJson(string rawText)
    {
        var brand = ExtractBrandName(rawText);
        var alcohol = ExtractAlcoholContent(rawText);
        var net = ExtractNetContents(rawText);
        var country = ExtractCountryOfOrigin(rawText);
        var warning = ExtractGovernmentWarning(rawText);

        return $$"""
        {
          "brandName": {{(brand is null ? "0.20" : "0.70")}},
          "classTypeDesignation": {{(ExtractClassTypeDesignation(rawText) is null ? "0.20" : "0.70")}},
          "alcoholContent": {{(alcohol is null ? "0.20" : "0.85")}},
          "netContents": {{(net is null ? "0.20" : "0.85")}},
          "countryOfOrigin": {{(country is null ? "0.20" : "0.80")}},
          "governmentWarning": {{(warning is null ? "0.20" : "0.85")}}
        }
        """;
    }

    private static bool ShouldRequireHumanReview(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
            return true;

        var alcoholContent = ExtractAlcoholContent(rawText);
        var netContents = ExtractNetContents(rawText);
        var warning = ExtractGovernmentWarning(rawText);

        if (alcoholContent is null)
            return true;

        if (netContents is null)
            return true;

        if (warning is null)
            return true;

        return false;
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

    private async Task<WorkerStatus> GetOrCreateWorkerStatusAsync(
        string workerName,
        CancellationToken cancellationToken)
    {
        var machineName = Environment.MachineName;

        var workerStatus = await _dbContext.WorkerStatuses
            .FirstOrDefaultAsync(x =>
                x.WorkerName == workerName &&
                x.MachineName == machineName,
                cancellationToken);

        if (workerStatus is not null)
            return workerStatus;

        workerStatus = new WorkerStatus
        {
            Id = Guid.NewGuid(),
            WorkerName = workerName,
            MachineName = machineName,
            Status = "Idle",
            CurrentJobId = null,
            FilesProcessing = 0,
            TotalJobsProcessed = 0,
            LastHeartbeatUtc = DateTime.UtcNow
        };

        _dbContext.WorkerStatuses.Add(workerStatus);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return workerStatus;
    }

    private async Task MarkWorkerBusyAsync(
        WorkerStatus workerStatus,
        Guid jobId,
        CancellationToken cancellationToken)
    {
        workerStatus.Status = "Processing";
        workerStatus.CurrentJobId = jobId;
        workerStatus.FilesProcessing = 1;
        workerStatus.LastHeartbeatUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task UpdateWorkerIdleAsync(
        WorkerStatus workerStatus,
        CancellationToken cancellationToken)
    {
        workerStatus.Status = "Idle";
        workerStatus.CurrentJobId = null;
        workerStatus.FilesProcessing = 0;
        workerStatus.LastHeartbeatUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}