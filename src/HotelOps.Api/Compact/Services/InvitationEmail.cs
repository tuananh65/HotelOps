using System.Net;
using System.Net.Mail;
using System.Text;
namespace HotelOps.Api.Compact.Services;

public interface IInvitationEmail
{
    string? ConfigurationError { get; }
    Task Send(long invitationId,string recipient,string username,string token,DateTime expiresAt,CancellationToken ct);
}
public sealed class SmtpInvitationEmail(IConfiguration config):IInvitationEmail
{
    private IConfigurationSection Settings=>config.GetSection("Email");
    public string? ConfigurationError {
        get {
            if(!Settings.GetValue<bool>("Enabled"))return "BLOCKED_EXTERNAL_CONFIGURATION";
            var host=Settings["SmtpHost"];
            if(string.IsNullOrWhiteSpace(host)||!MailAddress.TryCreate(Settings["FromAddress"],out _)
                ||Settings.GetValue<int>("SmtpPort") is <1 or >65535)return "BLOCKED_EXTERNAL_CONFIGURATION";
            if(!Uri.TryCreate(Settings["PublicBaseUrl"],UriKind.Absolute,out var url)||!(url.Scheme=="https"||(url.Scheme=="http"&&url.IsLoopback)))return "EMAIL_PUBLIC_URL_INVALID";
            if(!Settings.GetValue<bool>("AllowLiveDelivery")&&host is not ("localhost" or "127.0.0.1" or "::1"))return "LIVE_EMAIL_NOT_ENABLED";
            if(Settings.GetValue<bool>("AllowLiveDelivery")&&!Settings.GetValue("UseTls",true))return "EMAIL_TLS_REQUIRED";
            return null;
        }
    }
    public async Task Send(long invitationId,string recipient,string username,string token,DateTime expiresAt,CancellationToken ct)
    {
        if(ConfigurationError!=null)throw new InvalidOperationException(ConfigurationError);
        var url=Settings["PublicBaseUrl"]!.TrimEnd('/')+"/#setup-password?token="+Uri.EscapeDataString(token);
        using var message=new MailMessage(Settings["FromAddress"]!,recipient){Subject="HotelOps — Thiết lập mật khẩu",
            Body=$"HotelOps đã nhận yêu cầu thiết lập mật khẩu cho tài khoản của bạn.\nTên đăng nhập: {username}\n\nThiết lập mật khẩu: {url}\n\nLiên kết chỉ dùng một lần, hết hạn lúc {expiresAt:yyyy-MM-dd HH:mm} UTC.\nSau khi thiết lập, đăng nhập bằng mật khẩu mới. Nếu bạn không yêu cầu hoặc không nhận ra lời mời, hãy bỏ qua email này; mật khẩu hiện tại không thay đổi. Không chia sẻ liên kết này.",
            BodyEncoding=Encoding.UTF8,SubjectEncoding=Encoding.UTF8,IsBodyHtml=false};
        message.Headers.Add("Message-ID",$"<hotelops-invitation-{invitationId}@{new MailAddress(Settings["FromAddress"]!).Host}>");
        using var client=new SmtpClient(Settings["SmtpHost"],Settings.GetValue<int>("SmtpPort")){EnableSsl=Settings.GetValue("UseTls",true),UseDefaultCredentials=false};
        if(!string.IsNullOrWhiteSpace(Settings["Username"]))client.Credentials=new NetworkCredential(Settings["Username"],Settings["Password"]);
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromSeconds(15));
        await client.SendMailAsync(message,timeout.Token);
    }
}
