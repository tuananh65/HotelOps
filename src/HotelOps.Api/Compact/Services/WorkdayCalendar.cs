using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Infrastructure;

namespace HotelOps.Api.Compact.Services;

// Calendar markers only. Enterprise Tet choices, substitute days and leave are not inferred.
public static class WorkdayCalendar
{
    public const string AnnualSource = "https://xaydungchinhsach.chinhphu.vn/lich-nghi-tet-nghi-le-2027-119261115134738791.htm";
    public const string TetSource = "https://xaydungchinhsach.chinhphu.vn/de-xuat-2-phuong-an-nghi-tet-nguyen-dan-2027-tet-dinh-mui-11926080513033257.htm";
    public const string CultureSource = "https://xaydungchinhsach.chinhphu.vn/ngay-24-11-hang-nam-la-ngay-van-hoa-viet-nam-nguoi-lao-dong-duoc-nghi-huong-nguyen-luong-119260113152642414.htm";

    public static WorkdayCalendarMarker[] Markers(int year)
    {
        Output.Require(year is >= 2000 and <= 2100, "INVALID_WORKDAY_DATE", 400);
        var markers = new List<WorkdayCalendarMarker>
        {
            new(new(year, 1, 1), "Tết Dương lịch", "HOLIDAY", AnnualSource),
            new(new(year, 4, 30), "Ngày Chiến thắng", "HOLIDAY", AnnualSource),
            new(new(year, 5, 1), "Quốc tế Lao động", "HOLIDAY", AnnualSource),
            new(new(year, 9, 2), "Quốc khánh", "HOLIDAY", TetSource),
            new(new(year, 9, 1), "Ngày liền kề Quốc khánh — theo lịch đơn vị", "REVIEW", TetSource),
            new(new(year, 9, 3), "Ngày liền kề Quốc khánh — theo lịch đơn vị", "REVIEW", TetSource),
        };
        if (year >= 2026) markers.Add(new(new(year, 11, 24), "Ngày Văn hóa Việt Nam", "HOLIDAY", CultureSource));
        if (year >= 2027) markers.Add(new(new(year, 7, 27), "Ngày Tổ quốc tri ân — đối chiếu lịch đơn vị", "REVIEW", AnnualSource));
        // Verified annual anchors; do not extrapolate a lunar conversion or public-sector leave to hotel staff.
        if (year is 2026 or 2027)
        {
            var tet = year == 2026 ? new DateOnly(2026, 2, 17) : new DateOnly(2027, 2, 6);
            var source = year == 2026 ? "https://baochinhphu.vn/bo-noi-vu-thong-bao-lich-nghi-tet-am-lich-va-nghi-le-quoc-khanh-nam-2026-102251017095507785.htm" : TetSource;
            for (var offset = -3; offset <= 3; offset++)
                markers.Add(new(tet.AddDays(offset), offset == 0 ? "Mùng 1 Tết Nguyên đán" : "Dịp Tết — đối chiếu lịch đơn vị",
                    offset == 0 ? "HOLIDAY" : "REVIEW", source));
            markers.Add(new(new(year, 4, year == 2026 ? 26 : 16), "Giỗ Tổ Hùng Vương (10/3 âm lịch)", "HOLIDAY", year == 2026 ? "https://xaydungchinhsach.chinhphu.vn/lich-nghi-le-gio-to-hung-vuong-30-4-1-5-quoc-khanh-2-9-nam-2026-119260222115822151.htm" : AnnualSource));
        }
        return markers.OrderBy(m => m.Date).ToArray();
    }

    public static string Notice(int year) =>
        "Mốc lịch tham khảo, không tự ghi nhận nghỉ phép hay nghỉ bù. Tết và ngày liền kề Quốc khánh cần đối chiếu lịch của đơn vị; chọn hoặc bỏ từng ngày trước khi lưu."
        + (year is 2026 or 2027 ? "" : $" Chưa xác minh mốc Tết và Giỗ Tổ âm lịch năm {year}; các mốc đang hiển thị chưa phải toàn bộ lịch lễ của năm.");
}

public sealed partial class WorkdayService
{
    public async Task<object> Calendar(long branch, int year, CancellationToken ct)
    {
        await ReadScope(branch, ct);
        var markers = WorkdayCalendar.Markers(year).ToList();
        // Weekly views may cross a year boundary.
        if (year > 2000) markers.AddRange(WorkdayCalendar.Markers(year - 1).Where(m => m.Date.Month == 12));
        if (year < 2100) markers.AddRange(WorkdayCalendar.Markers(year + 1).Where(m => m.Date.Month == 1));
        return new { markers, notice = WorkdayCalendar.Notice(year) };
    }
}
