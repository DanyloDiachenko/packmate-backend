using System.Reflection;
using Microsoft.OpenApi;
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
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Packmate Weather Service API",
        Version = "v1",
        Description = "Weather forecast and climate condition service for Packmate."
    });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.MapGroup("/weather").MapWeatherEndpoints();

app.Run();