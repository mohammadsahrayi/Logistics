using System.Diagnostics;

namespace Logistics.Api.Infrastructure
{
    public sealed class CorrelationIdMiddleware
    {
        private const string HeaderName = "X-Correlation-ID";
        private readonly RequestDelegate _next;

        public CorrelationIdMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var correlationId = context.Request.Headers[HeaderName].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(correlationId))
                correlationId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;

            context.TraceIdentifier = correlationId;
            context.Response.Headers[HeaderName] = correlationId;
            using (context.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger("CorrelationIdMiddleware")
                .BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
            {
                await _next(context);
            }
        }
    }
}
