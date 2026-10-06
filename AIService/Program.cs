using MailService.Endpoints;
using MailService.Interfaces;
using MailService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));

builder.Services.AddScoped<IEmailService, GmailEmailService>();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen();
var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapGroup("/mail").MapMailEndpoints();

app.Run();