using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using HotelOps.Api.Compact.Infrastructure;

namespace HotelOps.Api.Compact.Services;

public interface IPasswordResetEmail
{
    bool IsConfigured { get; }
    Task Send(string recipient,string username,string password,DateTime expiresAt,CancellationToken ct,GmailSender? gmail=null);
}

// Request-scoped credentials only; never persist, log or return these values.
public sealed class GmailSender
{
    public string Address { get; }
    public string AppPassword { get; }
    private GmailSender(string address,string password){Address=address;AppPassword=password;}
    public static GmailSender? From(string? address,string? password)
    {
        if(address==null&&password==null)return null;
        var sender=address?.Trim();
        var secret=password?.Replace(" ","").Trim();
        Output.Require(!string.IsNullOrWhiteSpace(sender)&&sender.Length<=254
            &&MailAddress.TryCreate(sender,out var parsed)&&parsed.Address==sender,
            "INVALID_GMAIL_ADDRESS",400);
        Output.Require(secret is {Length:16}&&secret.All(char.IsAsciiLetterOrDigit),"INVALID_GMAIL_APP_PASSWORD",400);
        return new GmailSender(sender!,secret!);
    }
}

public sealed class PasswordResetEmail(IConfiguration configuration) : IPasswordResetEmail
{
    private IConfigurationSection Settings => configuration.GetSection("PasswordResetEmail");
    public bool IsConfigured => !string.IsNullOrWhiteSpace(Settings["Host"])
        && MailAddress.TryCreate(Settings["From"],out _)
        && int.TryParse(Settings["Port"]??"587",out var port) && port is >0 and <=65535
        && port!=465 && !string.IsNullOrWhiteSpace(Settings["Username"])
        && !string.IsNullOrWhiteSpace(Settings["Password"]);

    public async Task Send(string recipient,string username,string password,DateTime expiresAt,CancellationToken ct,GmailSender? gmail=null)
    {
        Output.Require(gmail!=null||IsConfigured,"EMAIL_NOT_CONFIGURED",503);
        using var message=new MailMessage(new MailAddress(gmail?.Address??Settings["From"]!,"HotelOps"),new MailAddress(recipient)){
            Subject="HotelOps — Mật khẩu đăng nhập tạm thời",
            Body=$"Tài khoản {username} vừa được quản trị viên đặt lại mật khẩu.\n\nTên đăng nhập: {username}\nMật khẩu tạm: {password}\nHết hạn: {expiresAt:yyyy-MM-dd HH:mm} UTC (24 giờ).\n\nĐăng nhập HotelOps và đổi mật khẩu ngay. Không chia sẻ email này. Nếu bạn không yêu cầu, hãy liên hệ quản trị viên.",
            BodyEncoding=Encoding.UTF8,SubjectEncoding=Encoding.UTF8,IsBodyHtml=false
        };
        using var client=new SmtpClient(gmail!=null?"smtp.gmail.com":Settings["Host"],gmail!=null?587:int.Parse(Settings["Port"]??"587")){
            EnableSsl=true,UseDefaultCredentials=false,
            Credentials=new NetworkCredential(gmail?.Address??Settings["Username"],gmail?.AppPassword??Settings["Password"])
        };
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        try { await client.SendMailAsync(message,timeout.Token); }
        catch(SmtpException){throw new ApiException(502,"PASSWORD_EMAIL_FAILED");}
        catch(OperationCanceledException) when(!ct.IsCancellationRequested){throw new ApiException(504,"PASSWORD_EMAIL_TIMEOUT");}
    }
}

public static class TemporaryAccountPassword
{
    public static string Generate()
    {
        const string alphabet="ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%-_";
        string value;
        do { value=string.Create(24,alphabet,static(span,chars)=>{
            for(var i=0;i<span.Length;i++)span[i]=chars[RandomNumberGenerator.GetInt32(chars.Length)];
        }); } while(!value.Any(char.IsUpper)||!value.Any(char.IsLower)||!value.Any(char.IsDigit)||!value.Any(c=>"!@#$%-_".Contains(c)));
        return value;
    }
}
