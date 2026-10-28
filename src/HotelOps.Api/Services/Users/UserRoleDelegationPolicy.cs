namespace HotelOps.Api.Services.Users;

// Exact permission/scope envelopes approved for Task44 technical implementation; no catalog mutation.
internal static class UserRoleDelegationPolicy
{
    private static readonly IReadOnlyDictionary<string, (string Department, string[] Grants)> Roles =
        new Dictionary<string, (string, string[])>(StringComparer.Ordinal)
        {
            ["HK_STAFF"] = ("HK", [ "ATTENDANCE_RECORD/OWN", "CLEANING_EXECUTE/OWN", "CLEANING_VIEW/OWN", "DEFECT_RESOLVE/OWN", "INCIDENT_CREATE/BRANCH", "INCIDENT_VIEW/OWN", "IR_CREATE/OWN", "IR_PROCESS/OWN", "MR_CREATE/OWN", "ROOM_VIEW/BRANCH", "SHIFT_VIEW/OWN", "TASK_EXECUTE/OWN", "TASK_VIEW/OWN" ]),
            ["HK_SUPERVISOR"] = ("HK", [ "ATTENDANCE_RECORD/OWN", "CLEANING_ASSIGN/BRANCH", "CLEANING_VIEW/BRANCH", "INCIDENT_CREATE/BRANCH", "INCIDENT_VIEW/BRANCH", "INSPECTION_PERFORM/BRANCH", "INSPECTION_VIEW/BRANCH", "IR_CREATE/OWN", "MR_CREATE/OWN", "ROOM_VIEW/BRANCH", "SHIFT_VIEW/BRANCH", "TASK_EXECUTE/OWN", "TASK_VIEW/BRANCH" ]),
            ["MAINTENANCE_STAFF"] = ("MT", [ "ASSET_VIEW/BRANCH", "ATTENDANCE_RECORD/OWN", "INCIDENT_CREATE/BRANCH", "INCIDENT_VIEW/BRANCH", "IR_CREATE/OWN", "IR_PROCESS/OWN", "MAINTENANCE_EXECUTE/OWN", "MAINTENANCE_VIEW/OWN", "MR_CREATE/OWN", "ROOM_VIEW/BRANCH", "SHIFT_VIEW/OWN", "TASK_EXECUTE/OWN", "TASK_VIEW/OWN" ]),
            ["MAINTENANCE_LEAD"] = ("MT", [ "ASSET_VIEW/BRANCH", "ATTENDANCE_RECORD/OWN", "INCIDENT_CREATE/BRANCH", "INCIDENT_TRIAGE/BRANCH", "INCIDENT_VIEW/BRANCH", "IR_CREATE/OWN", "IR_PROCESS/BRANCH", "MAINTENANCE_ASSIGN/BRANCH", "MAINTENANCE_EXECUTE/OWN", "MAINTENANCE_REVIEW/BRANCH", "MAINTENANCE_VIEW/BRANCH", "MR_CREATE/OWN", "PM_MANAGE/BRANCH", "ROOM_BLOCK_MANAGE/BRANCH", "ROOM_VIEW/BRANCH", "SHIFT_VIEW/BRANCH", "TASK_EXECUTE/OWN", "TASK_VIEW/BRANCH" ]),
            ["STOREKEEPER"] = ("WH", [ "ASSET_VIEW/BRANCH", "ATTENDANCE_RECORD/OWN", "INVENTORY_VIEW/BRANCH", "IR_CREATE/OWN", "IR_PROCESS/OWN", "MR_CREATE/OWN", "MR_FULFILL/BRANCH", "SHIFT_VIEW/OWN", "STOCK_ISSUE_POST/BRANCH", "STOCK_RECEIPT_POST/BRANCH", "STOCK_RETURN_POST/BRANCH", "STOCK_TRANSFER_POST/BRANCH", "STOCKTAKE_PERFORM/BRANCH", "TASK_EXECUTE/OWN", "TASK_VIEW/OWN" ]),
        };

    internal static bool TryGetDepartment(string code, out string department)
    {
        department = string.Empty;
        if (code is null || !Roles.TryGetValue(code, out var policy)) return false;
        department = policy.Department; return true;
    }

    internal static bool Matches(string code, IEnumerable<string> grants)
        => Roles.TryGetValue(code, out var policy)
            && policy.Grants.Order(StringComparer.Ordinal).SequenceEqual(grants.Order(StringComparer.Ordinal), StringComparer.Ordinal);
}
