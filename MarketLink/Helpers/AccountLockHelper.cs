using System.Net;

namespace MarketLink.Helpers
{
    public static class AccountLockHelper
    {
        public const string Subject = "Your MarketLink account has been locked";

        public static string BuildEmail(string name, string reason)
        {
            string safeName = WebUtility.HtmlEncode(name);
            string safeReason = WebUtility.HtmlEncode(reason);

            return $@"
<div style=""font-family:Arial,sans-serif;font-size:15px;color:#1d2620;line-height:1.6"">
  <p>Hello {safeName},</p>
  <p>Your MarketLink account has been <strong>locked</strong>. You have been logged out and cannot log in again.</p>
  <p><strong>Reason:</strong> {safeReason}</p>
  <p>If you think this is a mistake, please reply to this email or contact the MarketLink team.</p>
  <p>MarketLink</p>
</div>";
        }
    }
}
