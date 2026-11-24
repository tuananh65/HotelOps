import { lazy, Suspense, useState } from "react";
import { useAuth } from "./auth";
import { Dialog } from "./ui";
import { Building2, ArrowLeftRight } from "lucide-react";
const HotelSelector = lazy(() => import("./hotel-selector"));
export default function BranchContext() {
  const { profile, branch, setBranch } = useAuth();
  const [open, setOpen] = useState(false);
  const [selected, setSelected] = useState(branch);
  const current = profile.branches.find((b) => b.branchId === branch);
  if (!current || profile.roles.some(role=>role.code==="SYS_ADMIN")) return null;
  return (
    <div className="branch-context">
      <div className="branch-current" title={current.name}>
        <Building2 size={17} aria-hidden="true" />
        <span>{current.name}</span>
      </div>
      {profile.branches.length > 1 && (
        <button
          aria-label="Đổi khách sạn"
          title="Đổi khách sạn"
          onClick={() => {
            setSelected(branch);
            setOpen(true);
          }}
        >
          <ArrowLeftRight size={16} aria-hidden="true" />
          <span>Đổi khách sạn</span>
        </button>
      )}
      {open && (
        <Dialog title="Chọn khách sạn" close={() => setOpen(false)}>
          <Suspense fallback={<p>Đang tải…</p>}>
            <HotelSelector
              hotels={profile.branches.map((b) => ({
                id: b.branchId,
                name: b.name,
                active: b.isActive !== false,
              }))}
              selected={selected}
              onSelect={setSelected}
            />
          </Suspense>
          <button
            className="primary"
            onClick={() => {
              setBranch(selected);
              setOpen(false);
            }}
          >
            Sử dụng khách sạn này
          </button>
        </Dialog>
      )}
    </div>
  );
}
