import { useRef, useState } from "react";
import { Link } from "react-router-dom";
import {
  destinationBranchId,
  unknownMembershipMessage,
  type useAddUserBranch,
  type BranchAssignmentOption,
} from "./useAddUserBranch";

function ConfirmationForm({
  target,
  add,
  options,
}: {
  target: number;
  add(branchId: number, departmentId: number): Promise<void>;
  options:BranchAssignmentOption[];
}) {
  const [draft, setDraft] = useState("");
  const [departmentDraft, setDepartmentDraft] = useState("");
  const [review, setReview] = useState<{branch:number;department:number} | null>(null);
  const reviewed = useRef<{branch:number;department:number} | null>(null);
  const [error, setError] = useState("");
  function preview() {
    const branch = destinationBranchId(draft);
    const department = destinationBranchId(departmentDraft);
    if (branch === null || department === null || !options.some(b=>b.id===branch&&b.departments.some(d=>d.id===department))) {
      setError(
        "Chọn chi nhánh và bộ phận làm việc khả dụng.",
      );
      return;
    }
    setError("");
    reviewed.current = {branch,department};
    setReview({branch,department});
  }
  function confirm() {
    if (reviewed.current === null) return;
    const branch = reviewed.current;
    reviewed.current = null;
    setReview(null);
    void add(branch.branch, branch.department);
  }
  return (
    <form
      aria-label="Thêm phân quyền chi nhánh"
      noValidate
      onSubmit={(e) => e.preventDefault()}
    >
      <label>
        Chi nhánh đích
        <select
          name="branchId"
          required
          value={draft}
          onChange={(e) => {
            setDraft(e.target.value);
            setDepartmentDraft("");
            reviewed.current = null;
            setReview(null);
            setError("");
          }}
        ><option value="">Chọn chi nhánh</option>{options.map(b=><option key={b.id} value={b.id}>{b.name}</option>)}</select>
      </label>
      <label>Bộ phận làm việc
        <select name="departmentId" required value={departmentDraft}
          onChange={e=>{setDepartmentDraft(e.target.value);reviewed.current=null;setReview(null);setError("");}}>
          <option value="">Chọn bộ phận</option>{options.find(b=>b.id===Number(draft))?.departments.map(d=><option key={d.id} value={d.id}>{d.name}</option>)}
        </select>
      </label>
      <p>Bộ phận phải đang hoạt động và thuộc chi nhánh đích. Cả hai phân công thành viên được lưu cùng lúc.</p>
      {options.length===0&&<p role="status">Không có chi nhánh mới với bộ phận hoạt động trong phạm vi quản lý hiện tại.</p>}
      <button type="button" disabled={!draft||!departmentDraft} onClick={preview}>
        Xem lại thêm chi nhánh
      </button>
      {review !== null && (
        <section aria-label="Xem lại phân quyền chi nhánh">
          <p>
            Thêm quyền chi nhánh phụ mức STANDARD cho tài khoản: {target}, Mã chi nhánh đích: {review.branch}, Mã bộ phận làm việc: {review.department}?
          </p>
          <button type="button" onClick={confirm}>
            Xác nhận thêm chi nhánh
          </button>
        </section>
      )}
      {error && <p role="alert">{error}</p>}
    </form>
  );
}

export function AddUserBranchPage({
  target,
  flow,
}: {
  target: number;
  flow: ReturnType<typeof useAddUserBranch>;
}) {
  const { snapshot, progress } = flow;
  const locked = ["pending", "unknown", "stale", "confirmed"].includes(
    progress.phase,
  );
  const membership =
    progress.phase === "confirmed" ? progress.membership : undefined;
  return (
    <main>
      <h1>Thêm phân quyền chi nhánh cho người dùng {target}</h1>
      <Link to="/profile">Hồ sơ cá nhân</Link>
      <p>
        Khi gửi, máy chủ kiểm tra tài khoản đích, chi nhánh đích, thành viên hiện có và quyền nguồn / đích. Tải hồ sơ không cấp quyền thêm thành viên, kể cả cho chính bạn.
      </p>
      <p>
        Thao tác thêm quyền chi nhánh phụ mức STANDARD và phân công bộ phận đã chọn. Không đổi chi nhánh chính, bộ phận chính hoặc vai trò.
      </p>
      {progress.phase === "unknown" && (
        <>
          <p role="alert">{unknownMembershipMessage}</p>
          <p>
            Việc tải lại chỉ xác nhận danh tính và phiên bản, không xác nhận yêu cầu trước đã lưu. Thao tác thêm vẫn bị khóa tại đây.
          </p>
        </>
      )}
      {membership && (
        <section aria-label="Phân công thành viên đã xác nhận" role="status">
          <p>Máy chủ đã xác nhận thêm phân quyền chi nhánh. Nhân viên cần đăng nhập lại để dùng phạm vi mới.</p>
          <p>Mã quyền truy cập: {membership.accessId}</p>
          <p>Mã người dùng: {membership.userId}</p>
          <p>Mã chi nhánh: {membership.branchId}</p>
          <p>Bộ phận làm việc: {membership.departmentId}</p>
          <p>Mức truy cập: {membership.accessLevel}</p>
          <p>Là chính: {String(membership.isPrimary)} (phụ)</p>
        </section>
      )}
      {progress.phase === "pending" && (
        <p role="status">Đang chờ yêu cầu phân quyền chi nhánh…</p>
      )}
      {progress.message && (
        <p role="alert">
          {progress.phase === "not-dispatched"
            ? "Không gửi yêu cầu thêm phân công thành viên."
            : ""}
          {progress.message}
        </p>
      )}
      {flow.error && <p role="alert">{flow.error}</p>}
      {flow.loading && <p role="status">Đang tải hồ sơ cá nhân...</p>}
      {snapshot && (
        <section aria-label="Bản chụp thông tin quản lý từ máy chủ">
          <p>Mã tài khoản: {snapshot.profile.id}</p>
          <p>Họ và tên: {snapshot.profile.fullName}</p>
          <p>Email: {snapshot.profile.email}</p>
          {!locked && (
            <ConfirmationForm
              key={snapshot.revision}
              target={target}
              add={flow.add}
              options={snapshot.options}
            />
          )}
        </section>
      )}
      <button
        type="button"
        disabled={flow.loading || progress.phase === "pending"}
        onClick={flow.reload}
      >
        Tải lại hồ sơ cá nhân
      </button>
    </main>
  );
}
