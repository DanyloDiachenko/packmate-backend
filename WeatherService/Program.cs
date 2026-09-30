using WeatherService.Endpoints;
using WeatherService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient<OpenMeteoWeatherService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration["Redis:Configuration"] ?? "localhost:6379";
    options.InstanceName = "Packmate_";
});

builder.Services.AddScoped<OpenMeteoWeatherService>();
builder.Services.AddScoped<IWeatherProviderService>(sp =>
{
    var openMeteo = sp.GetRequiredService<OpenMeteoWeatherService>();
    var cache = sp.GetRequiredService<Microsoft.Extensions.Caching.Distributed.IDistributedCache>();
    var config = sp.GetRequiredService<IConfiguration>();
    return new CachedWeatherService(openMeteo, cache, config);
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.MapGroup("/weather").MapWeatherEndpoints();

app.Run();