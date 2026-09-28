using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

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