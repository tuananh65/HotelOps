using System.Text.RegularExpressions;

namespace HotelOps.Api.Compact.Services;

public static partial class NotificationText
{
    [GeneratedRegex(@"\b(?:[0-9a-fA-F]{32}|[0-9a-fA-F]{8}(?:-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12})\b")]
    private static partial Regex Identifiers();
    public static string Title(string type,string title)
    {
        var subject=Identifiers().Replace(title,"").Trim(' ','·','-');
        var prefix=type switch {
            "WORK_ASSIGNED"=>"Công việc mới được phân công",
            "WORK_OVERDUE"=>"Công việc đã quá hạn",
            "SCHEDULE_GENERATED"=>"Công việc định kỳ cần phân công",
            "HANDOVER_OFFERED"=>"Đề nghị bàn giao công việc",
            "PENDING_APPROVAL"=>"Yêu cầu vật tư chờ phê duyệt",
            "DOCUMENT_POST"=>"Phiếu kho đã được ghi sổ thành công",
            "DOCUMENT_APPROVE"=>"Phiếu vật tư đã được duyệt",
            "DOCUMENT_REJECT"=>"Phiếu vật tư đã bị từ chối",
            "TRANSFER_SUBMIT"=>"Yêu cầu điều chuyển vật tư chờ phê duyệt",
            "TRANSFER_APPROVE"=>"Yêu cầu điều chuyển đã được duyệt",
            "TRANSFER_REJECT"=>"Yêu cầu điều chuyển đã bị từ chối",
            _ when type.StartsWith("DOCUMENT_")=>"Phiếu vật tư được cập nhật",
            _ when type.StartsWith("TRANSFER_")=>"Điều chuyển vật tư được cập nhật",
            _=>"Thông báo công việc"
        };
        if(subject.Length==0)return prefix;
        var mapped=type is "WORK_ASSIGNED" or "WORK_OVERDUE" or "SCHEDULE_GENERATED" or "HANDOVER_OFFERED" or "PENDING_APPROVAL"
            ||type.StartsWith("DOCUMENT_")||type.StartsWith("TRANSFER_");
        var result=mapped?$"{prefix}: {subject}":subject;
        return result.Length>240?result[..237]+"…":result;
    }
}
