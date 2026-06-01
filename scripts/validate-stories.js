#!/usr/bin/env node
// Verify top-level stories follow NN-slug.story_TODO.md or .story_DONE.md
// and that the filename status matches a 'status:' frontmatter field if present.
// Pre-existing topical subfolders under stories/ are ignored.

const fs = require("fs");
const path = require("path");

const root = process.cwd();
const storiesDir = path.join(root, "stories");
const pattern = /^(\d{2})-[a-z0-9]+(?:-[a-z0-9]+)*\.story_(TODO|DONE|IN_PROGRESS|COMPLETE)\.md$/;

if (!fs.existsSync(storiesDir)) {
  console.log("validate-stories: no stories/ directory; skipping");
  process.exit(0);
}

const errors = [];
const entries = fs
  .readdirSync(storiesDir, { withFileTypes: true })
  .filter((d) => d.isFile() && d.name.endsWith(".md"))
  .map((d) => d.name)
  .filter((n) => n !== "00-story-map.md" && !n.startsWith("_"));

const seenNumbers = new Map();

for (const name of entries) {
  const m = pattern.exec(name);
  if (!m) {
    errors.push(`stories/${name}: does not match NN-slug.story_STATUS.md`);
    continue;
  }
  const [, num, status] = m;
  if (seenNumbers.has(num)) {
    errors.push(
      `stories/${name}: duplicate story number ${num} (also ${seenNumbers.get(num)})`
    );
  } else {
    seenNumbers.set(num, name);
  }

  const text = fs.readFileSync(path.join(storiesDir, name), "utf8");
  const fmStatusMatch = /^status:\s*(\w+)\s*$/m.exec(text.split(/\n---\n/)[0] || "");
  if (fmStatusMatch) {
    const fmStatus = fmStatusMatch[1].toUpperCase();
    const fileStatus = status.toUpperCase();
    if (fmStatus !== fileStatus) {
      errors.push(
        `stories/${name}: filename status (${fileStatus}) does not match frontmatter status (${fmStatus})`
      );
    }
  }
}

if (errors.length > 0) {
  for (const e of errors) console.error(`error: ${e}`);
  process.exit(1);
}

console.log(`validate-stories: OK (${entries.length} top-level stories)`);
