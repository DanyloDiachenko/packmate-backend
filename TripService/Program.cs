using System.Text;
using System.Reflection;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using TripService.Data;
using TripService.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<TripDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddValidatorsFromAssemblyContaining<Program>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Packmate Trip Service API",
        Version = "v1",
        Description = "Trip management and AI recommendations service for Packmate."
    });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Enter JWT Bearer token format: Bearer {your token}",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT"
    });

    options.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference("Bearer"),
            new List<string>()
        }
    });
});

var jwtSection = builder.Configuration.GetSection("Jwt");
var secretKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSection["Key"]!));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidAudience = jwtSection["Audience"],
            IssuerSigningKey = secretKey
        };
    });

builder.Services.AddHttpClient("WeatherService", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:WeatherService"] ?? "http://weather-service:8080");
});

builder.Services.AddHttpClient("TripItemsService", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:TripItemsService"] ?? "http://trip-items-service:8080");
});

builder.Services.AddHttpClient("AIService", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:AIService"] ?? "http://ai-service:8080");
    client.Timeout = TimeSpan.FromSeconds(35);
});

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

app.MapGroup("/trips")
   .RequireAuthorization()
   .MapTripEndpoints();

app.Run();