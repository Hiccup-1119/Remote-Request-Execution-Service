using System.Text.RegularExpressions;

namespace RreService.Security;

public static class Redaction
{
    static readonly Regex Tokenish = new("(bearer\s+[a-z0-9\-\._~\+\/]+=*)", RegexOptions.IgnoreCase|RegexOptions.Compiled);

    public static string Mask(string s)
        => Tokenish.Replace(s, _ => "bearer ***redacted***");
}