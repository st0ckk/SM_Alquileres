using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Mail;

namespace SGA.Infrastructure.Services;

public class SmtpEmailNotificationService(IConfiguration configuration) : IEmailNotificationService
{
    public async Task SendWelcomeEmailAsync(string recipientEmail, string recipientName, CancellationToken cancellationToken = default)
    {
        var enabled = configuration.GetValue<bool>("EmailSettings:Enabled");
        if (!enabled)
        {
            return;
        }

        var host = configuration["EmailSettings:SmtpHost"] ?? throw new InvalidOperationException("EmailSettings:SmtpHost no configurado.");
        var port = configuration.GetValue<int>("EmailSettings:SmtpPort");
        var username = configuration["EmailSettings:SmtpUsername"] ?? throw new InvalidOperationException("EmailSettings:SmtpUsername no configurado.");
        var password = configuration["EmailSettings:SmtpPassword"] ?? throw new InvalidOperationException("EmailSettings:SmtpPassword no configurado.");
        var fromName = configuration["EmailSettings:FromName"] ?? "Bienes y Raices";
        var fromEmail = configuration["EmailSettings:FromEmail"] ?? throw new InvalidOperationException("EmailSettings:FromEmail no configurado.");

        using var message = new MailMessage
        {
            From = new MailAddress(fromEmail, fromName),
            Subject = "Bienvenido a Bienes y Raices",
            Body = BuildWelcomeBody(recipientName),
            IsBodyHtml = true
        };
        message.To.Add(new MailAddress(recipientEmail));

        using var smtpClient = new SmtpClient(host, port)
        {
            EnableSsl = true,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(username, password)
        };

        cancellationToken.ThrowIfCancellationRequested();
        await smtpClient.SendMailAsync(message, cancellationToken);
    }

    public async Task SendAppointmentEmailAsync(string recipientEmail, string recipientName, DateTime fechaVisita, string propiedadInteres, CancellationToken cancellationToken = default)
    {
        var enabled = configuration.GetValue<bool>("EmailSettings:Enabled");
        if (!enabled)
        {
            return;
        }

        var host = configuration["EmailSettings:SmtpHost"] ?? throw new InvalidOperationException("EmailSettings:SmtpHost no configurado.");
        var port = configuration.GetValue<int>("EmailSettings:SmtpPort");
        var username = configuration["EmailSettings:SmtpUsername"] ?? throw new InvalidOperationException("EmailSettings:SmtpUsername no configurado.");
        var password = configuration["EmailSettings:SmtpPassword"] ?? throw new InvalidOperationException("EmailSettings:SmtpPassword no configurado.");
        var fromName = configuration["EmailSettings:FromName"] ?? "Bienes y Raices";
        var fromEmail = configuration["EmailSettings:FromEmail"] ?? throw new InvalidOperationException("EmailSettings:FromEmail no configurado.");

        using var message = new MailMessage
        {
            From = new MailAddress(fromEmail, fromName),
            Subject = "Confirmacion de cita - Bienes y Raices",
            Body = BuildAppointmentBody(recipientName, fechaVisita, propiedadInteres),
            IsBodyHtml = true
        };
        message.To.Add(new MailAddress(recipientEmail));

        using var smtpClient = new SmtpClient(host, port)
        {
            EnableSsl = true,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(username, password)
        };

        cancellationToken.ThrowIfCancellationRequested();
        await smtpClient.SendMailAsync(message, cancellationToken);
    }

    private static string BuildWelcomeBody(string recipientName)
    {
        var displayName = string.IsNullOrWhiteSpace(recipientName) ? "Cliente" : recipientName.Trim();
        return $"""
<html>
  <body style="font-family:Arial,Helvetica,sans-serif;color:#1f2937;">
    <h2 style="color:#0f172a;">Bienvenido a Bienes y Raices</h2>
    <p>Hola {WebUtility.HtmlEncode(displayName)}, tu registro fue completado correctamente.</p>
    <p>Ya puedes iniciar sesion y gestionar tus solicitudes de alquiler.</p>
  </body>
</html>
""";
    }

    private static string BuildAppointmentBody(string recipientName, DateTime fechaVisita, string propiedadInteres)
    {
        var displayName = string.IsNullOrWhiteSpace(recipientName) ? "Cliente" : recipientName.Trim();
        var propiedad = string.IsNullOrWhiteSpace(propiedadInteres) ? "Propiedad solicitada" : propiedadInteres.Trim();
        return $"""
<html>
  <body style="font-family:Arial,Helvetica,sans-serif;color:#1f2937;">
    <h2 style="color:#0f172a;">Cita registrada correctamente</h2>
    <p>Hola {WebUtility.HtmlEncode(displayName)}, recibimos tu solicitud de visita.</p>
    <p><strong>Propiedad:</strong> {WebUtility.HtmlEncode(propiedad)}</p>
    <p><strong>Fecha de visita:</strong> {fechaVisita:dd/MM/yyyy HH:mm}</p>
    <p>Te contactaremos para confirmar los detalles.</p>
  </body>
</html>
""";
    }
}

