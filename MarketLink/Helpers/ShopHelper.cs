using MarketLink.Models;

namespace MarketLink.Helpers
{
    public static class ShopHelper
    {
        public static string Money(decimal amount)
        {
            return "$" + amount.ToString("N2", System.Globalization.CultureInfo.InvariantCulture);
        }

        public static string ItemStatus(StockPrice sp)
        {
            if (sp.EffectiveTo != null)
            {
                return "unavailable";
            }
            if (sp.Product == null || sp.Product.Status != "active" || sp.Product.ExpiresAt <= DateTime.Now)
            {
                return "unavailable";
            }
            if (sp.Stall == null || !sp.Stall.IsActive || sp.Stall.Farmer == null || sp.Stall.Farmer.ApprovalStatus != "approved")
            {
                return "unavailable";
            }
            if (sp.QuantityAvailable < 1)
            {
                return "sold_out";
            }
            return "ok";
        }

        public static string Qty(decimal quantity)
        {
            return quantity.ToString("0.##");
        }

        public static string DayNames(string dayList)
        {
            var result = new List<string>();
            foreach (string s in dayList.Split(','))
            {
                string dayNumber = s.Trim();
                if (dayNumber == "2") result.Add("Mon");
                else if (dayNumber == "3") result.Add("Tue");
                else if (dayNumber == "4") result.Add("Wed");
                else if (dayNumber == "5") result.Add("Thu");
                else if (dayNumber == "6") result.Add("Fri");
                else if (dayNumber == "7") result.Add("Sat");
                else if (dayNumber == "8" || dayNumber == "1" || dayNumber == "CN") result.Add("Sun");
            }
            return string.Join(", ", result);
        }

        public static string Distance(double? km)
        {
            if (km == null)
            {
                return "";
            }
            if (km.Value < 1)
            {
                return Math.Round(km.Value * 1000) + " m";
            }
            return km.Value.ToString("0.0") + " km";
        }

        public static bool CoordinatesFromMapUrl(string? mapUrl, out double lat, out double lng)
        {
            lat = 0;
            lng = 0;
            if (string.IsNullOrEmpty(mapUrl))
            {
                return false;
            }

            string latText = "";
            string lngText = "";
            int latIndex = mapUrl.IndexOf("!3d");
            int lngIndex = mapUrl.IndexOf("!4d");
            int atIndex = mapUrl.IndexOf("/@");
            if (latIndex >= 0 && lngIndex > latIndex)
            {
                latText = mapUrl.Substring(latIndex + 3, lngIndex - latIndex - 3);
                lngText = mapUrl.Substring(lngIndex + 3);
                int endIndex = lngText.IndexOfAny(new char[] { '!', '?', '/' });
                if (endIndex >= 0)
                {
                    lngText = lngText.Substring(0, endIndex);
                }
            }
            else if (atIndex >= 0)
            {
                string[] parts = mapUrl.Substring(atIndex + 2).Split(',');
                if (parts.Length >= 2)
                {
                    latText = parts[0];
                    lngText = parts[1];
                }
            }

            var culture = System.Globalization.CultureInfo.InvariantCulture;
            if (!double.TryParse(latText, System.Globalization.NumberStyles.Float, culture, out lat)
                || !double.TryParse(lngText, System.Globalization.NumberStyles.Float, culture, out lng))
            {
                lat = 0;
                lng = 0;
                return false;
            }
            return lat >= -90 && lat <= 90 && lng >= -180 && lng <= 180;
        }

        public static string Hours(TimeSpan start, TimeSpan end)
        {
            return start.ToString(@"hh\:mm") + " – " + end.ToString(@"hh\:mm");
        }

        public static string StatusText(string status)
        {
            if (status == "placed") return "Placed – waiting for farmer";
            if (status == "accepted") return "Accepted – ready for pickup";
            if (status == "rejected") return "Rejected by farmer";
            if (status == "cancelled") return "Cancelled";
            if (status == "completed") return "Picked up";
            if (status == "no_show") return "Not picked up";
            return status;
        }

        public static string StatusClass(string status)
        {
            if (status == "placed") return "badge-yellow";
            if (status == "accepted") return "badge-green";
            if (status == "completed") return "badge-dark";
            return "badge-red";
        }

        public static string AddressFromMapUrl(string? mapUrl)
        {
            if (string.IsNullOrEmpty(mapUrl))
            {
                return "";
            }

            string text = "";
            int placeIndex = mapUrl.IndexOf("/place/");
            int queryIndex = mapUrl.IndexOf("query=");
            if (placeIndex >= 0)
            {
                text = mapUrl.Substring(placeIndex + 7);
                int slashIndex = text.IndexOf('/');
                if (slashIndex >= 0)
                {
                    text = text.Substring(0, slashIndex);
                }
            }
            else if (queryIndex >= 0)
            {
                text = mapUrl.Substring(queryIndex + 6);
                int andIndex = text.IndexOf('&');
                if (andIndex >= 0)
                {
                    text = text.Substring(0, andIndex);
                }
            }

            try
            {
                return Uri.UnescapeDataString(text.Replace("+", " ")).Trim();
            }
            catch
            {
                return "";
            }
        }

        public static string MarketAddress(Market market)
        {
            string address = AddressFromMapUrl(market.MapUrl);
            if (address != "")
            {
                return address;
            }
            if (market.District != null)
            {
                return market.District.DistrictName;
            }
            return "";
        }

        public static string MapLink(Stall stall, Market market)
        {
            double lat;
            double lng;
            if (CoordinatesFromMapUrl(stall.GoogleUrl, out lat, out lng) || CoordinatesFromMapUrl(market.MapUrl, out lat, out lng))
            {
                var culture = System.Globalization.CultureInfo.InvariantCulture;
                return "https://www.google.com/maps/dir/?api=1&destination=" + lat.ToString(culture) + "," + lng.ToString(culture);
            }
            if (!string.IsNullOrEmpty(stall.GoogleUrl))
            {
                return stall.GoogleUrl;
            }
            return market.MapUrl;
        }
    }
}
