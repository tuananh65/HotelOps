using HotelOps.Api.Compact.Services;
using Xunit;

namespace HotelOps.Compact.Tests;
public class NotificationTextTests
{
    [Fact] public void InternalGuidIsNotNotificationTitle() => Assert.Equal("Phiếu kho đã được ghi sổ thành công",NotificationText.Title("DOCUMENT_POST","5ac4e9973fbf4548a8adabd14a4b938f"));
    [Fact] public void BusinessSubjectRemainsReadable() => Assert.Equal("Công việc mới được phân công: Dọn phòng 305",NotificationText.Title("WORK_ASSIGNED","Dọn phòng 305"));
    [Fact] public void NoRawGuidInMixedSubject() => Assert.Equal("Yêu cầu",NotificationText.Title("REQUEST_WORKFLOW","Yêu cầu 5ac4e997-3fbf-4548-a8ad-abd14a4b938f"));
}
