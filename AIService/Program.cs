using System.Reflection;
using Microsoft.OpenApi;
using FluentValidation;
using AIService.Endpoints;
using AIService.Services;

var builder = WebApplication.CreateBuilder(args);

var baseUrl = builder.Configuration["AiSettings:BaseUrl"] ?? "https://generativelanguage.googleapis.com/v1beta/";
builder.Services.AddHttpClient<IAIService, LlmPackingService>(client =>
{
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddValidatorsFromAssemblyContaining<Program>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ReadyRoam AI Service API",
        Version = "v1",
        Description = "AI-powered packing recommendation service for ReadyRoam."
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

app.MapGroup("/ai").MapAIEndpoints();

app.Run();