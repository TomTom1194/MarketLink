using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace MarketLink.Services
{
    // Sends email through an SMTP server (Gmail by default)
    public class EmailService : IEmailService
    {
        private readonly EmailSettings _settings;

        public EmailService(IOptions<EmailSettings> settings)
        {
            _settings = settings.Value;
        }

        public async Task SendAsync(string toEmail, string toName, string subject, string htmlBody)
        {
            if (string.IsNullOrWhiteSpace(_settings.UserName) || string.IsNullOrWhiteSpace(_settings.Password))
            {
                throw new InvalidOperationException("Email is not set up. Fill in EmailSettings:UserName and EmailSettings:Password.");
            }

            string fromEmail = string.IsNullOrWhiteSpace(_settings.FromEmail) ? _settings.UserName : _settings.FromEmail;

            using var message = new MailMessage
            {
                From = new MailAddress(fromEmail, _settings.FromName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };
            message.To.Add(new MailAddress(toEmail, toName));

            using var client = new SmtpClient(_settings.Host, _settings.Port)
            {
                EnableSsl = _settings.EnableSsl,
                Credentials = new NetworkCredential(_settings.UserName, _settings.Password)
            };

            await client.SendMailAsync(message);
        }
    }
}
