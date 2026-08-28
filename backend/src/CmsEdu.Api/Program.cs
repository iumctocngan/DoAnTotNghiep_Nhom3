using CmsEdu.Infrastructure;
using CmsEdu.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Infrastructure services (DbContext, Identity)
builder.Services.AddInfrastructureServices(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Seed initial database in development or when starting
if (app.Environment.IsDevelopment())
{
    await DbInitializer.InitializeAsync(app.Services);
}

app.Run();
