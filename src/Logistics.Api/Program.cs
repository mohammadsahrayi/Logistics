using Logistics.Modules.Capacity;
using Logistics.Modules.Booking;
using Logistics.Modules.Capacity.Contracts;
using Logistics.Infrastructure.Persistence;
using Logistics.Infrastructure.Services;
using Logistics.Shared;
using Logistics.Shared.Messaging;
using Logistics.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Logistics.Infrastructure.Services.Logistics.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddHealthChecks().AddCheck<PostgresHealthCheck>("postgres");
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var connectionString = builder.Configuration.GetConnectionString("LogisticsDb")
    ?? throw new InvalidOperationException("ConnectionStrings:LogisticsDb is not configured.");

builder.Services.AddDbContext<LogisticsDbContext>(options =>
    options.UseNpgsql(connectionString));

// Register shared infrastructure
builder.Services.AddScoped<IClock,DbClock>();
builder.Services.AddScoped<IIntegrationEventDeserializer, IntegrationEventDeserializer>();;
builder.Services.AddSingleton<IMessageSender, LoggingMessageSender>();

// Register modules
builder.Services.AddCapacityModule(null!);  // DbContext is resolved via DI
builder.Services.AddBookingModule();

// Background services
builder.Services.AddScoped<ExpiryWorker>();
builder.Services.AddScoped<OutboxPublisher>();
builder.Services.AddScoped<IntegrationEventConsumer>();
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
