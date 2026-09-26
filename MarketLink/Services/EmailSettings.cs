namespace MarketLink.Services
{
    // Values come from the "EmailSettings" section in appsettings.json / user-secrets
    public class EmailSettings
    {
        public string Host { get; set; } = "smtp.gmail.com";
        public int Port { get; set; } = 587;
        public bool EnableSsl { get; set; } = true;

        // Gmail address and its 16-letter App Password (not the normal Gmail password)
        public string UserName { get; set; } = "";
        public string Password { get; set; } = "";

        // Sender shown to the receiver. Empty FromEmail = use UserName.
        public string FromEmail { get; set; } = "";
        public string FromName { get; set; } = "MarketLink";
    }
}
