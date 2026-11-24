using HotelOps.Api.Compact.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Data;

public partial class HotelOpsCompactDbContext
{
    public DbSet<RequestWorkflow> RequestWorkflows => Set<RequestWorkflow>();
    public DbSet<RequestWorkflowLine> RequestWorkflowLines => Set<RequestWorkflowLine>();
    public DbSet<RequestWorkflowEvent> RequestWorkflowEvents => Set<RequestWorkflowEvent>();

    private static void MapRequests(ModelBuilder model)
    {
        var request=model.Entity<RequestWorkflow>();
        request.ToTable("request_workflows");request.HasKey(x=>x.WorkId);request.Property(x=>x.WorkId).ValueGeneratedNever();
        request.Property(x=>x.RequestType).HasMaxLength(50).IsUnicode(false);
        request.Property(x=>x.Status).HasMaxLength(40).IsUnicode(false);request.Property(x=>x.RowVersion).IsRowVersion();
        var line=model.Entity<RequestWorkflowLine>();line.ToTable("request_workflow_lines");line.HasKey(x=>x.RequestLineId);
        var history=model.Entity<RequestWorkflowEvent>();history.ToTable("request_workflow_events");history.HasKey(x=>x.RequestEventId);
        history.Property(x=>x.Action).HasMaxLength(40).IsUnicode(false);
        history.Property(x=>x.PreviousStatus).HasMaxLength(40).IsUnicode(false);
        history.Property(x=>x.NewStatus).HasMaxLength(40).IsUnicode(false);
        history.Property(x=>x.PayloadHash).HasMaxLength(64).IsUnicode(false);
        history.Property(x=>x.Reason).HasMaxLength(2000);history.Property(x=>x.PreviousVersion).HasMaxLength(8);
        foreach(var type in new[]{typeof(RequestWorkflow),typeof(RequestWorkflowLine),typeof(RequestWorkflowEvent)})
            foreach(var property in model.Entity(type).Metadata.GetProperties()){
                property.SetColumnName(System.Text.RegularExpressions.Regex.Replace(property.Name,"(?<!^)([A-Z])","_$1").ToLowerInvariant());
                if(property.ClrType==typeof(decimal)||property.ClrType==typeof(decimal?)){property.SetPrecision(18);property.SetScale(4);}
            }
    }
}
