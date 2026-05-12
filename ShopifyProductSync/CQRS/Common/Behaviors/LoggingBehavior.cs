using MediatR;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace ShopifyProductSync.CQRS.Common.Behaviors
{
    /// <summary>
    /// MediatR pipeline behavior that logs request name, start time,
    /// end time, and elapsed milliseconds for every request.
    /// </summary>
    public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

        public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
        {
            _logger = logger;
        }

        public async Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            var requestName = typeof(TRequest).Name;
            var startTime = DateTime.UtcNow;

            _logger.LogInformation(
                "Handling {RequestName} — Start: {StartTime}",
                requestName,
                startTime.ToString("o"));

            var stopwatch = Stopwatch.StartNew();

            TResponse response;
            try
            {
                response = await next();
            }
            finally
            {
                stopwatch.Stop();
                var endTime = DateTime.UtcNow;

                _logger.LogInformation(
                    "Handled {RequestName} — End: {EndTime} — Elapsed: {ElapsedMs}ms",
                    requestName,
                    endTime.ToString("o"),
                    stopwatch.ElapsedMilliseconds);
            }

            return response;
        }
    }
}
