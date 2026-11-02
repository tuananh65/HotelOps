// Test-only entry ensures the controllers and existing auth share one module instance.
export * from "../src/controllers/dev1";
export { login, logout } from "../src/services";
