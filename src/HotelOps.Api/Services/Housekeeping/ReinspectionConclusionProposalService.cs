using System.Text.Json;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Services.Authentication;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

public sealed partial class RoomTypeCreationService
{
    // A persisted review proposal, NOT a registered issuer, N01 receipt or operational conclusion.
    internal Task<RoomTypeBatchResult> PrepareReinspectionConclusionAsync(SessionReference ticket,long sid,long branch,long inspection,
        string expected,ReinspectionConclusionProposalRequest input,CancellationToken ct)
        =>ReinspectionWorkflowBatch(ticket,sid,branch,token=>PersistReinspectionProposal(ticket,sid,branch,inspection,expected,input,token),ct);

    internal Task<RoomTypeBatchResult> ReadReinspectionConclusionAsync(SessionReference ticket,long sid,long branch,long inspection,long rework,CancellationToken ct)
        =>ReinspectionWorkflowBatch(ticket,sid,branch,async token=>{
            await RequireProposalSchema(token);
            var details=(ReinspectionWorkflowDetails)(await WorkflowDetails(ticket,sid,branch,inspection,rework,token)).Value!;
            var proposal=await ReadProposal(inspection,token);
            return new(RoomTypeBatchStatus.Success,new ReinspectionConclusionProposalRead(proposal==null?null:ValidateProposal(proposal,details,ticket.UserId,branch,false)),details.InspectionRevision);
        },ct);

    private async Task<RoomTypeBatchResult> PersistReinspectionProposal(SessionReference ticket,long sid,long branch,long inspection,string expected,
        ReinspectionConclusionProposalRequest input,CancellationToken ct)
    {
        await RequireProposalSchema(ct);
        if(input.OperationId==Guid.Empty||!input.Confirmed||input.SelectedDefectIds==null||!WorkflowStrongTag(expected)
            ||!WorkflowStrongTag(input.BindingRevision)||!WorkflowStrongTag(input.ReworkRevision))
            throw new InvalidOperationException("REINSPECTION_PROPOSAL_INPUT_INVALID");
        // Real session/branch/ownership/cycle/version/SOD revalidation precedes any replay read.
        var source=(ReinspectionWorkflowDetails)(await WorkflowDetails(ticket,sid,branch,inspection,input.ReworkId,ct)).Value!;
        var selected=input.SelectedDefectIds.Order().ToArray();
        if(!selected.SequenceEqual(source.Defects.Select(d=>d.DefectId).Order()))
            throw new InvalidOperationException("REINSPECTION_EXACT_COMPLETE_DECISIONS_REQUIRED");
        var canonical=JsonSerializer.Serialize(new{scope="UC047_REVIEW_PROPOSAL_V1",actor=ticket.UserId,branch,inspection,rework=input.ReworkId,
            inspectionRevision=expected,reworkRevision=input.ReworkRevision,bindingRevision=input.BindingRevision,selected});
        var used=await db.Database.SqlQuery<long>($"SELECT inspection_id AS Value FROM dbo.fe05_reinspection_conclusion_proposals_v1 WITH(UPDLOCK,HOLDLOCK) WHERE operation_id={input.OperationId}").SingleOrDefaultAsync(ct);
        if(used!=0&&used!=inspection)throw new InvalidOperationException("REINSPECTION_OPERATION_CONFLICT");
        var prior=await ReadProposal(inspection,ct);
        if(prior!=null){
            if(prior.OperationId!=input.OperationId||prior.Canonical!=canonical)throw new InvalidOperationException("REINSPECTION_OPERATION_CONFLICT");
            var replay=ValidateProposal(prior,source,ticket.UserId,branch,true);
            return new(RoomTypeBatchStatus.Success,replay,source.InspectionRevision);
        }
        if(source.InspectionRevision!=expected||source.BindingRevision!=input.BindingRevision||source.ReworkRevision!=input.ReworkRevision)
            throw new InvalidOperationException("REINSPECTION_CONCLUSION_STALE");
        if(source.Defects.Count==0||source.Defects.Any(d=>d.Fixed==null||d.RecordedBy!=ticket.UserId||d.RecordedAt==null))
            throw new InvalidOperationException("REINSPECTION_EXACT_COMPLETE_DECISIONS_REQUIRED");
        var decisions=ProposalDecisions(source);
        var proposed=source.Defects.All(d=>d.Fixed==true)?"COMPLETED":"FAILED";
        // Existing root and binding rowversions, in the SAME caller BatchAsync transaction.
        // No Inspection/Rework/task/room operational status is changed.
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.room_inspections SET summary_note=summary_note WHERE inspection_id={inspection} AND row_version={Convert.FromHexString(expected.Trim('"'))}; IF @@ROWCOUNT<>1 THROW 51997,'Proposal source changed',1; UPDATE dbo.fe05_reinspection_bindings_v1 SET touched_at=SYSUTCDATETIME() WHERE inspection_id={inspection}",ct);
        var post=(ReinspectionWorkflowDetails)(await WorkflowDetails(ticket,sid,branch,inspection,input.ReworkId,ct)).Value!;
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT dbo.fe05_reinspection_conclusion_proposals_v1(inspection_id,rework_id,operation_id,branch_id,actor_user_id,
              request_canonical,decisions_json,proposed_rework_state,inspection_revision,binding_revision,rework_revision,created_at)
            VALUES({inspection},{input.ReworkId},{input.OperationId},{branch},{ticket.UserId},{canonical},{decisions},{proposed},
              {Convert.FromHexString(post.InspectionRevision.Trim('"'))},{Convert.FromHexString(post.BindingRevision.Trim('"'))},
              {Convert.FromHexString(post.ReworkRevision.Trim('"'))},SYSUTCDATETIME())
            """,ct);
        await CandidateAudit("CREATE",ticket.UserId,branch,inspection,JsonSerializer.Serialize(new{input.OperationId,proposedReworkState=proposed,proposalOnly=true,selected}),ct);
        var result=ValidateProposal((await ReadProposal(inspection,ct))!,post,ticket.UserId,branch,false);
        return new(RoomTypeBatchStatus.Success,result,post.InspectionRevision);
    }
    private static string ProposalDecisions(ReinspectionWorkflowDetails source)=>JsonSerializer.Serialize(source.Defects.OrderBy(d=>d.DefectId).Select(d=>new{defectId=d.DefectId,fixedResult=d.Fixed,actor=d.RecordedBy,at=d.RecordedAt}));
    private static ReinspectionConclusionProposal ValidateProposal(ProposalRow p,ReinspectionWorkflowDetails source,long actor,long branch,bool replay)
    {
        if(p.InspectionId!=source.InspectionId||p.ReworkId!=source.ReworkId||p.ActorId!=actor||p.BranchId!=branch
            ||WorkflowTag(p.InspectionRevision)!=source.InspectionRevision||WorkflowTag(p.BindingRevision)!=source.BindingRevision
            ||WorkflowTag(p.ReworkRevision)!=source.ReworkRevision||p.Decisions!=ProposalDecisions(source)
            ||p.Proposed!=(source.Defects.All(d=>d.Fixed==true)?"COMPLETED":"FAILED"))
            throw new InvalidOperationException("REINSPECTION_CONCLUSION_STALE");
        return new(p.OperationId,source.InspectionId,source.ReworkId,actor,source.OriginalChecklistVersionId,source.AssignedChecklistVersionId,p.Proposed,
            source.InspectionRevision,source.ReworkRevision,source.BindingRevision,WorkflowTag(p.ProposalRevision),source.Defects.Where(d=>d.Fixed==false).Select(d=>d.DefectId).Order().ToArray(),p.CreatedAt,replay,false,false);
    }
    private async Task RequireProposalSchema(CancellationToken ct)
    {
        await RequireReinspectionComponentSchemaAsync(ct);
        if(await db.Database.SqlQuery<int>($"SELECT CASE WHEN OBJECT_ID('dbo.fe05_reinspection_conclusion_proposals_v1') IS NOT NULL AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.FE05.ReinspectionConclusionProposal' AND CONVERT(nvarchar(128),value)='FAST016_NOT_ACCEPTED') THEN 1 ELSE 0 END AS Value").SingleAsync(ct)!=1)
            throw new InvalidOperationException("REINSPECTION_PROPOSAL_SCHEMA_REQUIRED");
    }
    private async Task<ProposalRow?> ReadProposal(long inspection,CancellationToken ct)=>(await db.Database.SqlQuery<ProposalRow>($"""
        SELECT inspection_id AS InspectionId,rework_id AS ReworkId,operation_id AS OperationId,branch_id AS BranchId,actor_user_id AS ActorId,
          request_canonical AS Canonical,decisions_json AS Decisions,proposed_rework_state AS Proposed,inspection_revision AS InspectionRevision,
          binding_revision AS BindingRevision,rework_revision AS ReworkRevision,created_at AS CreatedAt,proposal_revision AS ProposalRevision
        FROM dbo.fe05_reinspection_conclusion_proposals_v1 WITH(UPDLOCK,HOLDLOCK) WHERE inspection_id={inspection}
        """).ToArrayAsync(ct)).SingleOrDefault();
    private async Task RequireUnreviewedProposalSource(SessionReference ticket,long sid,long branch,long inspection,long rework,CancellationToken ct)
    {
        if(await db.Database.SqlQuery<int>($"SELECT CASE WHEN OBJECT_ID('dbo.fe05_reinspection_conclusion_proposals_v1') IS NOT NULL THEN 1 ELSE 0 END AS Value").SingleAsync(ct)==0)return;
        await WorkflowDetails(ticket,sid,branch,inspection,rework,ct);
        if(await ReadProposal(inspection,ct)!=null)throw new InvalidOperationException("REINSPECTION_RESULTS_ALREADY_REVIEWED");
    }
    private sealed class ProposalRow
    {
        public long InspectionId{get;set;}public long ReworkId{get;set;}public Guid OperationId{get;set;}public long BranchId{get;set;}public long ActorId{get;set;}
        public string Canonical{get;set;}="";public string Decisions{get;set;}="";public string Proposed{get;set;}="";
        public byte[] InspectionRevision{get;set;}=[];public byte[] BindingRevision{get;set;}=[];public byte[] ReworkRevision{get;set;}=[];public byte[] ProposalRevision{get;set;}=[];public DateTime CreatedAt{get;set;}
    }
}
