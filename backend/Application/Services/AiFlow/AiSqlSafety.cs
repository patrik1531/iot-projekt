using System.Text.RegularExpressions;

namespace Application.Services.AiFlow;

public sealed class AiSqlSafety
{
    private static readonly Regex StartsWithSelect = new(@"^\s*select\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex Forbidden = new(@"\b(insert|update|delete|drop|alter|create|truncate|grant|revoke|call|do|execute)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public bool IsSafeSelect(string sql, out string reason)
    {
        reason = "";
        if (string.IsNullOrWhiteSpace(sql)) { reason = "empty"; return false; }
        if (sql.Contains(';')) { reason = "semicolon"; return false; }
        if (!StartsWithSelect.IsMatch(sql)) { reason = "not_select"; return false; }
        if (Forbidden.IsMatch(sql)) { reason = "forbidden_keyword"; return false; }
        return true;
    }
}
