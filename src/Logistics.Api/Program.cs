using Logistics.Application.Contracts;
using Logistics.Infrastructure.Repositories;
using Logistics.Infrastructure.Services;
using Logistics.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddHealthChecks().AddCheck<PostgresHealthCheck>("postgres");
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var connectionString = builder.Configuration.GetConnectionString("LogisticsDb")
    ?? throw new InvalidOperationException("ConnectionStrings:LogisticsDb is not configured.");

builder.Services.AddDbContext<Logistics.Infrastructure.Persistence.LogisticsDbContext>(options =>
    options.UseNpgsql(connectionString));

// Repositories and services
builder.Services.AddScoped<VoyageCapacityRepository>();
builder.Services.AddScoped<ICapacityService, CapacityService>();
builder.Services.AddScoped<IClock, DbClock>();
builder.Services.AddScoped<ExpiryWorker>();
builder.Services.AddScoped<OutboxPublisher>();
builder.Services.AddScoped<IntegrationEventConsumer>();
builder.Services.AddSingleton<IMessageSender, LoggingMessageSender>();
builder.Services.AddHostedService<ExpiryWorkerHostedService>();
builder.Services.AddHostedService<OutboxPublisherHostedService>();
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseMiddleware<CorrelationIdMiddleware>();

app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
