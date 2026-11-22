import PageTitle from "./page-title";
import { useEffect, useState } from "react";
import { BedDouble, Layers } from "lucide-react";
import { api, message, type Row } from "./api";
import { useAuth } from "./auth";
import { Dialog, Empty, State } from "./ui";
import { useCatalogRows } from "./use-catalog-rows";
import HotelSelector from "./hotel-selector";
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
              <div className="floor-grid">
                {locations.data?.map((loc) => {
                  const count =
                    rooms.data?.filter((r) => r.locationId === loc.locationId)
                      .length || 0;
                  return (
                    <article className="floor-card" key={loc.locationId}>
                      <button
                        className="floor-open"
                        onClick={() => go("rooms", loc.locationId)}
                      >
                        <Layers size={24} />
                        <strong>{loc.name}</strong>
                        <span>
                          {count} phòng ·{" "}
                          {loc.isActive ? "Hoạt động" : "Đã ngừng"}
                        </span>
                        <span className="floor-cta">
                          <BedDouble size={16} /> Xem phòng →
                        </span>
                      </button>
                      {canEdit && (
                        <button
                          className="floor-edit"
                          onClick={() =>
                            setEdit({ resource: "locations", row: loc })
                          }
                          aria-label={`Chỉnh sửa ${loc.name}`}
                        >
                          Chỉnh sửa
                        </button>
                      )}
                    </article>
                  );
                })}
              </div>
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
            <label className="checkbox-label">
              <input
                type="checkbox"
                name="isActive"
                defaultChecked={hotel.isActive}
              />{" "}
              Đang hoạt động
            </label>
            {error && (
              <p role="alert" className="alert">
                {error}
              </p>
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
            <p className="alert" role="alert">
              Không tìm thấy khách sạn trong phạm vi của bạn. Chọn lại bên dưới.
            </p>
          )}
          <HotelSelector
            hotels={(query.data || []).map((h) => ({
              id: h.branchId,
              name: h.name,
              active: h.isActive,
            }))}
            onSelect={(id) => {
              location.hash = `catalog?hotel=${id}`;
            }}
          />
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
