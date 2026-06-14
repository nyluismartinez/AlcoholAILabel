using AlcoholAILabel.Worker.Services;
using AlcoholAILabel_Data.Database;
using AlcoholAILabel_Worker;
using AlcoholAILabel_Worker.Services;
using AlcoholAILabel_Worker.Services.Ocr;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);



builder.Services.AddDbContext<AlcoholLabelDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));


builder.Services.AddScoped<IOcrService, NativeTesseractOcrService>();


builder.Services.AddScoped<AlcoholLabelJobProcessor>();



builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
