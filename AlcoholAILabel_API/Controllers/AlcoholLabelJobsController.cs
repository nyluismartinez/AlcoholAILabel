using AlcoholAILabel_Data.Database;
using AlcoholAILabel_Data.Entities;
using AlcoholAILabel_Shared.Dtos;
using AlcoholAILabel_Shared.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AlcoholAILabel_API.Controllers;


[ApiController]
[Route("api/[controller]")]
public class AlcoholLabelJobsController : ControllerBase
{
    private readonly AlcoholLabelDbContext _dbContext;
    private readonly IConfiguration _configuration;

    public AlcoholLabelJobsController(
        AlcoholLabelDbContext dbContext,
        IConfiguration configuration)
    {
        _dbContext = dbContext;
        _configuration = configuration;
    }

    [HttpPost("upload")]
    [RequestSizeLimit(50_000_000)]
    public async Task<ActionResult<CreateAlcoholLabelJobResponse>> UploadAsync(
        [FromForm] string productName,
        [FromForm] AlcoholProductType productType,
        [FromForm] SourceProduct sourceProduct,
        [FromForm] IFormFile labelImage,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(productName))
            return BadRequest("Product name is required.");

        if (labelImage is null || labelImage.Length == 0)
            return BadRequest("Label image is required.");

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".tif", ".tiff", ".webp" };

        var extension = Path.GetExtension(labelImage.FileName).ToLowerInvariant();

        if (!allowedExtensions.Contains(extension))
            return BadRequest("Invalid file type. Allowed: jpg, jpeg, png, bmp, tif, tiff, webp.");

        var jobId = Guid.NewGuid();

        var uploadRootPath = _configuration["AlcoholLabelStorage:UploadRootPath"];

        if (string.IsNullOrWhiteSpace(uploadRootPath))
            return StatusCode(500, "Upload path is not configured.");

        var jobFolder = Path.Combine(uploadRootPath, jobId.ToString());

        Directory.CreateDirectory(jobFolder);

        var safeFileName = $"{jobId}{extension}";

        var savedFilePath = Path.Combine(jobFolder, safeFileName);

        await using (var stream = new FileStream(savedFilePath, FileMode.Create))
        {
            await labelImage.CopyToAsync(stream, cancellationToken);
        }

        var job = new AlcoholLabelJob
        {
            Id = jobId,
            ProductName = productName.Trim(),
            ProductType = productType,
            SourceProduct = sourceProduct,
            UploadedFilePath = savedFilePath,
            Status = AlcoholLabelJobStatus.Uploaded,
            PercentComplete = 0,
            CreatedDate = DateTime.UtcNow
        };

        _dbContext.AlcoholLabelJobs.Add(job);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new CreateAlcoholLabelJobResponse
        {
            JobId = jobId,
            Status = job.Status.ToString(),
            Message = "Alcohol label uploaded. Not sent for processing yet."
        });
    }

    [HttpGet("{jobId:guid}")]
    public async Task<ActionResult<AlcoholLabelJobDetailsDto>> GetByIdAsync(
        Guid jobId,
        CancellationToken cancellationToken)
    {
        var job = await _dbContext.AlcoholLabelJobs
            .AsNoTracking()
            .Include(x => x.ExtractionResult)
            .FirstOrDefaultAsync(x => x.Id == jobId, cancellationToken);

        if (job is null)
            return NotFound("Job was not found.");

        var dto = new AlcoholLabelJobDetailsDto
        {
            Id = job.Id,
            ProductName = job.ProductName,
            ProductType = job.ProductType.ToString(),
            SourceProduct = job.SourceProduct.ToString(),
            UploadedFilePath = job.UploadedFilePath,
            Status = job.Status.ToString(),
            PercentComplete = job.PercentComplete,
            CreatedDate = job.CreatedDate,
            StartedDate = job.StartedDate,
            CompletedDate = job.CompletedDate,
            ErrorMessage = job.ErrorMessage,
            ExtractionResult = job.ExtractionResult is null
                ? null
                : new AlcoholLabelExtractionResultDto
                {
                    Id = job.ExtractionResult.Id,
                    JobId = job.ExtractionResult.JobId,
                    RawOcrText = job.ExtractionResult.RawOcrText,
                    BrandName = job.ExtractionResult.BrandName,
                    ClassTypeDesignation = job.ExtractionResult.ClassTypeDesignation,
                    AlcoholContent = job.ExtractionResult.AlcoholContent,
                    NetContents = job.ExtractionResult.NetContents,
                    BottlerProducerNameAddress = job.ExtractionResult.BottlerProducerNameAddress,
                    CountryOfOrigin = job.ExtractionResult.CountryOfOrigin,
                    GovernmentWarning = job.ExtractionResult.GovernmentWarning,
                    ConfidenceJson = job.ExtractionResult.ConfidenceJson,
                    NeedsHumanReview = job.ExtractionResult.NeedsHumanReview,
                    CreatedDate = job.ExtractionResult.CreatedDate
                }
        };

        return Ok(dto);
    }


    [HttpPut("{jobId:guid}/review")]
    public async Task<ActionResult<AlcoholLabelJobDetailsDto>> UpdateReviewAsync(
    Guid jobId,
    [FromBody] UpdateAlcoholLabelReviewRequest request,
    CancellationToken cancellationToken)
    {
        var job = await _dbContext.AlcoholLabelJobs
            .Include(x => x.ExtractionResult)
            .FirstOrDefaultAsync(x => x.Id == jobId, cancellationToken);

        if (job is null)
            return NotFound("Job was not found.");

        if (job.ExtractionResult is null)
            return BadRequest("Extraction result does not exist for this job.");

        job.ExtractionResult.BrandName = request.BrandName;
        job.ExtractionResult.ClassTypeDesignation = request.ClassTypeDesignation;
        job.ExtractionResult.AlcoholContent = request.AlcoholContent;
        job.ExtractionResult.NetContents = request.NetContents;
        job.ExtractionResult.BottlerProducerNameAddress = request.BottlerProducerNameAddress;
        job.ExtractionResult.CountryOfOrigin = request.CountryOfOrigin;
        job.ExtractionResult.GovernmentWarning = request.GovernmentWarning;

        if (request.Approve)
        {
            job.ExtractionResult.NeedsHumanReview = false;
            job.Status = AlcoholLabelJobStatus.Completed;
            job.PercentComplete = 100;
            job.CompletedDate ??= DateTime.UtcNow;
        }
        else
        {
            job.ExtractionResult.NeedsHumanReview = true;
            job.Status = AlcoholLabelJobStatus.NeedsHumanReview;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(jobId, cancellationToken);
    }


    [HttpGet("uploaded")]
    public async Task<ActionResult<List<RecentAlcoholLabelJobDto>>> GetUploadedJobsAsync(
    CancellationToken cancellationToken)
    {
        var jobs = await _dbContext.AlcoholLabelJobs
            .AsNoTracking()
            .Where(x => x.Status == AlcoholLabelJobStatus.Uploaded)
            .OrderByDescending(x => x.CreatedDate)
            .Select(x => new RecentAlcoholLabelJobDto
            {
                Id = x.Id,
                ProductName = x.ProductName,
                ProductType = x.ProductType.ToString(),
                SourceProduct = x.SourceProduct.ToString(),
                Status = x.Status.ToString(),
                PercentComplete = x.PercentComplete,
                NeedsHumanReview = false,
                CreatedDate = x.CreatedDate,
                StartedDate = x.StartedDate,
                CompletedDate = x.CompletedDate,
                ErrorMessage = x.ErrorMessage
            })
            .ToListAsync(cancellationToken);

        return Ok(jobs);
    }

    [HttpPost("send-for-processing")]
    public async Task<ActionResult> SendForProcessingAsync(
        [FromBody] SendAlcoholLabelJobsForProcessingRequest request,
        CancellationToken cancellationToken)
    {
        if (request.JobIds.Count == 0)
            return BadRequest("No jobs were selected.");

        var jobs = await _dbContext.AlcoholLabelJobs
            .Where(x =>
                request.JobIds.Contains(x.Id) &&
                x.Status == AlcoholLabelJobStatus.Uploaded)
            .ToListAsync(cancellationToken);

        if (jobs.Count == 0)
            return BadRequest("No uploaded jobs were found.");

        foreach (var job in jobs)
        {
            job.Status = AlcoholLabelJobStatus.Queued;
            job.PercentComplete = 0;
            job.ErrorMessage = null;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            Message = $"{jobs.Count} job(s) sent for processing."
        });
    }

}

