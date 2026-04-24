using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SGA.Infrastructure.Services;

namespace SGA.Infrastructure.Middleware;

public class ExceptionLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionLoggingMiddleware> _logger;

    public ExceptionLoggingMiddleware(RequestDelegate next, ILogger<ExceptionLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ISgaDataService dataService)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            string? email = null;
            try
            {
                email = context.Session.GetString("UsuarioEmail");
            }
            catch
            {
            }
            var code = $"EX-{DateTime.UtcNow:yyyyMMddHHmmss}";
            var detail = $"{ex.Message} | {ex.StackTrace}";

            try
            {
                await dataService.LogSystemErrorAsync(code, detail, "ALTA", email);
            }
            catch (Exception logEx)
            {
                _logger.LogError(logEx, "No se pudo registrar el error en BD.");
            }

            _logger.LogError(ex, "Error no controlado capturado por middleware.");
            throw;
        }
    }
}
