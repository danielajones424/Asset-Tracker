import { defineConfig, loadEnv } from "vite";

// DEV identity: the API's dev-auth middleware (Development-only) reads
// X-Dev-* headers. Injecting them at the proxy keeps dev identity out of
// application code entirely — the SPA never knows dev auth exists.
// Set values in src/Client/.env.local (gitignored):
//   VITE_DEV_USER_ID=<guid of a seeded app_user row>
//   VITE_DEV_ROLE=UnitCustodian
//   VITE_DEV_UNIT_ID=<guid of that user's unit>
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), "VITE_DEV_");
  const devHeaders = env.VITE_DEV_USER_ID
    ? {
        "X-Dev-UserId": env.VITE_DEV_USER_ID,
        "X-Dev-Role": env.VITE_DEV_ROLE ?? "UnitCustodian",
        ...(env.VITE_DEV_UNIT_ID ? { "X-Dev-UnitId": env.VITE_DEV_UNIT_ID } : {}),
        ...(env.VITE_DEV_SQUADRON_ID ? { "X-Dev-SquadronId": env.VITE_DEV_SQUADRON_ID } : {}),
        ...(env.VITE_DEV_NAME ? { "X-Dev-Name": env.VITE_DEV_NAME } : {}),
      }
    : {};

  return {
    server: {
      port: 5173,
      proxy: {
        "/api": { target: "http://localhost:5000", headers: devHeaders },
      },
    },
    build: { outDir: "dist", sourcemap: true },
  };
});
