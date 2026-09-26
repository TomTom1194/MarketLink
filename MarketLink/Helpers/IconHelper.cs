using Microsoft.AspNetCore.Html;

namespace MarketLink.Helpers
{
    public static class IconHelper
    {
        private static readonly Dictionary<string, string> Paths = new Dictionary<string, string>
        {
            { "pin", "<path d=\"M12 21s-7-6.2-7-11.5a7 7 0 0 1 14 0C19 14.8 12 21 12 21z\"></path><circle cx=\"12\" cy=\"9.5\" r=\"2.5\"></circle>" },
            { "calendar", "<rect x=\"3\" y=\"4\" width=\"18\" height=\"17\" rx=\"2\"></rect><path d=\"M16 2v4M8 2v4M3 10h18\"></path>" },
            { "store", "<path d=\"M3 9l1.5-5h15L21 9\"></path><path d=\"M4 9v11h16V9\"></path><path d=\"M3 9h18\"></path><path d=\"M9 20v-6h6v6\"></path>" },
            { "basket", "<path d=\"M3 10h18l-2 9.2a2 2 0 0 1-2 1.8H7a2 2 0 0 1-2-1.8z\"></path><path d=\"m8 10 4-6 4 6\"></path>" },
            { "cash", "<rect x=\"2\" y=\"6\" width=\"20\" height=\"12\" rx=\"2\"></rect><circle cx=\"12\" cy=\"12\" r=\"2.5\"></circle><path d=\"M6 12h.01M18 12h.01\"></path>" },
            { "bell", "<path d=\"M6 8a6 6 0 0 1 12 0c0 7 3 9 3 9H3s3-2 3-9\"></path><path d=\"M10.3 21a1.94 1.94 0 0 0 3.4 0\"></path>" },
            { "check", "<path d=\"M20 6 9 17l-5-5\"></path>" },
            { "x", "<path d=\"M18 6 6 18M6 6l12 12\"></path>" },
            { "trash", "<path d=\"M3 6h18\"></path><path d=\"M8 6V4h8v2\"></path><path d=\"M19 6l-1 14H6L5 6\"></path>" },
            { "phone", "<path d=\"M22 16.9v3a2 2 0 0 1-2.2 2 19.8 19.8 0 0 1-8.6-3.1 19.5 19.5 0 0 1-6-6A19.8 19.8 0 0 1 2.1 4.2 2 2 0 0 1 4.1 2h3a2 2 0 0 1 2 1.7c.1.9.4 1.8.7 2.7a2 2 0 0 1-.5 2.1L8 9.8a16 16 0 0 0 6 6l1.3-1.3a2 2 0 0 1 2.1-.4c.9.3 1.8.6 2.7.7a2 2 0 0 1 1.7 2z\"></path>" },
            { "heart", "<path d=\"M12 20s-7.5-4.6-7.5-10A4.5 4.5 0 0 1 12 7a4.5 4.5 0 0 1 7.5 3c0 5.4-7.5 10-7.5 10z\"></path>" },
            { "chevron-down", "<path d=\"m6 9 6 6 6-6\"></path>" },
            { "chevron-left", "<path d=\"m15 18-6-6 6-6\"></path>" },
            { "chevron-right", "<path d=\"m9 18 6-6-6-6\"></path>" },
            { "logout", "<path d=\"M15 4h3a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2h-3\"></path><path d=\"M10 16l-4-4 4-4\"></path><path d=\"M6 12h10\"></path>" },
            { "alert", "<path d=\"M10.3 3.9 1.8 18a2 2 0 0 0 1.7 3h17a2 2 0 0 0 1.7-3L13.7 3.9a2 2 0 0 0-3.4 0z\"></path><path d=\"M12 9v4M12 17h.01\"></path>" },
            { "clock", "<circle cx=\"12\" cy=\"12\" r=\"9\"></circle><path d=\"M12 7v5l3 2\"></path>" },
            { "star", "<path d=\"m12 3 2.8 5.7 6.2.9-4.5 4.4 1 6.2L12 17.3 6.5 20.2l1-6.2L3 9.6l6.2-.9z\"></path>" },
            { "dot", "<circle cx=\"12\" cy=\"12\" r=\"5\"></circle>" }
        };

        public static IHtmlContent Icon(string name, int size = 18, bool filled = false)
        {
            string path = "";
            if (Paths.ContainsKey(name))
            {
                path = Paths[name];
            }
            string fill = filled ? "currentColor" : "none";
            string svg = "<svg class=\"ic\" width=\"" + size + "\" height=\"" + size + "\" viewBox=\"0 0 24 24\" fill=\"" + fill
                + "\" stroke=\"currentColor\" stroke-width=\"2.2\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\">"
                + path + "</svg>";
            return new HtmlString(svg);
        }
    }
}
