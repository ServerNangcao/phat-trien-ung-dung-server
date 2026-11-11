import { spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";
import path from "node:path";

const root = path.dirname(fileURLToPath(import.meta.url));
const source = process.env.OPENAPI_URL ?? `${process.env.VITE_API_URL ?? "http://localhost:8080"}/openapi/v1.json`;
const command = process.platform === "win32" ? "openapi.cmd" : "openapi";
const result = spawnSync(command, ["--input", source, "--output", "src/api/generated", "--client", "axios"], {
  cwd: path.resolve(root, ".."),
  stdio: "inherit",
  shell: process.platform === "win32",
});

if (result.error) {
  console.error(`Could not start OpenAPI type generation: ${result.error.message}`);
  process.exit(1);
}
process.exit(result.status ?? 1);
