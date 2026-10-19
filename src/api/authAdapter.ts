import type { SessionDto } from "./types";

/** Real JSON response from BE1 AuthController, not the older FE SessionDto. */
export interface BackendSessionDto {
  user: { id: string; username: string; name: string };
  expiresAt: string;
  absoluteExpiresAt: string;
  mustChangePassword: boolean;
  csrfToken: string;
}

/** No fabricated grants, roles or scopes: the current BE1 auth API omits them. */
export function adaptBackendSession(raw: BackendSessionDto): SessionDto {
  if (!raw || !raw.user || typeof raw.user.id !== "string" ||
      typeof raw.user.username !== "string" ||
      typeof raw.user.name !== "string" ||
      !raw.expiresAt || !Number.isFinite(Date.parse(raw.expiresAt)) ||
      typeof raw.mustChangePassword !== "boolean" ||
      typeof raw.csrfToken !== "string" || !raw.csrfToken) {
    throw new Error("Authentication API returned an invalid session response.");
  }
  return {
    principal: {
      id: raw.user.id,
      username: raw.user.username,
      name: raw.user.name,
      email: "",
      roleNames: [],
      primaryBranch: { id: "", name: "" },
      primaryDepartment: { id: "", name: "" },
      branches: [],
      departments: [],
      grants: [],
    },
    expiresAt: raw.expiresAt,
    mustChangePassword: raw.mustChangePassword,
    csrfToken: raw.csrfToken,
  };
}
