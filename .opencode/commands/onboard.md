---
description: Onboards a new agent to the project by reading all markdown documentation files, source files, and exploring directory structures
agent: plan
---

Read all markdown files in the repository root to understand the project context, then provide a brief summary of:

1. **Project Overview**: What is this project about?
2. **Key Conventions**: What coding rules and naming conventions must be followed?
3. **Current State**: What features are being worked on right now?
4. **Technical Context**: What APIs, patterns, or dependencies should be known?

Here are the markdown files in the repository root:

!find . -maxdepth 1 -name "*.md" -type f | head -20

Read each file and summarize the key information.

---

After completing the documentation review, **read all other source files** in the repository to understand the current implementation:

1. List all tracked files (excluding markdown files, respecting .gitignore):
   !git ls-files | grep -v "\.md$" | head -50

2. Also check for any untracked files that aren't ignored:
   !git ls-files --others --exclude-standard | grep -v "\.md$" | head -20

3. Read the main source files (e.g., `.cs`, `.xml`, `.csproj`, `.json`, `.js`, `.ts`, `.py`, etc.) to understand:
   - Current code structure and architecture
   - Existing implementations and patterns
   - Configuration files and their purposes
   - Any scripts or build files

**Note**: Only read files that are tracked by git or not ignored by .gitignore. Do not read files in directories like `node_modules/`, `temp/`, build output folders, or other ignored paths.

---

Finally, **explore all directories** you have access to (both inside and outside the repository) and get their file structures:

1. List all directories in the repo (excluding ignored paths):
   !git ls-files | xargs -I {} dirname {} | sort -u | grep -v "^\.$"

2. For each directory, note:
   - Directory purpose (from its name and contents)
   - Key files it contains
   - Relationship to other directories

3. List directories outside the repo that you can access (e.g., game directories, shared resources):
   !ls -la ~ 2>/dev/null || dir %USERPROFILE% 2>nul || echo "Note accessible directories here"

4. Summarize the file structure knowledge so you know where to find resources if needed later.

---

**Final Output**: Provide a comprehensive onboarding summary covering:
- Project documentation overview
- Current implementation state
- Directory/file structure map for quick reference
