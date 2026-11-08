using HotelOps.Api.Models;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
namespace HotelOps.Api.Data;

public partial class HotelOpsDbContext
{
    public DbSet<NotificationOperationCandidate> CandidateOperations => Set<NotificationOperationCandidate>();
    public DbSet<NotificationPublicationCandidate> CandidatePublications => Set<NotificationPublicationCandidate>();
    public DbSet<NotificationIntentCandidate> CandidateIntents => Set<NotificationIntentCandidate>();
    private static void ConfigureNotificationCandidate(ModelBuilder b)
    {
        var op = b.Entity<NotificationOperationCandidate>();
        op.ToTable("notification_operation_reservations", t => t.UseSqlOutputClause(false)); op.HasKey(x => new { x.ActorUserId, x.OperationKey });
        op.Property(x => x.Fingerprint).HasColumnType("binary(32)"); op.Property(x => x.RowVersion).IsRowVersion();
        op.HasOne<User>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.NoAction);
        var pub = b.Entity<NotificationPublicationCandidate>();
        pub.ToTable("notification_publications", t => t.UseSqlOutputClause(false)); pub.HasKey(x => x.PublicationId); pub.Property(x => x.PublicationId).ValueGeneratedNever();
        pub.Property(x => x.EventType).HasMaxLength(60).IsUnicode(false); pub.Property(x => x.SourceRevision).HasColumnType("binary(8)");
        pub.HasIndex(x => x.AssignmentId).IsUnique();
        pub.HasIndex(x => new { x.ActorUserId, x.OperationKey }).IsUnique();
        pub.HasOne<NotificationOperationCandidate>().WithMany().HasForeignKey(x => new { x.ActorUserId, x.OperationKey }).OnDelete(DeleteBehavior.NoAction);
        pub.HasOne<CleaningAssignment>().WithMany().HasForeignKey(x => x.AssignmentId).OnDelete(DeleteBehavior.NoAction);
        var intent = b.Entity<NotificationIntentCandidate>();
        intent.ToTable("notification_delivery_intents"); intent.HasKey(x => x.IntentId); intent.Property(x => x.IntentId).ValueGeneratedOnAdd();
        intent.Property(x => x.RowVersion).IsRowVersion(); intent.Property(x => x.DeliveryChannel).HasMaxLength(30).IsUnicode(false);
        intent.Property(x => x.Status).HasMaxLength(30).IsUnicode(false); intent.Property(x => x.FailureCode).HasMaxLength(60).IsUnicode(false);
        intent.HasIndex(x => new { x.PublicationId, x.UserId, x.DeliveryChannel }).IsUnique();
        intent.HasOne<NotificationPublicationCandidate>().WithMany().HasForeignKey(x => x.PublicationId).OnDelete(DeleteBehavior.NoAction);
        intent.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
        var link = b.Entity<CandidateInboxLink>(); link.ToView("notification_recipients"); link.HasKey(x => new { x.NotificationId, x.UserId, x.DeliveryChannel });
        link.Property(x => x.DeliveryChannel).HasMaxLength(30).IsUnicode(false);
        intent.HasOne<CandidateInboxLink>().WithMany().HasForeignKey(x => new { x.InboxNotificationId, x.UserId, x.DeliveryChannel }).OnDelete(DeleteBehavior.NoAction);
        foreach (var type in new[] { typeof(NotificationOperationCandidate), typeof(NotificationPublicationCandidate), typeof(NotificationIntentCandidate), typeof(CandidateInboxLink) })
            foreach (var property in b.Entity(type).Metadata.GetProperties()) property.SetColumnName(Regex.Replace(property.Name, "([a-z0-9])([A-Z])", "$1_$2").ToLowerInvariant());
    }
}