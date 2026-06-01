#!/usr/bin/env node
// Verify stable IDs in specs/*.spec.md are unique within each file.
// Tolerant on first run: only flags duplicates, does not require gap-free IDs.

const fs = require("fs");
const path = require("path");

const root = process.cwd();
const specsDir = path.join(root, "specs");
const checklistIdPattern = /^- \[[ X]\]\s+([A-Z][A-Z0-9-]+-\d{3})\b/gm;

function listSpecFiles() {
  if (!fs.existsSync(specsDir)) return [];
  return fs
    .readdirSync(specsDir)
    .filter((n) => n.endsWith(".spec.md"))
    .map((n) => path.join(specsDir, n))
    .sort();
}

const errors = [];

for (const specPath of listSpecFiles()) {
  const text = fs.readFileSync(specPath, "utf8");
  const seen = new Map();
  let match;
  checklistIdPattern.lastIndex = 0;
  while ((match = checklistIdPattern.exec(text)) !== null) {
    const id = match[1];
    const line = text.slice(0, match.index).split(/\r?\n/).length;
    if (seen.has(id)) {
      errors.push(
        `${path.relative(root, specPath)}:${line} duplicate ID ${id} (first seen line ${seen.get(id)})`
      );
    } else {
      seen.set(id, line);
    }
  }
}

if (errors.length > 0) {
  for (const e of errors) console.error(`error: ${e}`);
  process.exit(1);
}

console.log("validate-spec-ids: OK");
