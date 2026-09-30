var builder = WebApplication.CreateBuilder(args);

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseCors("AllowAll");

app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/user-service/swagger/v1/swagger.json", "User Service API");
    c.SwaggerEndpoint("/trip-service/swagger/v1/swagger.json", "Trip Service API");
    c.SwaggerEndpoint("/trip-items-service/swagger/v1/swagger.json", "Trip Items Service API");
    c.SwaggerEndpoint("/weather-service/swagger/v1/swagger.json", "Weather Service API");
    c.SwaggerEndpoint("/ai-service/swagger/v1/swagger.json", "AI Service API");
    c.RoutePrefix = "swagger";
    c.DocumentTitle = "Packmate API Documentation";
});

app.MapGet("/", () => Results.Redirect("/swagger"));

app.MapReverseProxy();

app.Run();