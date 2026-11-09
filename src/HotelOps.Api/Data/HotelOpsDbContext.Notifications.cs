using HotelOps.Api.Entities.Notifications;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Data;

public partial class HotelOpsDbContext
{
    public DbSet<NotificationIntent> NotificationIntents => Set<NotificationIntent>();
    private static void ConfigureNotifications(ModelBuilder modelBuilder)
    {
        var e = modelBuilder.Entity<NotificationIntent>();
        e.ToTable("notification_intents_c04", "dbo");
        e.HasKey(x => x.IntentId);
        e.Property(x => x.EventType).HasMaxLength(60).IsUnicode(false);
        e.Property(x => x.ReadPolicy).HasMaxLength(100).IsUnicode(false);
        e.Property(x => x.Title).HasMaxLength(200);
        e.Property(x => x.Message).HasMaxLength(2000);
        e.Property(x => x.ActionPath).HasMaxLength(200).IsUnicode(false);
        e.Property(x => x.State).HasMaxLength(12).IsUnicode(false);
        e.Property(x => x.FailureCode).HasMaxLength(60).IsUnicode(false);
        e.Property(x => x.PayloadHash).HasColumnType("binary(32)");
        e.Property(x => x.RowVersion).IsRowVersion();
        e.HasIndex(x => new { x.EventType, x.EventVersion, x.OccurrenceId, x.RecipientUserId }).IsUnique();
        e.HasIndex(x => new { x.State, x.NextAttemptUtc, x.IntentId });
        e.HasIndex(x => new { x.RecipientUserId, x.State, x.CreatedUtc });
        e.HasOne<Entities.User>().WithMany().HasForeignKey(x => x.RecipientUserId).OnDelete(DeleteBehavior.NoAction);
        e.HasOne<Entities.Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.NoAction);
        e.HasOne<Entities.Notification>().WithMany().HasForeignKey(x => x.NotificationId).OnDelete(DeleteBehavior.NoAction);
    }
}
