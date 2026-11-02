/** Proposed transport settings. Confirm authentication and CSRF with the API team. */
const env = import.meta.env ?? {};
const mode = env.VITE_AUTH_MODE ?? "unconfigured";
export const apiConfig = {
  baseUrl: (env.VITE_API_BASE_URL ?? "").replace(/\/$/, ""),
  authMode: mode as "cookie" | "bearer" | "unconfigured",
  csrfHeader: env.VITE_CSRF_HEADER ?? "X-CSRF-TOKEN",
  timeoutMs: 20000,
};
export function requireAuthConfiguration() {
  if (!["cookie", "bearer"].includes(apiConfig.authMode))
    throw new Error(
      "Set VITE_AUTH_MODE to the authentication method agreed with the API team.",
    );
}
