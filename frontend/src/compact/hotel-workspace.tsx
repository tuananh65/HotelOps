import { AlertPopup } from "./alert-popup";
import PageTitle from "./page-title";
import { useEffect, useState } from "react";
import StructureTree from "./structure-tree";
import { api, message, type Row } from "./api";
import { useAuth } from "./auth";
import { Dialog, Empty, State } from "./ui";
import { useCatalogRows } from "./use-catalog-rows";
import BranchTree from "./branch-tree";
import Catalog from "./catalog";

function HotelDetail({
  hotel,
  revision,
  refresh,
}: {
  hotel: Row;
  revision: number;
  refresh: () => void;
}) {
  const params = new URLSearchParams(location.hash.split("?")[1]);
  const section = params.get("resource") || "overview";
  const floorId = Number(params.get("location")) || null;
  const { canGlobal } = useAuth();
  const canEdit = canGlobal("MASTER_DATA_MANAGE");
  const locations = useCatalogRows(
    hotel.isActive ? "locations" : null,
    hotel.branchId,
    revision,
  );
  const rooms = useCatalogRows(
    hotel.isActive ? "rooms" : null,
    hotel.branchId,
    revision,
  );
  const [edit, setEdit] = useState<{ resource: string; row: Row } | null>(null);
  const [hotelEditing, setHotelEditing] = useState(false);
  const [pending, setPending] = useState(false);
  const [error, setError] = useState("");
  const go = (resource = "overview", locationId?: number) => {
    location.hash = `catalog?hotel=${hotel.branchId}&resource=${resource}${locationId ? `&location=${locationId}` : ""}`;
  };
  return (
    <>
      <div className="page-heading">
        <PageTitle>
          <h1>{hotel.name}</h1>
          <span className={`badge ${hotel.isActive ? "good" : "neutral"}`}>
            {hotel.isActive ? "Hoạt động" : "Đã ngừng"}
          </span>
        </PageTitle>
      </div>
      {section !== "rooms" && (
        <div className="hotel-overview-toolbar">
          <a className="button-link" href="#catalog">
            ← Tất cả khách sạn
          </a>
          <div className="hotel-overview-actions">
            {canEdit && (
              <button
                onClick={() => {
                  setError("");
                  setHotelEditing(true);
                }}
              >
                Chỉnh sửa khách sạn
              </button>
            )}
            {hotel.isActive && (
              <>
                <button onClick={() => go("rooms")}>Xem tất cả phòng</button>
                {canEdit && (
                  <button
                    className="primary"
                    onClick={() => setEdit({ resource: "locations", row: {} })}
                  >
                    + Thêm tầng / khu vực
                  </button>
                )}
              </>
            )}
          </div>
        </div>
      )}
      {section !== "rooms" && (
        <dl className="branch-information">
          <div>
            <dt>Địa chỉ</dt>
            <dd>{hotel.address || "Chưa cập nhật"}</dd>
          </div>
          <div>
            <dt>Số điện thoại</dt>
            <dd>{hotel.phone || "Chưa cập nhật"}</dd>
          </div>
          <div>
            <dt>Người đại diện</dt>
            <dd>{hotel.representativeName || "Chưa cập nhật"}</dd>
          </div>
        </dl>
      )}
      {hotel.isActive ? (
        <>
          {section === "rooms" ? (
            <Catalog
              key={`${hotel.branchId}-${floorId}`}
              embedded
              mode="hotels"
              resourceOverride="rooms"
              toolbarAction={
                <button onClick={() => go()}>← Tầng & phòng</button>
              }
            />
          ) : (
            <State
              loading={locations.loading || rooms.loading}
              error={locations.error || rooms.error}
              retry={refresh}
            >
              <div className="structure-summary">
                <span>
                  <strong>{locations.data?.length || 0}</strong> tầng / khu vực
                </span>
                <span>
                  <strong>{rooms.data?.length || 0}</strong> phòng
                </span>
              </div>
              <StructureTree
                locations={locations.data || []}
                rooms={rooms.data || []}
                onRooms={(id) => go("rooms", id)}
                onEdit={
                  canEdit
                    ? (loc) => setEdit({ resource: "locations", row: loc })
                    : undefined
                }
              />
              {!locations.data?.length && (
                <Empty>
                  Chưa có tầng / khu vực. Thêm tầng để bắt đầu sắp xếp phòng.
                </Empty>
              )}
            </State>
          )}
        </>
      ) : (
        <Empty>
          Khách sạn đã ngừng hoạt động. Bạn có thể cập nhật tên và trạng thái.
        </Empty>
      )}
      {edit && (
        <Catalog
          dialogOnly
          embedded
          mode="hotels"
          resourceOverride={edit.resource}
          initialEditing={edit.row}
          onFinish={(saved) => {
            setEdit(null);
            if (saved) refresh();
          }}
        />
      )}
      {hotelEditing && (
        <Dialog
          title="Chỉnh sửa khách sạn"
          close={() => {
            if (!pending) setHotelEditing(false);
          }}
        >
          <form
            onSubmit={async (e) => {
              e.preventDefault();
              if (pending) return;
              const values = new FormData(e.currentTarget);
              setPending(true);
              setError("");
              try {
                await api(
                  `catalog/branches/${hotel.branchId}`,
                  {
                    fields: {
                      name: String(values.get("name")).trim(),
                      address:
                        String(values.get("address") || "").trim() || null,
                      phone: String(values.get("phone") || "").trim() || null,
                      representativeName:
                        String(values.get("representativeName") || "").trim() ||
                        null,
                      isActive: values.get("isActive") === "on",
                    },
                    rowVersion: hotel.rowVersion,
                  },
                  "PUT",
                );
                setHotelEditing(false);
                refresh();
              } catch (e) {
                setError(message(e));
              } finally {
                setPending(false);
              }
            }}
          >
            <label>
              Tên khách sạn
              <input
                name="name"
                defaultValue={hotel.name}
                maxLength={150}
                required
              />
            </label>
            <label>
              Người đại diện
              <input
                name="representativeName"
                defaultValue={hotel.representativeName || ""}
                maxLength={150}
              />
            </label>
            <label>
              Địa chỉ
              <input
                name="address"
                defaultValue={hotel.address || ""}
                maxLength={500}
              />
            </label>
            <label>
              Số điện thoại
              <input
                name="phone"
                type="tel"
                defaultValue={hotel.phone || ""}
                maxLength={30}
                pattern={"\\+?[0-9][0-9 \\(\\)\\-]{6,28}[0-9]"}
                title="Nhập số điện thoại từ 8 đến 30 ký tự, gồm chữ số, dấu +, khoảng trắng hoặc dấu gạch nối."
              />
            </label>
            <label className="checkbox-label">
              <input
                type="checkbox"
                name="isActive"
                defaultChecked={hotel.isActive}
              />{" "}
              Đang hoạt động
            </label>
            {error && (
              <AlertPopup>
                {error}
              </AlertPopup>
            )}
            <button className="primary" disabled={pending}>
              Lưu thay đổi
            </button>
          </form>
        </Dialog>
      )}
    </>
  );
}

export default function HotelWorkspace() {
  const { branch, setBranch, canGlobal, refresh } = useAuth();
  const [revision, bump] = useState(0);
  const [creating, setCreating] = useState(false);
  const query = useCatalogRows("branches", branch, revision);
  const id = Number(
    new URLSearchParams(location.hash.split("?")[1]).get("hotel"),
  );
  const selected = query.data?.find((h) => h.branchId === id);
  const legacyResource = new URLSearchParams(location.hash.split("?")[1]).get(
    "resource",
  );
  const movedResource = ["departments", "room-types"].includes(
    legacyResource || "",
  )
    ? legacyResource
    : null;
  useEffect(() => {
    if (selected?.isActive && selected.branchId !== branch)
      setBranch(selected.branchId);
    if (movedResource && (!id || selected?.isActive))
      location.hash = `settings?resource=${movedResource}`;
  }, [selected, branch, setBranch, movedResource, id]);
  const reload = () => {
    bump((n) => n + 1);
    refresh().catch(() => {});
  };
  return (
    <State {...query} retry={() => bump((n) => n + 1)}>
      {selected ? (
        <HotelDetail hotel={selected} revision={revision} refresh={reload} />
      ) : (
        <>
          <div className="page-heading">
            <PageTitle>
              <h1>Khách sạn</h1>
              <p>Chọn khách sạn để xem và quản lý cấu trúc.</p>
            </PageTitle>
            {canGlobal("MASTER_DATA_MANAGE") && (
              <button className="primary" onClick={() => setCreating(true)}>
                + Thêm khách sạn
              </button>
            )}
          </div>
          {id > 0 && (
            <AlertPopup>
              Không tìm thấy khách sạn trong phạm vi của bạn. Chọn lại bên dưới.
            </AlertPopup>
          )}
          <BranchTree hotels={query.data || []} />
          {!query.data?.length && <Empty>Chưa có khách sạn.</Empty>}
        </>
      )}
      {creating && (
        <Catalog
          mode="hotels"
          embedded
          dialogOnly
          resourceOverride="branches"
          initialEditing={{}}
          onFinish={(saved) => {
            setCreating(false);
            if (saved) reload();
          }}
        />
      )}
    </State>
  );
}
