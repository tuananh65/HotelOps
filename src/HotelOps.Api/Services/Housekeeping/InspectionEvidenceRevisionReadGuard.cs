using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

// The pinned amendment has two byte hashes solely because Git LF and Windows CRLF differ.
// Both identify the same reviewed SQL; arbitrary 64-character markers remain rejected.
public sealed partial class RoomTypeCreationService
{
    private async Task<bool> InspectionEvidenceRevisionInstalled(CancellationToken ct)
        => await db.Database.SqlQuery<int>($"SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.Housekeeping.EvidenceRevision.V1' AND CONVERT(nvarchar(128),value) IN ('E94CD891B0EDE6AD98384B91B35B6DC395E70E53D3CAD994854F4B7237C61CAC','314DBD336CED88E2DCFDCF6762F8126BFA95D349247ACADA7294F83F19B1F7AD')) AND (SELECT COUNT(*) FROM sys.triggers WHERE name IN('TR_hk_attachment_revision_v1','TR_hk_defect_files_v1','TR_hk_result_files_v1') AND is_disabled=0)=3 THEN 1 ELSE 0 END AS Value").SingleAsync(ct)==1;
}
