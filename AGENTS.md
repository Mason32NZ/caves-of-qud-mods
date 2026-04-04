# AGENTS.md - Coding Agent Instructions

## IMPORTANT: Read Before Working on Caves of Qud Mods

**If you are working on a mod for Caves of Qud, you MUST read the `API.md` file first.**

The `API.md` file contains comprehensive documentation about:
- The Caves of Qud modding API
- Critical pitfalls and common mistakes
- GameObject, Part, and Event systems
- XML blueprint structures
- Proper C# patterns for CoQ modding

**This is essential context** before making any changes to CoQ-related code. The API documentation is based on decompiled game source code and wiki information.

---

## Build / Lint / Test Commands

```bash
# Install dependencies
npm install

# Development server (with hot reload)
npm run dev

# Production build
npm run build

# Run all tests
npm test

# Run a single test file
npm test -- path/to/test.spec.ts

# Run tests in watch mode
npm run test:watch

# Run tests with coverage
npm run test:coverage

# Lint all files
npm run lint

# Lint and auto-fix issues
npm run lint:fix

# Format code with Prettier
npm run format

# Type check without emitting
npm run type-check
```

## Code Style Guidelines

### Imports
- Use absolute imports with path aliases (e.g., `@/components`, `@/utils`)
- Group imports: 1) external libraries, 2) internal modules, 3) relative imports
- Sort imports alphabetically within groups
- Avoid default exports; prefer named exports for better tree-shaking

```typescript
// Good
import { useState } from 'react';
import { debounce } from 'lodash-es';

import { Button } from '@/components/Button';
import { formatDate } from '@/utils/date';

import { Helper } from './Helper';

// Bad - mixed groups, default export
import React from 'react';
import Helper from './Helper';
import { Button } from '@/components/Button';
```

### Formatting
- Use 2 spaces for indentation
- Max line length: 100 characters
- Use single quotes for strings
- Trailing commas in multi-line objects/arrays
- Semicolons are required

### Types & Naming
- Use TypeScript for all new code
- Prefer `interface` for object shapes, `type` for unions/aliases
- Use PascalCase for: types, interfaces, enums, classes, components
- Use camelCase for: variables, functions, methods, properties
- Use UPPER_SNAKE_CASE for: constants, enum values
- Use kebab-case for: file names (e.g., `my-component.tsx`)
- Prefix interfaces with `I` only when avoiding naming conflicts

```typescript
// Good
interface UserProfile {
  userId: string;
  displayName: string;
}

const MAX_RETRY_COUNT = 3;

function calculateTotal(items: CartItem[]): number {
  return items.reduce((sum, item) => sum + item.price, 0);
}
```

### Error Handling
- Use custom error classes for domain-specific errors
- Always handle Promise rejections; use `try/catch` with async/await
- Provide meaningful error messages with context
- Log errors with sufficient detail for debugging
- Never swallow errors silently

```typescript
// Good
async function fetchUser(id: string): Promise<User> {
  try {
    const response = await api.get(`/users/${id}`);
    return response.data;
  } catch (error) {
    if (error instanceof NotFoundError) {
      throw new UserNotFoundError(`User ${id} not found`);
    }
    logger.error('Failed to fetch user', { userId: id, error });
    throw new UserFetchError('Unable to retrieve user data');
  }
}
```

### Functions & Components
- Keep functions small and focused (max 50 lines ideally)
- Use early returns to reduce nesting
- Destructure props/parameters at the top
- Document public APIs with JSDoc comments

```typescript
// Good
interface ButtonProps {
  label: string;
  onClick: () => void;
  disabled?: boolean;
}

export function Button({ label, onClick, disabled = false }: ButtonProps): JSX.Element {
  if (disabled) {
    return <span className="btn-disabled">{label}</span>;
  }

  return (
    <button className="btn-primary" onClick={onClick}>
      {label}
    </button>
  );
}
```

### Testing
- Use descriptive test names: `should <expected behavior> when <condition>`
- Follow AAA pattern: Arrange, Act, Assert
- Mock external dependencies; test one thing per test
- Place tests next to source files or in `__tests__` directories

```typescript
// Good
it('should return discounted price when coupon is applied', () => {
  // Arrange
  const cart = createCart({ items: [{ price: 100 }] });
  const coupon = createCoupon({ discount: 0.2 });

  // Act
  const total = cart.applyCoupon(coupon);

  // Assert
  expect(total).toBe(80);
});
```

## Project Conventions

- Prefer immutable data; use `const`, spread operator, and methods like `map/filter/reduce`
- Avoid `any` type; use `unknown` when type is uncertain
- Use optional chaining (`?.`) and nullish coalescing (`??`)
- Commit messages: use conventional commits format
- Feature branches: `feature/description` or `fix/description`

## Temp Folder Usage

- **You can freely use the `temp/` folder** for any temporary files, working data, or outputs that should not be committed to version control
- The `temp/` folder is already in `.gitignore` and will not be tracked
- Common uses for the temp folder:
  - Decompiled game files from `ilspy` → `temp/decompiled/`
  - Intermediate build artifacts
  - Test data or scratch files
  - Debugging outputs

## M32's QoL Mod - Agent Guidelines

When working on the M32's QoL Caves of Qud mod:

### Naming Conventions
- **ALL C# class names MUST use the `M32_` prefix** for consistency:
  - ✅ `M32_Options`, `M32_PlayerMutator`, `M32_ZoneTradePart`
  - ❌ `M32QoLOptions`, `M32QoL_PlayerMutator`, `ZoneTradePart`

### PLAN.md Maintenance
- **You MUST keep the TODO section in `PLAN.md` up to date**
- Mark items as completed (`[x]`) when you finish them
- Add new features/tasks to the TODO as they are identified
- Update implementation details if code structure changes
- Use the TODO section to track progress and remaining work

### File Organization
- All mod files go in `M32_QoL/` directory
- Each feature should have its own toggle in `Options.xml`
- Each feature should have its own keybind in `Commands.xml`
- Main logic goes in appropriately named `.cs` files

### API Documentation Maintenance
- **When you fix errors and learn something new about the CoQ API, you MUST update `API.md`**
- Add the corrected information with clear examples showing what works vs. what doesn't
- Use comments like `// CORRECT` and `// WRONG` to make it obvious
- This helps future agents avoid the same mistakes
- Example: If you discover TradeEntry uses constructor parameters not property initializers, add that to the Trade API section

### Separation of Concerns: PLAN.md vs API.md

To avoid duplication and keep documentation organized, follow this separation:

**`PLAN.md`** — Project-specific implementation details:
- TODO lists and task tracking
- Feature specifications and behavior descriptions
- Project architecture and file organization
- Testing checklists
- High-level implementation approach (pseudocode/diagrams)
- Lessons learned at the conceptual level
- **Cross-references to API.md** for reusable code patterns

**`API.md`** — General Caves of Qud modding API reference:
- Reusable code patterns and examples
- API documentation for common operations
- Working solutions to common problems
- Critical pitfalls and best practices
- Complete working code examples that can be copied/pasted

**When adding content:**
- If it's a **general API pattern** that could help other mods → put it in `API.md`
- If it's **specific to this mod's implementation** → put it in `PLAN.md`
- If `PLAN.md` needs a code example that's already in `API.md` → use a cross-reference like:  
  "See `API.md` section 'TradeScreen — Force Close After Trade' for implementation"
- **Never duplicate** large code blocks between files — this creates maintenance burden

**Examples:**
- ✅ PLAN.md: "We use the pattern described in API.md's 'Multi-Merchant Trade' section"
- ✅ API.md: Full Harmony patch implementation with complete code example
- ❌ PLAN.md: Copy-pasting the full Harmony patch code that's already in API.md

### Harmony / Decompilation

**ALWAYS DECOMPILE BEFORE YOU PATCH.** When working on Harmony patches, you MUST use `ilspy` to inspect the actual game code before writing your patch. Do not guess. Do not assume. Decompile and verify.

- **Use `ilspy` BEFORE making any Harmony patch** — this is not optional
- **Use `ilspy` when uncertain about the API** — if you're not 100% sure about a method signature, field name, or class structure, decompile and check
- **Use `ilspy` to understand internal logic** — game systems often have unexpected behavior that you can only understand by reading the decompiled code
- **Use `ilspy` to debug patches that aren't working** — if your patch doesn't work as expected, decompile to see what the actual code does

Example: `ilspy --assembly "path/to/Assembly-CSharp.dll" --output "temp/decompiled/"`

**Decompiled output MUST go to `temp/decompiled/`** — use the project's temp folder to keep decompiled files organized and out of version control.

**Remember:** The API documentation in `API.md` is helpful, but it cannot capture every detail. When in doubt, decompile the actual game code and see for yourself.

**API.md is NOT infallible.** It is a reference, not the source of truth. The source of truth is the decompiled game code.

**You MUST update `API.md` when you discover:**
- New patterns or APIs not documented in `API.md`
- Information that conflicts with what's in `API.md`
- Better examples or clearer explanations than what exists
- Common pitfalls you encountered that aren't documented

**After using `ilspy`, ask yourself:** Did I learn something new? Is this in `API.md`? Is it accurate? If not, update `API.md` for future agents.

### File Access — You Have Full Read Access

**You have read access to ALL files you need.** This includes:

- **Mod files** — Read files in `M32_QoL/` directory anytime you need to understand the current implementation
- **Game files** — Read documents, search for patterns, and refer to existing working code in the codebase
- **Decompiled game assemblies** — Use `ilspy` to decompile and inspect game internals (Assembly-CSharp.dll, etc.)
- **Player.log** — Located at `%USERPROFILE%\AppData\LocalLow\Freehold Games\CavesOfQud\Player.log` on Windows

**When making changes:**
1. Read the relevant existing files first
2. Understand the current implementation before modifying
3. Search for similar patterns in existing code
4. Refer to `API.md` for documented patterns

**Do NOT operate in isolation.** Read files. Search for patterns. Understand before you write. And always decompile when working with Harmony patches.

### Debugging — Check Player.log When Issues Arise

**When the user reports an issue after testing, you MUST check Player.log.**

The Player.log file at `%USERPROFILE%\AppData\LocalLow\Freehold Games\CavesOfQud\Player.log` contains:
- Runtime errors and exceptions
- Stack traces with line numbers
- Debug output from the game
- Mod loading errors
- Harmony patch failures

**After the user tests and reports a problem:**
1. Read the Player.log file
2. Look for exceptions, errors, or warnings related to your changes
3. Search for your class names or mod ID in the log
4. Identify the root cause from the error messages
5. Fix the issue based on what the log tells you

**Do not guess at fixes.** Read the log. Understand what failed. Then fix it.
