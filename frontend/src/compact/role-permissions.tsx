import { useState } from "react";
import { api, fields, message, type Row } from "./api";
import { Dialog, State, useQuery } from "./ui";
import { permissionText } from "./permission-copy";
export default function RolePermissions({
  role,
  close,
}: {
  role: Row;
  close: () => void;
}) {
  const query = useQuery(`users/roles/${role.roleId}/permissions`),
    permissions = useQuery("catalog/permissions?pageSize=100");
  const [pending, setPending] = useState(false),
    [error, setError] = useState("");
  const locked = role.code === "SYS_ADMIN";
  return (
    <Dialog
      title={`Quyền · ${role.name}`}
      close={() => {
        if (!pending) close();
      }}
    >
      <State {...query}>
        <State {...permissions}>
          <p>
            {locked
              ? "Quyền Admin được bảo vệ. Không thể thay đổi bằng màn hình này."
              : "Thay đổi áp dụng cho tất cả người có vai trò này. Hệ thống sẽ kiểm tra công việc và lịch ca đang phụ thuộc."}
          </p>
          <form
            onSubmit={async (e) => {
              e.preventDefault();
              if (pending || locked) return;
              const form = new FormData(e.currentTarget),
                grants = fields(permissions.data?.items)
                  .filter((p) => form.get(`grant-${p.permissionId}`))
                  .map((p) => ({
                    permissionId: p.permissionId,
                    dataScope: form.get(`scope-${p.permissionId}`),
                  }));
              setPending(true);
              setError("");
              try {
                await api(
                  `users/roles/${role.roleId}/permissions`,
                  grants,
                  "PUT",
                );
                close();
              } catch (e) {
                setError(message(e));
              } finally {
                setPending(false);
              }
            }}
          >
            <div className="table-scroll">
              <table>
                <thead>
                  <tr>
                    <th>Cấp quyền</th>
                    <th>Chức năng</th>
                    <th>Phạm vi</th>
                  </tr>
                </thead>
                <tbody>
                  {fields(permissions.data?.items).map((p) => {
                    const text = permissionText(p.code, p.description);
                    const grant = query.data?.permissions.find(
                      (g: Row) => g.permissionId === p.permissionId,
                    );
                    return (
                      <tr key={p.permissionId}>
                        <td>
                          <input
                            type="checkbox"
                            aria-label={`Cấp quyền ${text.name}`}
                            name={`grant-${p.permissionId}`}
                            defaultChecked={!!grant}
                            disabled={locked}
                          />
                        </td>
                        <td>
                          <strong>{text.name}</strong>
                          <small>{text.description}</small>
                        </td>
                        <td>
                          <select
                            aria-label={`Phạm vi ${text.name}`}
                            name={`scope-${p.permissionId}`}
                            defaultValue={grant?.dataScope || "BRANCH"}
                            disabled={locked}
                          >
                            <option value="OWN">Của mình</option>
                            <option value="BRANCH">Chi nhánh được gán</option>
                            <option value="ALL">Toàn hệ thống</option>
                          </select>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
            {error && (
              <p className="alert" role="alert">
                {error}
              </p>
            )}
            {!locked && (
              <button className="primary" disabled={pending}>
                Lưu phân quyền
              </button>
            )}
          </form>
        </State>
      </State>
    </Dialog>
  );
}
