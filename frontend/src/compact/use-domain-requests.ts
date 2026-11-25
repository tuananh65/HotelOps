import { type Row } from "./api";
import { useAuth } from "./auth";
import { useQuery } from "./ui";

/** Read-only projection: decisions still use each domain's transactional workflow. */
export function useDomainRequests(revision: number, queue: string, search: string) {
  const { branch, profile, can } = useAuth();
  const role = (code: string) => profile.roles.some(r => r.code === code && r.branchId === branch);
  const writeOffAccess = role("STOREKEEPER") && can("STOCK_ISSUE_POST", false)
    || role("OPERATION_MANAGER") && can("ROOM_SUPPLY_CONFIG", false);
  const replacementAccess = role("OPERATION_MANAGER") && can("ASSET_MANAGE", false);
  const writeOffs = useQuery<Row[]>(writeOffAccess ? `reusable-inventory/write-offs?branchId=${branch}` : null, revision);
  const replacements = useQuery<Row[]>(replacementAccess ? `work/replacements?branchId=${branch}` : null, revision);
  const userId = profile.user.fields.userId;
  const rows: Row[] = [
    ...(writeOffs.data || []).map(r => ({
      workId: r.writeOffId, requestKey: `write-off:${r.writeOffId}`, domain: "write-offs",
      requestType: "REUSABLE_WRITE_OFF", title: `Ghi giảm · ${r.name} · ${r.quantity}`,
      status: r.status, queueRole: r.status === "PENDING" ? "OPERATION_MANAGER" : null,
      createdBy: r.createdBy, updatedAt: r.decidedAt || r.createdAt,
      actionable: role("OPERATION_MANAGER") && r.status === "PENDING" && r.createdBy !== userId,
      completed: r.status === "APPROVED", rejected: r.status === "REJECTED",
    })),
    ...(replacements.data || []).map(r => ({
      ...r, requestKey: `replacement:${r.workId}`, domain: "replacement",
      requestType: "ASSET_REPLACEMENT", queueRole: r.status === "REPLACEMENT_CONFIRMED" ? "OPERATION_MANAGER" : null,
      actionable: r.canDecide, completed: r.status === "REPLACEMENT_INSTALLED", rejected: r.status === "REPLACEMENT_REJECTED",
    })),
  ];
  const term = search.trim().toLocaleLowerCase("vi");
  return {
    loading: writeOffs.loading || replacements.loading,
    error: writeOffs.error || replacements.error,
    data: rows.filter(r => (!term || r.title.toLocaleLowerCase("vi").includes(term))
      && (queue === "mine" ? r.createdBy === userId : queue === "action" ? r.actionable
        : queue === "completed" ? r.completed : queue === "rejected" ? r.rejected
          : queue === "progress" ? !r.completed && !r.rejected : true)),
    writeOffAccess, replacementAccess,
  };
}
