import { useEffect, useState } from "react";
import { useAuth } from "../../auth/AuthProvider";
type Profile = {
  employeeCode: string;
  fullName: string;
  email: string;
  phone: string | null;
  branch: string | null;
  department: string | null;
  roles: { name: string; branch: string | null }[];
};
export function PersonalProfileDetails() {
  const { state, actions } = useAuth();
  const owner = state.authorityGeneration;
  const [revision, retry] = useState(0);
  const [snapshot, setProfile] = useState<{
    owner: typeof owner;
    data: Profile;
  } | null>(null);
  const profile = snapshot && snapshot.owner === owner ? snapshot.data : null;
  const [error, setError] = useState(false);
  useEffect(() => {
    const abort = new AbortController();
    setProfile(null);
    setError(false);
    void fetch("/api/auth/profile", {
      credentials: "same-origin",
      cache: "no-store",
      redirect: "error",
      signal: abort.signal,
    })
      .then(async (r) => {
        if (r.status === 401) {
          if (!abort.signal.aborted) actions.invalidateSession(owner);
          throw Error();
        }
        if (!r.ok) throw Error();
        const p = await r.json();
        if (
          !p ||
          !["employeeCode", "fullName", "email"].every(
            (k) => typeof p[k] === "string",
          ) ||
          !["phone", "branch", "department"].every(
            (k) => p[k] === null || typeof p[k] === "string",
          ) ||
          !Array.isArray(p.roles) ||
          p.roles.some(
            (r: Profile["roles"][number]) =>
              !r ||
              typeof r.name !== "string" ||
              !(r.branch === null || typeof r.branch === "string"),
          )
        )
          throw Error();
        if (!abort.signal.aborted) setProfile({ owner, data: p });
      })
      .catch(() => {
        if (!abort.signal.aborted) setError(true);
      });
    return () => abort.abort();
  }, [owner, actions, revision]);
  if (error)
    return (
      <p role="alert">
        Chưa tải được thông tin nhân viên.{" "}
        <button onClick={() => retry((v) => v + 1)}>Thử lại</button>
      </p>
    );
  if (!profile) return <p role="status">Đang tải thông tin nhân viên…</p>;
  return (
    <>
      <dl className="metadata">
        {[
          ["Mã nhân viên", profile.employeeCode],
          ["Họ và tên", profile.fullName],
          ["Email", profile.email],
          ["Số điện thoại", profile.phone],
          ["Cơ sở làm việc", profile.branch],
          ["Bộ phận làm việc", profile.department],
        ].map(([label, value]) => (
          <div key={label}>
            <dt>{label}</dt>
            <dd>{value || "Chưa cập nhật"}</dd>
          </div>
        ))}
      </dl>
      <h3>Vai trò đang được phân công</h3>
      {profile.roles.length ? (
        <ul className="profile-roles">
          {profile.roles.map((r, i) => (
            <li key={i}>
              <strong>{r.name}</strong>
              <span>{r.branch ?? "Toàn hệ thống"}</span>
            </li>
          ))}
        </ul>
      ) : (
        <p>Chưa có vai trò đang có hiệu lực.</p>
      )}
    </>
  );
}
