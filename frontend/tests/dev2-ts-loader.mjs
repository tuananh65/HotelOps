import ts from "../node_modules/typescript/lib/typescript.js";
import { existsSync } from "node:fs";
import { readFile } from "node:fs/promises";
export async function resolve(specifier, context, nextResolve) {
  if (
    /\.tsx?$/.test(context.parentURL ?? "") &&
    specifier.startsWith(".") &&
    !/\.(ts|tsx|mjs|js|json|css)$/.test(specifier)
  ) {
    for (const suffix of [".ts", ".tsx"]) {
      const url = new URL(specifier + suffix, context.parentURL);
      if (existsSync(url)) return { url: url.href, shortCircuit: true };
    }
  }
  return nextResolve(specifier, context);
}
export async function load(url, context, nextLoad) {
  // SSR-only hook: styles remain checked by the production build.
  if (url.endsWith(".json")) return {format:"module",shortCircuit:true,source:"export default "+JSON.stringify(JSON.parse(await readFile(new URL(url), "utf8")))};
  if (url.endsWith(".css")) return {format:"module",shortCircuit:true,source:"export default {};"};
  if (!/\.tsx?$/.test(url)) return nextLoad(url, context);
  return {
    format: "module",
    shortCircuit: true,
    source: ts.transpileModule(await readFile(new URL(url), "utf8"), {
      fileName: url,
      compilerOptions: {
        target: ts.ScriptTarget.ES2022,
        module: ts.ModuleKind.ESNext,
        jsx: ts.JsxEmit.ReactJSX,
      },
    }).outputText,
  };
}
