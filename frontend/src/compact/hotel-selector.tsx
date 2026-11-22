import { useEffect, useState } from "react";
import { Building2 } from "lucide-react";
import type { HotelOption } from "./hotel-3d/scene";

export default function HotelSelector({
  hotels,
  selected,
  onSelect,
}: {
  hotels: HotelOption[];
  selected?: number;
  onSelect: (id: number) => void;
}) {
  const [posters, setPosters] = useState<Record<string, string>>({});
  const [failed, setFailed] = useState(false);
  useEffect(() => {
    let cancelled = false;
    import("./hotel-3d/scene")
      .then(({ hotelPoster }) => {
        if (cancelled) return;
        try {
          setPosters({
            active: hotelPoster(true),
            inactive: hotelPoster(false),
          });
        } catch {
          setFailed(true);
        }
      })
      .catch(() => {
        if (!cancelled) setFailed(true);
      });
    return () => {
      cancelled = true;
    };
  }, []);
  return (
    <section className="hotel-selector" aria-label="Chọn khách sạn">
      <div className="hotel-options">
        {hotels.map((h) => (
          <button
            className="hotel-card"
            key={h.id}
            aria-pressed={
              selected === undefined ? undefined : h.id === selected
            }
            onClick={() => onSelect(h.id)}
          >
            <div className="hotel-preview">
              {posters[h.active ? "active" : "inactive"] ? (
                <img
                  src={posters[h.active ? "active" : "inactive"]}
                  alt=""
                  width="600"
                  height="440"
                />
              ) : (
                <Building2 size={64} aria-hidden="true" />
              )}
            </div>
            <div className="hotel-card-label">
              <strong>{h.name}</strong>
              <span className={`badge ${h.active ? "good" : "neutral"}`}>
                {h.active ? "Hoạt động" : "Đã ngừng"}
              </span>
            </div>
          </button>
        ))}
      </div>
      {failed && (
        <p className="hint" role="status">
          Không tải được mô hình 3D. Bạn vẫn có thể chọn khách sạn.
        </p>
      )}
    </section>
  );
}
