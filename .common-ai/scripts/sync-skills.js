#!/usr/bin/env node
// Sync canonical skills from .common-ai/skills/ to runtime stub locations
// under .claude/skills/, .codex/skills/, and .cursor/skills/.
//
// Each runtime stub keeps the canonical's frontmatter (so the runtime can
// discover the skill and trigger on it) plus a one-line body telling the
// agent to read the canonical file and follow its workflow. Bundled scripts
// live only under .common-ai/; the canonical agents/openai.yaml is copied
// to .codex/skills/<name>/agents/ because Codex reads it from there.
//
// Usage:
//   node .common-ai/scripts/sync-skills.js

const fs = require('fs');
const path = require('path');

const REPO_ROOT = path.resolve(__dirname, '..', '..');
const COMMON_SKILLS = path.join(REPO_ROOT, '.common-ai', 'skills');
const RUNTIME_TARGETS = ['.claude', '.codex', '.cursor'];

function extractFrontmatter(content) {
  const match = content.match(/^---\r?\n([\s\S]*?)\r?\n---\r?\n/);
  if (!match) {
    throw new Error('No YAML frontmatter found');
  }
  return match[1];
}

function buildStub(skillName, frontmatter) {
  return `---
${frontmatter}
---

The full instructions for this skill live in \`.common-ai/skills/${skillName}/SKILL.md\`. Read that file now and follow its workflow. Do not act on this stub alone — the canonical file is the authoritative source.
`;
}

function ensureDir(filePath) {
  fs.mkdirSync(path.dirname(filePath), { recursive: true });
}

function syncSkill(skillName) {
  const canonicalDir = path.join(COMMON_SKILLS, skillName);
  const canonicalSkillMd = path.join(canonicalDir, 'SKILL.md');
  if (!fs.existsSync(canonicalSkillMd)) {
    console.warn(`skip: ${skillName} (no canonical SKILL.md)`);
    return;
  }

  const content = fs.readFileSync(canonicalSkillMd, 'utf8');
  const frontmatter = extractFrontmatter(content);
  const stub = buildStub(skillName, frontmatter);

  for (const runtime of RUNTIME_TARGETS) {
    const target = path.join(REPO_ROOT, runtime, 'skills', skillName, 'SKILL.md');
    ensureDir(target);
    fs.writeFileSync(target, stub);
    console.log(`stub: ${path.relative(REPO_ROOT, target)}`);
  }

  const canonicalYaml = path.join(canonicalDir, 'agents', 'openai.yaml');
  if (fs.existsSync(canonicalYaml)) {
    const codexYaml = path.join(REPO_ROOT, '.codex', 'skills', skillName, 'agents', 'openai.yaml');
    ensureDir(codexYaml);
    fs.copyFileSync(canonicalYaml, codexYaml);
    console.log(`copy: ${path.relative(REPO_ROOT, codexYaml)}`);
  }
}

function main() {
  if (!fs.existsSync(COMMON_SKILLS)) {
    console.error(`No canonical skills directory at ${COMMON_SKILLS}`);
    process.exit(1);
  }

  const skills = fs
    .readdirSync(COMMON_SKILLS, { withFileTypes: true })
    .filter((d) => d.isDirectory())
    .map((d) => d.name);

  for (const name of skills) {
    syncSkill(name);
  }

  console.log(`\nSynced ${skills.length} skill(s) to .claude/, .codex/, and .cursor/.`);
}

main();
