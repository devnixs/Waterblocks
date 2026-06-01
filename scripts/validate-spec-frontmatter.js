#!/usr/bin/env node
// Verify every specs/*.spec.md has required frontmatter fields: area, owners, status, depends_on.

const fs = require("fs");
const path = require("path");

const root = process.cwd();
const specsDir = path.join(root, "specs");

function listSpecFiles() {
  if (!fs.existsSync(specsDir)) return [];
  return fs
    .readdirSync(specsDir)
    .filter((n) => n.endsWith(".spec.md"))
    .map((n) => path.join(specsDir, n))
    .sort();
}

function parseFrontmatter(text) {
  if (!text.startsWith("---\n") && !text.startsWith("---\r\n")) return null;
  const end = text.indexOf("\n---", 4);
  if (end === -1) return null;
  const block = text.slice(4, end);
  const out = {};
  let currentList = null;
  for (const rawLine of block.split(/\r?\n/)) {
    const line = rawLine.replace(/\s+$/, "");
    const scalar = /^([a-z_]+):(?:\s(.*))?$/.exec(line);
    if (scalar) {
      const key = scalar[1];
      const val = scalar[2];
      if (val === undefined || val === "") {
        out[key] = [];
        currentList = key;
      } else if (val === "[]") {
        out[key] = [];
        currentList = null;
      } else {
        out[key] = val;
        currentList = null;
      }
      continue;
    }
    const item = /^\s+-\s+(.*)$/.exec(line);
    if (item && currentList) {
      out[currentList].push(item[1]);
    }
  }
  return out;
}

const required = ["area", "owners", "status", "depends_on"];
const errors = [];

for (const specPath of listSpecFiles()) {
  const text = fs.readFileSync(specPath, "utf8");
  const fm = parseFrontmatter(text);
  const rel = path.relative(root, specPath);
  if (!fm) {
    errors.push(`${rel}: missing YAML frontmatter`);
    continue;
  }
  for (const key of required) {
    if (!(key in fm)) {
      errors.push(`${rel}: missing frontmatter field '${key}'`);
    }
  }
  if (fm.owners !== undefined && !Array.isArray(fm.owners)) {
    errors.push(`${rel}: 'owners' must be a list`);
  } else if (Array.isArray(fm.owners) && fm.owners.length === 0) {
    errors.push(`${rel}: 'owners' must list at least one owner`);
  }
}

if (errors.length > 0) {
  for (const e of errors) console.error(`error: ${e}`);
  process.exit(1);
}

console.log("validate-spec-frontmatter: OK");
