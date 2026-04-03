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
- **You can use the `ilspy` tool to decompile game EXEs or DLLs** when working on Harmony patches
- Use ILSpy to inspect the game's internal methods, classes, and logic to understand what to patch
- This is especially useful when you need to:
  - Find the exact method signature to target with a Harmony patch
  - Understand the internal logic of a game system before patching it
  - Debug why a patch isn't working as expected
- Example: `ilspy --assembly "path/to/Assembly-CSharp.dll" --output "decompiled.cs"`
- **Decompiled output should go to `temp/decompiled/`** — use the project's temp folder to keep decompiled files organized and out of version control

### Debugging Resources
You have access to the following resources whenever you need them:
- **Player.log** — Located at `%USERPROFILE%\AppData\LocalLow\Freehold Games\CavesOfQud\Player.log` on Windows. Check this for runtime errors, exceptions, and debug output from the game
- **Game mod files** — You can read and search through existing mod files in the `M32_QoL/` directory
- **Game files** — You can decompile game assemblies (Assembly-CSharp.dll, etc.) using `ilspy` to inspect internal implementations
- **API.md** — Keep this up to date with any new patterns you discover

Use these resources proactively to understand issues, verify implementation details, and find working code patterns.
