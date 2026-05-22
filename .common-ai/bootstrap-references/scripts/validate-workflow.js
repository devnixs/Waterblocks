#!/usr/bin/env node

const fs = require("fs");
const path = require("path");

const root = process.cwd();
const specsDir = path.join(root, "specs");
const storiesDir = path.join(root, "stories");
const storyMapPath = path.join(storiesDir, "00-story-map.md");

const storyFilePattern =
  /^(\d{2})-[a-z0-9]+(?:-[a-z0-9]+)*\.story_(TODO|IN_PROGRESS|COMPLETE)\.md$/;
const genericSpecIdPattern = /\b[A-Z][A-Z0-9]+-\d{3}\b/g;
const specChecklistPattern = /^- \[[ X]\] (?:([A-Z][A-Z0-9]+(?:-[A-Z0-9]+)?-\d{3}): )?/gm;

const requiredNewStorySections = [
  "Goal",
  "Scope",
  "Relevant Specs",
  "Acceptance Notes",
  "Playwright Test",
  "Browser Test",
  "E2E Regression",
  "Completion Rule",
];

const errors = [];
const warnings = [];

function rel(filePath) {
  return path.relative(root, filePath);
}

function readText(filePath) {
  return fs.readFileSync(filePath, "utf8");
}

function listMarkdownFiles(dir) {
  if (!fs.existsSync(dir)) {
    return [];
  }

  return fs
    .readdirSync(dir)
    .filter((name) => name.endsWith(".md"))
    .sort()
    .map((name) => path.join(dir, name));
}

function listMarkdownFilesRecursive(dir) {
  if (!fs.existsSync(dir)) {
    return [];
  }

  const files = [];
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const fullPath = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      files.push(...listMarkdownFilesRecursive(fullPath));
    } else if (entry.isFile() && entry.name.endsWith(".md")) {
      files.push(fullPath);
    }
  }

  return files.sort();
}

function addError(message) {
  errors.push(message);
}

function addWarning(message) {
  warnings.push(message);
}

function collectSpecPrefixes() {
  const indexPath = path.join(specsDir, "index.md");
  if (!fs.existsSync(indexPath)) {
    return [];
  }

  return Array.from(
    new Set(
      Array.from(
        readText(indexPath).matchAll(/ID prefix: `([A-Z][A-Z0-9]+)`/g),
        (match) => match[1]
      )
    )
  );
}

function listSpecFiles() {
  return listMarkdownFiles(specsDir).filter((file) => file.endsWith(".spec.md"));
}

function parseFrontmatter(text) {
  if (!text.startsWith("---\n")) {
    return null;
  }

  const end = text.indexOf("\n---\n", 4);
  if (end === -1) {
    return null;
  }

  const raw = text.slice(4, end).trimEnd();
  const data = {};
  let currentListKey = null;

  for (const line of raw.split(/\r?\n/)) {
    const scalar = /^([a-z_]+):(?: (.*))?$/.exec(line);
    if (scalar) {
      const [, key, value = ""] = scalar;
      if (value === "") {
        data[key] = [];
        currentListKey = key;
      } else if (value === "[]") {
        data[key] = [];
        currentListKey = null;
      } else {
        data[key] = value;
        currentListKey = null;
      }
      continue;
    }

    const listItem = /^  - (.+)$/.exec(line);
    if (listItem && currentListKey) {
      data[currentListKey].push(listItem[1]);
      continue;
    }
  }

  return data;
}

function validateSpecFrontmatter() {
  const specFiles = listSpecFiles();
  const specNames = new Set(specFiles.map(rel));
  const seenAreas = new Map();
  const validOwners = new Set(["frontend", "backend", "product", "qa", "devops"]);
  const validStatuses = new Set(["active", "draft", "deprecated", "archived"]);

  for (const specPath of specFiles) {
    const text = readText(specPath);
    const frontmatter = parseFrontmatter(text);
    const specName = rel(specPath);

    if (!frontmatter) {
      addError(`${specName} is missing YAML frontmatter`);
      continue;
    }

    if (!frontmatter.area || typeof frontmatter.area !== "string") {
      addError(`${specName} frontmatter is missing area`);
    } else if (seenAreas.has(frontmatter.area)) {
      addError(
        `${specName} frontmatter area ${frontmatter.area} duplicates ${seenAreas.get(frontmatter.area)}`
      );
    } else {
      seenAreas.set(frontmatter.area, specName);
    }

    if (!Array.isArray(frontmatter.owners) || frontmatter.owners.length === 0) {
      addError(`${specName} frontmatter must list at least one owner`);
    } else {
      for (const owner of frontmatter.owners) {
        if (!validOwners.has(owner)) {
          addError(`${specName} frontmatter has unknown owner ${owner}`);
        }
      }
    }

    if (!validStatuses.has(frontmatter.status)) {
      addError(`${specName} frontmatter has invalid status ${frontmatter.status}`);
    }

    if (!Array.isArray(frontmatter.depends_on)) {
      addError(`${specName} frontmatter must include depends_on`);
    } else {
      for (const dependency of frontmatter.depends_on) {
        if (!specNames.has(dependency)) {
          addError(`${specName} depends_on references missing spec ${dependency}`);
        }
        if (dependency === specName) {
          addError(`${specName} depends_on must not reference itself`);
        }
      }
    }
  }
}

function createSpecIdPattern(prefixes) {
  if (prefixes.length === 0) {
    return genericSpecIdPattern;
  }

  return new RegExp(`\\b(?:${prefixes.join("|")})-\\d{3}\\b`, "g");
}

function collectSpecIds() {
  const ids = new Map();
  const missingIdItems = [];

  for (const specPath of listSpecFiles()) {
    const text = readText(specPath);
    let match;
    while ((match = specChecklistPattern.exec(text)) !== null) {
      const id = match[1];
      const line =
        text.slice(0, match.index).split(/\r?\n/).length;
      if (!id) {
        missingIdItems.push(`${rel(specPath)}:${line}`);
        continue;
      }

      if (ids.has(id)) {
        addError(
          `Duplicate spec ID ${id} in ${rel(specPath)} and ${ids.get(id)}`
        );
      } else {
        ids.set(id, rel(specPath));
      }
    }
  }

  if (missingIdItems.length > 0) {
    addWarning(
      `Spec checklist items without stable IDs: ${missingIdItems
        .slice(0, 10)
        .join(", ")}${missingIdItems.length > 10 ? `, +${missingIdItems.length - 10} more` : ""}`
    );
  }

  return ids;
}

function validateStories(specIds, specIdPattern) {
  const storyFiles = listMarkdownFilesRecursive(storiesDir).filter(
    (file) =>
      path.basename(file) !== "00-story-map.md" &&
      !path.basename(file).startsWith("_") &&
      !rel(file).startsWith("stories/archive/README.md")
  );
  const storyNumbers = new Map();
  const normalizedStoryNames = [];

  for (const storyPath of storyFiles) {
    const fileName = path.basename(storyPath);
    const match = storyFilePattern.exec(fileName);
    const text = readText(storyPath);

    if (!match) {
      addError(
        `${rel(storyPath)} does not match NN-slug.story_STATUS.md`
      );
      continue;
    }

    const [, number, status] = match;
    if (storyNumbers.has(number)) {
      addError(
        `Duplicate story number ${number}: ${storyNumbers.get(number)} and ${rel(storyPath)}`
      );
    } else {
      storyNumbers.set(number, rel(storyPath));
    }

    normalizedStoryNames.push(rel(storyPath));

    const hasExactSpecIds = (text.match(specIdPattern) || []).length > 0;
    const isActive = status !== "COMPLETE";

    if (isActive || hasExactSpecIds) {
      for (const section of requiredNewStorySections) {
        if (!text.includes(`## ${section}`)) {
          addError(`${rel(storyPath)} is missing ## ${section}`);
        }
      }
    }

    if (status === "COMPLETE") {
      const unchecked = text.match(/^- \[ \]/gm);
      if (unchecked) {
        addError(
          `${rel(storyPath)} is COMPLETE but still has ${unchecked.length} unchecked item(s)`
        );
      }
    }

    if (hasExactSpecIds) {
      const referencedIds = Array.from(new Set(text.match(specIdPattern) || []));
      for (const id of referencedIds) {
        if (!specIds.has(id)) {
          addError(`${rel(storyPath)} references unknown spec ID ${id}`);
        }
      }
    } else if (isActive) {
      addWarning(
        `${rel(storyPath)} is active but does not reference exact spec IDs`
      );
    }
  }

  return normalizedStoryNames;
}

function validateStoryMap(storyNames) {
  if (!fs.existsSync(storyMapPath)) {
    addError("stories/00-story-map.md is missing");
    return;
  }

  const text = readText(storyMapPath);
  const listed = Array.from(
    text.matchAll(/`(stories\/[^`]+\.md)`/g),
    (match) => match[1]
  ).filter(
    (name) =>
      name !== "stories/00-story-map.md" &&
      name !== "stories/archive/README.md"
  );

  const actual = new Set(storyNames);
  const listedSet = new Set(listed);

  for (const story of storyNames) {
    if (!listedSet.has(story)) {
      addError(`stories/00-story-map.md does not list ${story}`);
    }
  }

  for (const story of listed) {
    if (!actual.has(story)) {
      addError(`stories/00-story-map.md lists missing story ${story}`);
    }
  }
}

function main() {
  if (!fs.existsSync(specsDir)) {
    addError("specs/ directory is missing");
  }
  if (!fs.existsSync(storiesDir)) {
    addError("stories/ directory is missing");
  }

  const specPrefixes = collectSpecPrefixes();
  const specIdPattern = createSpecIdPattern(specPrefixes);
  validateSpecFrontmatter();
  const specIds = collectSpecIds();
  const storyNames = validateStories(specIds, specIdPattern);
  validateStoryMap(storyNames);

  for (const warning of warnings) {
    console.warn(`warning: ${warning}`);
  }

  if (errors.length > 0) {
    for (const error of errors) {
      console.error(`error: ${error}`);
    }
    process.exit(1);
  }

  console.log(
    `Workflow validation passed (${specIds.size} spec IDs, ${storyNames.length} stories).`
  );
}

main();
