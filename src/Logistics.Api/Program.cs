using Logistics.Application.Contracts;
using Logistics.Infrastructure.Repositories;
using Logistics.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// DbContext
builder.Services.AddDbContext<Logistics.Infrastructure.Persistence.LogisticsDbContext>(options =>
{
    // Use in-memory provider by default for local runs; in real runs use PostgreSQL connection from configuration
    options.UseInMemoryDatabase("logistics_dev");
});

// Repositories and services
builder.Services.AddScoped<VoyageCapacityRepository>();
builder.Services.AddScoped<ICapacityService, CapacityService>();
builder.Services.AddScoped<IClock, DbClock>();
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
