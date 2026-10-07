using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using SlseaSolarApi.Api.Application.Common;
using SlseaSolarApi.Api.Application.Security;
using SlseaSolarApi.Api.Infrastructure.Persistence;
using SlseaSolarApi.Api.Infrastructure.Persistence.Seeding;
using SlseaSolarApi.Api.Presentation.Filters;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Hosting / platform integration
// ---------------------------------------------------------------------------
// Containers and PaaS hosts inject the port to listen on via PORT (Heroku/Render/Fly style) or
// ASPNETCORE_URLS. Honouring both means the same image runs unchanged on any host.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

// The platform terminates TLS at its own proxy, so forwarded headers must be honoured for
// redirects and absolute links to come out as https.
builder.Services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor |
        Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// ---------------------------------------------------------------------------
// Infrastructure layer services
// ---------------------------------------------------------------------------
// The connection string and JWT signing key come from user-secrets / environment only —
// never from a committed appsettings file (see the NFRs).
builder.Services.AddDbContext<SolarDbContext>(options =>
    DatabaseProviderSelector.Configure(options, builder.Configuration));

// --- Authentication: JWT bearer tokens issued by POST /api/auth/token -------------
// The signing key comes from configuration (environment/user-secrets) — never from a committed file.
var jwtSigningKey = builder.Configuration["Jwt:SigningKey"];
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "SlseaSolarApi";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "SlseaSolarApiClients";

builder.Services
    .AddAuthentication(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = !string.IsNullOrEmpty(jwtSigningKey)
                ? new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                    System.Text.Encoding.UTF8.GetBytes(jwtSigningKey))
                : null,
            ValidateLifetime = true,
            // The "role" claim doubles as the role claim, so authorization policies can target it.
            RoleClaimType = "role",
            NameClaimType = "sub"
        };

        // Failed authentication must return 401 with the standard JSON error body, not an empty
        // response or a redirect.
        options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new ApiError
                {
                    Code = "UNAUTHENTICATED",
                    Message = "Authentication is required. Obtain a token from POST /api/auth/token.",
                    Detail = context.Error is { Length: > 0 } err ? "The token was missing or invalid." : null,
                    TraceId = context.HttpContext.TraceIdentifier
                });
            },
            OnForbidden = async context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new ApiError
                {
                    Code = "FORBIDDEN",
                    Message = "You are authenticated but not authorized for this resource.",
                    TraceId = context.HttpContext.TraceIdentifier
                });
            }
        };
    });

// ---------------------------------------------------------------------------
// Presentation layer services
// ---------------------------------------------------------------------------
builder.Services.AddControllers(options =>
{
    // Every failure — handled or not — leaves through the one error schema (decision D10).
    options.Filters.Add<ApiExceptionFilter>();
})
.ConfigureApiBehaviorOptions(options =>
{
    // Replace the framework's ProblemDetails-style automatic 400 with the project's ApiError body,
    // so every 4xx has exactly one shape.
    options.SuppressMapClientErrors = true;
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Where(entry => entry.Value?.Errors.Count > 0)
            .ToDictionary(
                entry => entry.Key,
                entry => entry.Value!.Errors
                    .Select(error => error.ErrorMessage)
                    .ToArray());

        return new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(new ApiError
        {
            Code = "MALFORMED_REQUEST",
            Message = "The request could not be parsed or failed input validation.",
            Errors = errors,
            TraceId = context.HttpContext.TraceIdentifier
        });
    };
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddScoped<DatabaseSeeder>();
builder.Services.AddScoped<JwtTokenService>();

// OpenAPI is a first-class deliverable: it must be served from the deployment, so it is enabled in
// every environment, not only Development (this is what the brief explicitly requires).

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "SLSEA National Solar Generation API",
        Version = "v1",
        Description =
            "API for acquiring real-time and historical rooftop-solar generation data from " +
            "metered installations across Sri Lanka, and serving it to SLSEA operational and " +
            "analytical consumers.",
        Contact = new OpenApiContact
        {
            Name = "SLSEA Solar Generation API",
            Url = new Uri("https://github.com/JMNSJ/Web-Api-Cobsccomp251p-040")
        }
    });

    // Surface the XML doc comments (the documentation surface is part of the product).
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }

    // The bearer-token "Authorize" button, so the write path can be exercised from the UI.
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the raw JWT from POST /api/auth/token. The 'Bearer ' prefix is added automatically."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Open CORS for the module; a real deployment would restrict this to known origins.
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

var app = builder.Build();

// ---------------------------------------------------------------------------
// Database bootstrap: apply migrations, then seed if empty.
// Development convenience only — a production deployment would run migrations as
// a controlled step rather than on application start.
// ---------------------------------------------------------------------------
if (app.Configuration.GetValue("Database:MigrateOnStartup", true))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<SolarDbContext>();

    // PostgreSQL/SQL Server have a migration set, so schema changes are applied incrementally.
    // SQLite (local development) has no migration set of its own, so the schema is created
    // directly from the model. Both paths are idempotent.
    if (string.Equals(
            DatabaseProviderSelector.Resolve(app.Configuration),
            DatabaseProviderSelector.Sqlite,
            StringComparison.OrdinalIgnoreCase))
    {
        await db.Database.EnsureCreatedAsync();
    }
    else
    {
        await db.Database.MigrateAsync();
    }

    if (app.Configuration.GetValue("Database:SeedOnStartup", true))
    {
        var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
        await seeder.SeedAsync();
    }
}

// ---------------------------------------------------------------------------
// HTTP request pipeline
// ---------------------------------------------------------------------------
app.UseForwardedHeaders();

// Swagger is served from the deployment in every environment — it is a graded deliverable.
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "SLSEA National Solar Generation API v1");
    options.DocumentTitle = "SLSEA National Solar Generation API";

    // Serve the UI at the site root as well, so the bare deployment URL lands on the docs.
    options.RoutePrefix = string.Empty;
});

app.UseCors();

app.UseAuthentication();

// Only redirect to HTTPS when we are not already behind a TLS-terminating proxy.
if (!app.Configuration.GetValue("Http:SkipHttpsRedirect", false))
{
    app.UseHttpsRedirection();
}

app.UseAuthorization();
app.MapControllers();

app.Run();

/// <summary>
/// Entry point marker type, exposed so integration tests can reference the host assembly.
/// </summary>
public partial class Program;