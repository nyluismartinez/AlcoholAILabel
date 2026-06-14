using AlcoholAILabel_Data.Database;
using AlcoholAILabel_Shared.Dtos;
using AlcoholAILabel_Shared.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AlcoholAILabel_API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AlcoholLabelDashboardController : ControllerBase
{
    private readonly AlcoholLabelDbContext _dbContext;

    public AlcoholLabelDashboardController(AlcoholLabelDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<AlcoholLabelDashboardDto>> GetDashboardAsync(
        CancellationToken cancellationToken)
    {
        var jobs = _dbContext.AlcoholLabelJobs.AsNoTracking();

        var totalJobs = await jobs.CountAsync(cancellationToken);
        var queued = await jobs.CountAsync(x => x.Status == AlcoholLabelJobStatus.Queued, cancellationToken);
        var processing = await jobs.CountAsync(x => x.Status == AlcoholLabelJobStatus.Processing, cancellationToken);
        var completed = await jobs.CountAsync(x => x.Status == AlcoholLabelJobStatus.Completed, cancellationToken);
        var failed = await jobs.CountAsync(x => x.Status == AlcoholLabelJobStatus.Failed, cancellationToken);

        var needsReview = await _dbContext.AlcoholLabelExtractionResults
            .AsNoTracking()
            .CountAsync(x => x.NeedsHumanReview, cancellationToken);

        var workers = await _dbContext.WorkerStatuses
            .AsNoTracking()
            .OrderBy(x => x.WorkerName)
            .Select(x => new WorkerStatusDto
            {
                Id = x.Id,
                WorkerName = x.WorkerName,
                MachineName = x.MachineName,
                Status = x.Status,
                CurrentJobId = x.CurrentJobId,
                FilesProcessing = x.FilesProcessing,
                TotalJobsProcessed = x.TotalJobsProcessed,
                LastHeartbeatUtc = x.LastHeartbeatUtc
            })
            .ToListAsync(cancellationToken);

        var recentJobs = await _dbContext.AlcoholLabelJobs
            .AsNoTracking()
            .Include(x => x.ExtractionResult)
            .OrderByDescending(x => x.CreatedDate)
            .Take(25)
            .Select(x => new RecentAlcoholLabelJobDto
            {
                Id = x.Id,
                ProductName = x.ProductName,
                ProductType = x.ProductType.ToString(),
                SourceProduct = x.SourceProduct.ToString(),
                Status = x.Status.ToString(),
                PercentComplete = x.PercentComplete,
                NeedsHumanReview = x.ExtractionResult != null && x.ExtractionResult.NeedsHumanReview,
                CreatedDate = x.CreatedDate,
                StartedDate = x.StartedDate,
                CompletedDate = x.CompletedDate,
                ErrorMessage = x.ErrorMessage
            })
            .ToListAsync(cancellationToken);

        var dashboard = new AlcoholLabelDashboardDto
        {
            JobStats = new AlcoholLabelJobStatsDto
            {
                TotalJobs = totalJobs,
                Queued = queued,
                Processing = processing,
                Completed = completed,
                Failed = failed,
                NeedsReview = needsReview
            },
            Workers = workers,
            RecentJobs = recentJobs
        };

        return Ok(dashboard);
    }
}