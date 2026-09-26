namespace MarketLink.Services
{
    public interface IEmailService
    {
        // Throws an exception when the email cannot be sent
        Task SendAsync(string toEmail, string toName, string subject, string htmlBody);
    }
}
