using MudBlazor.Services;
using AlcoholAILabel_Frontend.Components;
using AlcoholAILabel_Frontend.Services;

var builder = WebApplication.CreateBuilder(args);

// Add MudBlazor services
builder.Services.AddMudServices();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();



builder.Services.AddHttpClient<AlcoholLabelJobClient>(client =>
{
    var apiBaseUrl = builder.Configuration["ApiBaseUrl"];

    if (string.IsNullOrWhiteSpace(apiBaseUrl))
        throw new InvalidOperationException("ApiBaseUrl is missing from appsettings.json.");

    client.BaseAddress = new Uri(apiBaseUrl);
});


builder.Services.AddHttpClient<AlcoholLabelDashboardClient>(client =>
{
    var apiBaseUrl = builder.Configuration["ApiBaseUrl"];

    if (string.IsNullOrWhiteSpace(apiBaseUrl))
        throw new InvalidOperationException("ApiBaseUrl is missing from appsettings.json.");

    client.BaseAddress = new Uri(apiBaseUrl);
});


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
//app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseStatusCodePagesWithReExecute("/not-found");

app.UseHttpsRedirection();


app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
