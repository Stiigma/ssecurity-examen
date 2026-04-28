using ExamenSecurity.Api.Entities;
using ExamenSecurity.Api.Services;

namespace ExamenSecurity.Api;

public sealed class GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unhandled exception captured by global middleware.");

            var auditService = context.RequestServices.GetRequiredService<ISecurityAuditService>();
            await auditService.AuditAsync(
                new SecurityAuditRequest(
                    SecurityEventType.UnhandledException,
                    SecuritySeverity.Error,
                    "UnhandledException",
                    "Se capturo una excepcion no controlada. Se omitieron detalles internos de la respuesta.",
                    StatusCode: StatusCodes.Status500InternalServerError,
                    Metadata: new Dictionary<string, object?>
                    {
                        ["exceptionType"] = exception.GetType().Name
                    }),
                context.RequestAborted);

            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsJsonAsync(new
                {
                    message = "Ocurrio un error interno.",
                    correlationId = context.Items[CorrelationIdMiddleware.ItemName]
                });
            }
        }
    }
}
