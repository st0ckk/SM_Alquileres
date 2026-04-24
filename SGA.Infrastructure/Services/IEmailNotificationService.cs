namespace SGA.Infrastructure.Services;

public interface IEmailNotificationService
{
    Task SendWelcomeEmailAsync(string recipientEmail, string recipientName, CancellationToken cancellationToken = default);
    Task SendAppointmentEmailAsync(string recipientEmail, string recipientName, DateTime fechaVisita, string propiedadInteres, CancellationToken cancellationToken = default);
}

