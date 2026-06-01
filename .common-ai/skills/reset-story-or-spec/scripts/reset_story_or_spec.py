#!/usr/bin/env python3
"""
Reset story or spec files to their unchecked / TODO state.

Usage:
  python reset_story_or_spec.py story <path-or-number>
      Unchecks all checkboxes and renames the file to use the _TODO suffix.
      Accepts a full path or just a story number (e.g. "12" or "05").

  python reset_story_or_spec.py spec <path-or-number>
      Unchecks all checkboxes in the given spec file.
      Accepts a full path or just a spec number / name fragment.

  python reset_story_or_spec.py all-specs
      Unchecks all checkboxes in every spec file under specs/.

  python reset_story_or_spec.py all-stories
      Unchecks all checkboxes in every story file and marks them all as _TODO.
"""

import argparse
import glob
import os
import re
import sys

STORIES_DIR = "stories"
SPECS_DIR = "specs"

# Matches any checkbox state: [ ], [x], [X]
CHECKBOX_PATTERN = re.compile(r"\[[ xX]\]")


def uncheck_all(content: str) -> str:
    return CHECKBOX_PATTERN.sub("[ ]", content)


def story_suffix(path: str) -> str:
    """Extract the suffix token (e.g. 'COMPLETE', 'TODO') from a story path."""
    m = re.search(r"\.story_([A-Z]+)\.md$", path, re.IGNORECASE)
    return m.group(1).upper() if m else ""


def to_todo_path(path: str) -> str:
    """Return the _TODO variant of a story path, regardless of current suffix."""
    # Replace any existing .story_XXX.md suffix
    new_path = re.sub(r"\.story_[A-Za-z]+\.md$", ".story_TODO.md", path)
    if new_path == path:
        # No recognised suffix — append _TODO before .md
        new_path = re.sub(r"\.md$", ".story_TODO.md", path)
    return new_path


def resolve_story(token: str) -> str:
    """Resolve a story token (number string or path) to an existing file path."""
    if os.path.isfile(token):
        return token

    # Treat as a number prefix — find matching file in STORIES_DIR
    pattern = os.path.join(STORIES_DIR, f"{token.zfill(2)}-*.md")
    matches = glob.glob(pattern)
    # Also try without zero-padding
    matches += glob.glob(os.path.join(STORIES_DIR, f"{token}-*.md"))
    matches = list(dict.fromkeys(matches))  # deduplicate, preserve order

    if not matches:
        sys.exit(
            f"Error: no story file found for '{token}'. "
            f"Looked in '{STORIES_DIR}/' with pattern '{token.zfill(2)}-*.md'."
        )
    if len(matches) > 1:
        sys.exit(
            f"Error: ambiguous story token '{token}' matched multiple files:\n"
            + "\n".join(f"  {m}" for m in matches)
        )
    return matches[0]


def resolve_spec(token: str) -> str:
    """Resolve a spec token (number string, name fragment, or path) to an existing file."""
    if os.path.isfile(token):
        return token

    # Try zero-padded number prefix
    pattern = os.path.join(SPECS_DIR, f"{token.zfill(2)}-*.md")
    matches = glob.glob(pattern)
    matches += glob.glob(os.path.join(SPECS_DIR, f"{token}-*.md"))
    # Also allow partial name match
    matches += glob.glob(os.path.join(SPECS_DIR, f"*{token}*.md"))
    matches = list(dict.fromkeys(matches))

    if not matches:
        sys.exit(
            f"Error: no spec file found for '{token}'. "
            f"Looked in '{SPECS_DIR}/' for '{token}'."
        )
    if len(matches) > 1:
        sys.exit(
            f"Error: ambiguous spec token '{token}' matched multiple files:\n"
            + "\n".join(f"  {m}" for m in matches)
            + "\nPlease be more specific."
        )
    return matches[0]


def reset_story_file(path: str):
    path = os.path.realpath(path)
    with open(path, "r", encoding="utf-8") as f:
        content = f.read()

    new_content = uncheck_all(content)
    new_path = to_todo_path(path)

    with open(new_path, "w", encoding="utf-8") as f:
        f.write(new_content)

    if new_path != path:
        os.remove(path)
        print(f"Renamed + reset: {os.path.basename(path)} → {os.path.basename(new_path)}")
    else:
        print(f"Reset (already _TODO): {os.path.basename(path)}")


def reset_spec_file(path: str):
    path = os.path.realpath(path)
    with open(path, "r", encoding="utf-8") as f:
        content = f.read()

    new_content = uncheck_all(content)
    with open(path, "w", encoding="utf-8") as f:
        f.write(new_content)

    print(f"Reset: {os.path.basename(path)}")


def reset_all_specs():
    spec_files = sorted(glob.glob(os.path.join(SPECS_DIR, "*.md")))
    if not spec_files:
        sys.exit(f"Error: no spec files found in '{SPECS_DIR}/'.")
    for path in spec_files:
        reset_spec_file(path)


def reset_all_stories():
    story_files = sorted(glob.glob(os.path.join(STORIES_DIR, "*.md")))
    # Exclude the story map
    story_files = [p for p in story_files if "story-map" not in os.path.basename(p)]
    if not story_files:
        sys.exit(f"Error: no story files found in '{STORIES_DIR}/'.")
    for path in story_files:
        reset_story_file(path)


def main():
    parser = argparse.ArgumentParser(
        description="Reset story or spec files to their unchecked / TODO state."
    )
    subparsers = parser.add_subparsers(dest="command", required=True)

    p_story = subparsers.add_parser("story", help="Reset a single story file.")
    p_story.add_argument(
        "target", help="Story number (e.g. '12') or full file path."
    )

    p_spec = subparsers.add_parser("spec", help="Reset a single spec file.")
    p_spec.add_argument(
        "target", help="Spec number/name fragment (e.g. '03') or full file path."
    )

    subparsers.add_parser("all-specs", help="Reset all spec files.")
    subparsers.add_parser("all-stories", help="Reset all story files.")

    args = parser.parse_args()

    if args.command == "story":
        path = resolve_story(args.target)
        reset_story_file(path)
    elif args.command == "spec":
        path = resolve_spec(args.target)
        reset_spec_file(path)
    elif args.command == "all-specs":
        reset_all_specs()
    elif args.command == "all-stories":
        reset_all_stories()


if __name__ == "__main__":
    main()
