using Gta.LegalCopilot.Api;
using Gta.LegalCopilot.Api.Endpoints;
using Gta.LegalCopilot.Application.Common;
using Gta.LegalCopilot.Infrastructure;
using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLegalCopilot(builder.Configuration);
builder.Services.ConfigureHttpJsonOptions(o => JsonDefaults.Configure(o.SerializerOptions));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

var maxUpload = builder.Configuration.GetValue<long?>("Storage:MaxUploadBytes") ?? 20 * 1024 * 1024;
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = maxUpload * 5);
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = maxUpload * 5);

var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["http://localhost:5173"];
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseExceptionHandler();
app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapLegalCopilotEndpoints();
app.MapFallbackToFile("index.html");

app.Run();

public partial class Program;
