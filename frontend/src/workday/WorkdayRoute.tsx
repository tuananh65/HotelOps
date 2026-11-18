import { ShiftAssignmentSummary } from "./ShiftAssignmentSummary";
import { useState } from "react";
import { Link, Navigate, useSearchParams } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import { useWorkTimeScope, WorkTimeBranchField } from "../workTime/useWorkTimeScope";
import "../workTime/work-time.css";
import { WorkdayReadPanel } from "./WorkdayReadPanel";
import { safeId } from "./contracts";
import { UnscheduledWorkdaySummary } from "./UnscheduledWorkdaySummary";
import { ScheduledWorkdayPage } from "./ScheduledWorkdayPage";
import { LoadingIndicator } from "../components/LoadingIndicator";

function BranchSelection() {
  const [query] = useSearchParams();
  const noticeBranch = Number(query.get("branchId")), noticeId = Number(query.get("unscheduledId")), shiftAssignmentId = Number(query.get("shiftAssignmentId"));
  const scope = useWorkTimeScope("workday", noticeBranch);
  const branch = scope.branch;
  return <main className="time-workspace">
    <header className="time-heading"><div><h1>Lịch ca &amp; ngày làm việc</h1><p>Xem ca có thể bắt đầu hôm nay và ca đang làm. Thời gian theo giờ Việt Nam (UTC+7).</p></div></header>
    <WorkTimeBranchField scope={scope} />
    {branch && <>
      {branch.branchId === noticeBranch && safeId(shiftAssignmentId) && <ShiftAssignmentSummary key={`${noticeBranch}:${shiftAssignmentId}`} branchId={noticeBranch} assignmentId={shiftAssignmentId}/>}
      {branch.branchId === noticeBranch && safeId(noticeId) && <UnscheduledWorkdaySummary key={`${noticeBranch}:${noticeId}`} branchId={noticeBranch} workdayId={noticeId}/>}
      <WorkdayReadPanel key={`${scope.owner}:${branch.branchId}`} branchId={branch.branchId} canReadShifts={branch.canReadShifts} canReadCurrent={branch.canReadOwnTime}/>
    </>}
    <nav className="time-links" aria-label="Liên kết ca làm việc">
      {branch?.canReadShifts && branch.canReadOwnTime && <Link to={`/workday/scheduled?branchId=${branch.branchId}`}>Bắt đầu / kết thúc ca</Link>}
      {branch?.canReadOwnTime && <Link to="/my-time-log">Nhật ký thời gian của tôi</Link>}
      <Link to="/home">Trang chủ</Link>
    </nav>
  </main>;
}
/** Shared authenticated read route. Each API independently checks branch scope. */
export function WorkdayRoute({ scheduled = false }: { scheduled?: boolean }) {
  const { state, actions } = useAuth();
  if (state.status === "INITIALIZING" || state.pending !== null)
    return <LoadingIndicator label="Đang kiểm tra phiên đăng nhập" />;
  if (state.status === "ERROR_UNAVAILABLE")
    return (
      <section>
        <p role="alert">Phiên đăng nhập chưa khả dụng.</p>
        <button onClick={() => void actions.revalidate()}>Kiểm tra phiên đăng nhập</button>
      </section>
    );
  if (state.status === "RESTRICTED" || state.session?.mustChangePassword)
    return <Navigate replace to="/change-password" />;
  if (state.status !== "AUTHENTICATED" || !state.session)
    return <Navigate replace to="/login" />;
  // Reset the selection on session replacement, including same-user replacement.
  return <SessionSelection session={state.session} scheduled={scheduled} />;
}

function SessionSelection({ session, scheduled }: { session: object; scheduled: boolean }) {
  const [binding, setBinding] = useState(session);
  const [generation, setGeneration] = useState(0);
  if (binding !== session) {
    setBinding(session);
    setGeneration((value) => value + 1);
  }
  return scheduled ? <ScheduledWorkdayPage key={generation} /> : <BranchSelection key={`${generation}:${scheduled}`} />;
}
