using AlcoholAILabel.Worker.Services;
using AlcoholAILabel_Data.Database;
using AlcoholAILabel_Worker;
using AlcoholAILabel_Worker.Services;
using AlcoholAILabel_Worker.Services.Agent;
using AlcoholAILabel_Worker.Services.Agent.LocalAi;
using AlcoholAILabel_Worker.Services.Agent.Tools;
using AlcoholAILabel_Worker.Services.Ocr;
using Microsoft.EntityFrameworkCore;



var builder = Host.CreateApplicationBuilder(args);



builder.Services.AddDbContext<AlcoholLabelDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));


builder.Services.AddScoped<IOcrService, NativeTesseractOcrService>();

builder.Services.AddSingleton(sp =>
{
    var configuration =
        sp.GetRequiredService<IConfiguration>();

    var baseUrl =
        configuration["LocalAiSettings:BaseUrl"];

    if (string.IsNullOrWhiteSpace(baseUrl))
    {
        baseUrl = "http://localhost:11434";
    }

    return new HttpClient
    {
        BaseAddress = new Uri(baseUrl),
        Timeout = TimeSpan.FromMinutes(3)
    };
});

builder.Services.AddScoped<LocalLlmClient>();
builder.Services.AddScoped<RuleAlcoholLabelExtractorTool>();
builder.Services.AddScoped<AiAlcoholLabelExtractorTool>();
builder.Services.AddScoped<AlcoholLabelExtractionAgent>();




builder.Services.AddScoped<AlcoholLabelJobProcessor>();



builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
