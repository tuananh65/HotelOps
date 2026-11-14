// Test-only TypeScript syntax loader; type safety is checked separately by tsc.
import ts from "../node_modules/typescript/lib/typescript.js";
import { readFile } from "node:fs/promises";
export async function resolve(specifier, context, nextResolve) {
  if (
    context.parentURL?.endsWith(".ts") &&
    specifier.startsWith(".") &&
    !/\.[a-z]+$/.test(specifier)
  )
    return {
      url: new URL(specifier + ".ts", context.parentURL).href,
      shortCircuit: true,
    };
  return nextResolve(specifier, context);
}
export async function load(url, context, nextLoad) {
  if (!url.endsWith(".ts")) return nextLoad(url, context);
  const source = await readFile(new URL(url), "utf8");
  return {
    format: "module",
    shortCircuit: true,
    source: ts.transpileModule(source, {
      compilerOptions: {
        target: ts.ScriptTarget.ES2022,
        module: ts.ModuleKind.ESNext,
      },
      fileName: url,
    }).outputText,
  };
}
