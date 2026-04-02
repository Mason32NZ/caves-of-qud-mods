---
description: Onboards a new agent to the project by reading all markdown documentation files
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
