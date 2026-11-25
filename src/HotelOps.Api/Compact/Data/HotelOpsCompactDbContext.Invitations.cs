using HotelOps.Api.Compact.Entities;
using Microsoft.EntityFrameworkCore;
namespace HotelOps.Api.Compact.Data;
public partial class HotelOpsCompactDbContext
{
    public DbSet<AccountInvitation> AccountInvitations => Set<AccountInvitation>();
    private static void MapInvitations(ModelBuilder model)
    {
        var e=model.Entity<AccountInvitation>();e.ToTable("account_invitations");e.HasKey(x=>x.InvitationId);
        e.Property(x=>x.RowVersion).IsRowVersion();e.Property(x=>x.Recipient).HasMaxLength(254);
        e.Property(x=>x.TokenHash).HasMaxLength(32);e.Property(x=>x.PasswordFingerprint).HasMaxLength(32);
        e.Property(x=>x.ProtectedToken).HasMaxLength(2000);e.Property(x=>x.DeliveryStatus).HasMaxLength(20).IsUnicode(false);
        e.Property(x=>x.LastError).HasMaxLength(100).IsUnicode(false);
        foreach(var p in e.Metadata.GetProperties())p.SetColumnName(System.Text.RegularExpressions.Regex.Replace(p.Name,"(?<!^)([A-Z])","_$1").ToLowerInvariant());
    }
}
