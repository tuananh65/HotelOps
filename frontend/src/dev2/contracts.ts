export interface Field {
  name: string;
  type: string;
  optional: boolean;
  default?: string | number | boolean;
  fields?: Field[];
  options?: string[];
}
export interface Operation {
  id: string;
  decisionId: string | null;
  module: string;
  name: string;
  method: "GET" | "POST" | "PUT" | "PATCH" | "DELETE";
  route: string;
  permissions: string[];
  permissionHint?: string;
  authorityKind?: string;
  expectedStatus: number;
  dto?: string;
  fields: Field[];
  source: string;
}
export type Values = Record<string, unknown>;
export const modules = [
  {
    id: "incidents",
    name: "Incidents",
    list: "A004A-022",
    detail: "A004A-023",
    create: "A004A-021",
  },
  {
    id: "maintenance",
    name: "Maintenance Works",
    list: "A004A-035",
    detail: "A004A-036",
    create: "A004A-024",
  },
  {
    id: "material-requests",
    name: "Material Requests",
    list: "A004A-037",
    detail: "A004A-038",
    create: "A004A-043",
  },
  {
    id: "inventory",
    name: "Inventory",
    list: "inventory:Stock",
    detail: "inventory:Document",
    create: "inventory:CreateReceipt",
  },
  {
    id: "assets",
    name: "Assets",
    list: "A004A-009",
    detail: "A004A-010",
    create: "A004A-014",
  },
  {
    id: "replenishments",
    name: "Replenishments",
    list: "A004A-052",
    detail: "A004A-053",
    create: "A004A-056",
  },
] as const;
export const label = (key: string) =>
  key.replace(/([a-z])([A-Z])/g, "$1 $2").replace(/^./, (x) => x.toUpperCase());
