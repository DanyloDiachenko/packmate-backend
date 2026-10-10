using System.Net.Http.Headers;
using System.Reflection;
using Microsoft.OpenApi;
using DestinationService.Endpoints;
using DestinationService.Services;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient<IDestinationService, DestinationServiceImpl>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
    client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ReadyRoamBackend", "1.0"));
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

var redisConfig = builder.Configuration["Redis:Configuration"];
if (!string.IsNullOrWhiteSpace(redisConfig))
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        var configOptions = ConfigurationOptions.Parse(redisConfig);
        configOptions.AbortOnConnectFail = false;
        options.ConfigurationOptions = configOptions;
        options.InstanceName = "ReadyRoam_Dest_";
    });
}
else
{
    builder.Services.AddDistributedMemoryCache();
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ReadyRoam Destination Service API",
        Version = "v1",
        Description = "Destination catalog, search by country/city, country flags, and scenic banner images for ReadyRoam."
    });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

var app = builder.Build();

app.UseCors("AllowAll");
app.UseSwagger();
app.UseSwaggerUI();

app.MapGroup("/destinations").MapDestinationEndpoints();

app.Run();
