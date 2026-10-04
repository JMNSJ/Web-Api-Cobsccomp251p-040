using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using SlseaSolarApi.Api.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Infrastructure layer services
// ---------------------------------------------------------------------------
// The connection string and JWT signing key come from user-secrets / environment only —
// never from a committed appsettings file (see the NFRs).
builder.Services.AddDbContext<SolarDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("SolarDb"),
        sql => sql.EnableRetryOnFailure()));

// ---------------------------------------------------------------------------
// Presentation layer services
// ---------------------------------------------------------------------------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

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
});

var app = builder.Build();

// ---------------------------------------------------------------------------
// HTTP request pipeline
// ---------------------------------------------------------------------------
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();

/// <summary>
/// Entry point marker type, exposed so integration tests can reference the host assembly.
/// </summary>
public partial class Program;