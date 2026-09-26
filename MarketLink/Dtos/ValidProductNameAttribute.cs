using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace MarketLink.Dtos
{
    // Checked on the server so Vietnamese capital letters are handled correctly.
    public class ValidProductNameAttribute : ValidationAttribute
    {
        private static readonly Regex NamePattern = new(@"^\p{Lu}[\p{L}\p{M}\p{N}]*(?:[ '-][\p{L}\p{M}\p{N}]+)*$", RegexOptions.Compiled);

        public ValidProductNameAttribute()
        {
            ErrorMessage = "Product name must start with an uppercase letter, contain at least 3 letters, and have no extra spaces";
        }

        public override bool IsValid(object? value)
        {
            return value is not string name || string.IsNullOrWhiteSpace(name) || (name.Count(char.IsLetter) >= 3 && NamePattern.IsMatch(name));
        }
    }
}
