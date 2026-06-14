using AlcoholAILabel_API.Services.LocalAi;
using AlcoholAILabel_Data.Database;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 50_000_000;
});


builder.Services.AddDbContext<AlcoholLabelDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));


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

builder.Services.AddScoped<ApiLocalLlmClient>();




builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();


app.UseSwagger();
app.UseSwaggerUI();


// Configure the HTTP request pipeline.

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
