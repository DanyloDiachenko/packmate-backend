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
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGroup("/ai").MapAIEndpoints();

app.Run();