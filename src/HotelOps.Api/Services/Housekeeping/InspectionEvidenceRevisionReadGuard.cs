using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

// Existing read-only schema guard extracted from the historical conclusion candidate.
public sealed partial class RoomTypeCreationService
{
    private async Task<bool> InspectionEvidenceRevisionInstalled(CancellationToken ct)
        => await db.Database.SqlQuery<int>($"SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.Housekeeping.EvidenceRevision.V1' AND CONVERT(nvarchar(128),value)='E94CD891B0EDE6AD98384B91B35B6DC395E70E53D3CAD994854F4B7237C61CAC') AND (SELECT COUNT(*) FROM sys.triggers WHERE name IN('TR_hk_attachment_revision_v1','TR_hk_defect_files_v1','TR_hk_result_files_v1') AND is_disabled=0)=3 THEN 1 ELSE 0 END AS Value").SingleAsync(ct)==1;
}
