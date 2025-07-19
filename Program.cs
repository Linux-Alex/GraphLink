using Microsoft.AspNetCore.Mvc;
using Microsoft.Graph;
using Azure.Identity;
using Microsoft.Graph.Models;
using Microsoft.OpenApi.Models;

var builder = Microsoft.AspNetCore.Builder.WebApplication.CreateBuilder(args);

// Load AzureAD config from appsettings.json
var azureAdConfig = builder.Configuration.GetSection("AzureAD").Get<AzureADConfig>();
if (azureAdConfig == null)
{
    throw new InvalidOperationException("AzureAD configuration section is missing.");
}
builder.Services.AddSingleton(azureAdConfig);

// Register services
builder.Services.AddSingleton<M365EmailService>();

// Add minimal CORS only for Swagger UI (localhost origin)
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Add Swagger with API key support
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "GraphLinker API", Version = "v1" });
    
    c.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Description = "API Key needed to access the endpoints. Use 'X-API-KEY: {your_api_key}'",
        In = ParameterLocation.Header,
        Name = "X-API-KEY",
        Type = SecuritySchemeType.ApiKey
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "ApiKey"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

const string ApiKeyHeaderName = "X-API-KEY";
const string ApiKeyValue = "supersecureapikey123!"; // Move to config in prod

// === Middleware ===
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

// app.UseHttpsRedirection();

// Use Swagger UI only with CORS allowed from localhost
app.UseWhen(ctx => ctx.Request.Path.StartsWithSegments("/swagger"), appBuilder =>
{
    appBuilder.UseCors("AllowSwagger");
});

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "GraphLinker API v1");
    c.RoutePrefix = string.Empty; // Makes Swagger UI available at root URL
});

// Middleware to check API key except for Swagger
// Middleware to check API key except for Swagger
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? "";

    // List of paths that shouldn't require API key
    var excludedPaths = new[]
    {
        "/swagger",
        "/favicon.ico",
        "/swagger-ui.css",
        "/swagger-ui-bundle.js",
        "/swagger-ui-standalone-preset.js",
        "/swagger.json",
        "/index.html"
    };

    if (excludedPaths.Any(p => path.StartsWith(p)))
    {
        await next();
        return;
    }

    if (!context.Request.Headers.TryGetValue(ApiKeyHeaderName, out var extractedApiKey))
    {
        Console.WriteLine($"Missing API key header for path: {path}");
        context.Response.StatusCode = 401;
        await context.Response.WriteAsync("Unauthorized: Missing API key header");
        return;
    }

    if (extractedApiKey != ApiKeyValue)
    {
        Console.WriteLine($"Invalid API key for path: {path}. Provided: {extractedApiKey}");
        context.Response.StatusCode = 401;
        await context.Response.WriteAsync("Unauthorized: Invalid API key");
        return;
    }

    await next();
});

app.UseCors();

// === API Endpoints ===

// 1) Read emails with filtering
app.MapGet("/api/emails/{userEmail}", async (
    string userEmail,
    M365EmailService emailService,
    [FromQuery] int top = 10,
    [FromQuery] string folder = "Inbox",
    [FromQuery] DateTimeOffset? fromDate = null) =>
{
    if (!emailService.IsEmailAllowed(userEmail))
        return Results.Unauthorized();

    var messages = await emailService.ReadEmailsAsync(userEmail, top, folder, fromDate);
    return Results.Ok(messages.Select(m => new
    {
        m.Id,
        m.Subject,
        From = m.From?.EmailAddress?.Address,
        m.ReceivedDateTime,
        m.BodyPreview,
        Attachments = m.Attachments?.Select(a => new
        {
            a.Id,
            a.Name,
            a.Size,
            a.ContentType,
        })
    }));
}).WithName("ReadEmails").WithOpenApi();

// 2) Send email
app.MapPost("/api/emails/{userEmail}", async (
    string userEmail,
    EmailRequest request,
    M365EmailService emailService) =>
{
    if (!emailService.IsEmailAllowed(userEmail))
        return Results.Unauthorized();

    var toRecipients = request.To?.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
    var ccRecipients = request.Cc?.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
    var bccRecipients = request.Bcc?.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];

    var allRecipients = toRecipients.Concat(ccRecipients).Concat(bccRecipients);
    foreach (var recipient in allRecipients)
    {
        if (!emailService.IsReceiverAllowed(userEmail, recipient))
            return Results.BadRequest($"Recipient '{recipient}' is not allowed for sender '{userEmail}'.");
    }

    await emailService.SendEmailAsync(
        fromUserEmail: userEmail,
        toRecipients: toRecipients,
        subject: request.Subject,
        body: request.Body,
        ccRecipients: ccRecipients,
        bccRecipients: bccRecipients,
        attachments: request.Attachments
    );

    return Results.Ok("Email sent.");
}).WithName("SendEmail").WithOpenApi();

app.Run();

// ========== Models ==========

public class EmailRequest
{
    public string To { get; set; } = string.Empty;
    public string? Cc { get; set; }
    public string? Bcc { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public List<EmailAttachment>? Attachments { get; set; }
}

public record EmailAttachment(
    string Name,
    string ContentType,
    string Base64Content
);

public record AllowedAccount
{
    public string Email { get; init; } = null!;
    public string Password { get; init; } = null!;
    public string DisplayName { get; init; } = null!;
    public List<string> AllowedRecivers { get; init; } = new();
}

public record AzureADConfig
{
    public string ClientId { get; init; } = null!;
    public string TenantId { get; init; } = null!;
    public string ClientSecret { get; init; } = null!;
    public string RedirectUri { get; init; } = null!;
    public List<AllowedAccount> AllowedAccounts { get; init; } = new();
}
