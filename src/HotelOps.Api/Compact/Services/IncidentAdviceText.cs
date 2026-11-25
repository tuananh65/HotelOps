using System.Text.RegularExpressions;
namespace HotelOps.Api.Compact.Services;

public static class IncidentAdviceText
{
    public static string Sanitize(string? text,IEnumerable<string> names)
    {
        var value=(text??"")[..Math.Min(text?.Length??0,2000)];
        foreach(var name in names.Where(n=>!string.IsNullOrWhiteSpace(n)).Distinct().OrderByDescending(n=>n.Length))
            value=value.Replace(name,"[đã ẩn]",StringComparison.OrdinalIgnoreCase);
        value=Regex.Replace(value,@"[\w.+-]+@[\w.-]+\.[\p{L}]{2,}","[email]",RegexOptions.None,TimeSpan.FromMilliseconds(100));
        value=Regex.Replace(value,@"(?<!\w)\+?\d[\d\s().-]{7,}\d(?!\w)","[số liên hệ]",RegexOptions.None,TimeSpan.FromMilliseconds(100));
        value=Regex.Replace(value,@"(?i)(phòng|room)\s*[A-Z]?\d+[A-Z]?","phòng [ẩn]",RegexOptions.None,TimeSpan.FromMilliseconds(100));
        return string.Join(' ',value.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries));
    }
    public static bool NeedsSafetyReview(string text)=>new[]{"cháy","khói","rò điện","chập điện","điện giật","rò gas","rò khí","báo cháy"}.Any(word=>text.Contains(word,StringComparison.OrdinalIgnoreCase));
}
