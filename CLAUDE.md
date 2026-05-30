# Quartermaster Code Style Guide

## Language

- All code must be in English: variable names, class names, comments, API contracts
- Only user-facing display strings (labels, messages, UI text) may be in German
- The frontend display language is German; refer to `Quartermaster.Documentation/Translations.md` for term mappings

## Braces

- K&R style: opening `{` on the same line as the declaration/statement (types, methods, `if`/`else`/`for`/`foreach`/`while`/`switch`/`try`/`catch`/`finally`/`using`/`lock`, lambdas, initializers). The `.editorconfig` enforces this via `csharp_new_line_before_open_brace = none`.
- `else`/`catch`/`finally` sit on the line of the preceding `}`: `} else {`, `} catch (Exception ex) {`.

## If Statements

- Never write code on the same line as an if statement; the body must be on the next line
- If/for/foreach statements must always have braces `{}`, with one exception:
  - A single simple statement on the following line (no else) may omit braces **only if the if/for/foreach itself fits on one line**
  - If the condition or iterator spans multiple lines, braces are always required even for a single statement
- If any `else`/`else if` exists, all branches get braces

```csharp
// OK: single-line if, single simple statement, no else
if (value == null)
    return;

// OK: braces required when else is present
if (value == null) {
    return defaultValue;
} else {
    return value;
}

// OK: multiline foreach with braces (required because foreach is multiline)
foreach (var tag in new[] {
    "p", "br", "b", "i", "em", "strong"
}) {
    sanitizer.AllowedTags.Add(tag);
}

// WRONG: multiline if/for/foreach without braces
foreach (var tag in new[] {
    "p", "br", "b"
})
    sanitizer.AllowedTags.Add(tag);

// WRONG: no braces with else
if (value == null)
    return defaultValue;
else
    return value;

// WRONG: code on same line as if
if (value == null) return;
```

## Complex Conditionals

- If an `if` statement needs more than two lines for its conditions, extract it into a method using guard clauses
- Rule of thumb: simple conditions (e.g., null checks) can have up to 4 in one `if`; complex conditions should be extracted sooner
- The extracted method should use early returns (guard clauses) checking one condition at a time

```csharp
// OK: simple conditions, fits naturally
if (value != null && value.IsValid && items.Count > 0)
    Process(value);

// WRONG: too many complex conditions stacked in one if
if (div.ParentId.HasValue
    && parsedById.TryGetValue(div.ParentId.Value, out var parsedParent)
    && !string.IsNullOrEmpty(parsedParent.AdminCode)
    && existingByAdminCode.TryGetValue(parsedParent.AdminCode, out var dbParent)) {
    div.ParentId = dbParent.Id;
}

// RIGHT: extract into a method with guard clauses
private static Guid? ResolveDbParentId(AdministrativeDivision div, ...) {
    if (!div.ParentId.HasValue)
        return null;
    if (!parsedById.TryGetValue(div.ParentId.Value, out var parsedParent))
        return null;
    if (string.IsNullOrEmpty(parsedParent.AdminCode))
        return null;
    if (!existingByAdminCode.TryGetValue(parsedParent.AdminCode, out var dbParent))
        return null;
    return dbParent.Id;
}
```

## Tuples

- Tuples are capped at 3 values maximum
- For return types with more than 3 values, create a named class or record instead

## One Class Per File

- Never put two top-level classes or structs in one file
- Exceptions:
  - Pure data classes (only properties, no logic) — e.g., a DTO file can contain multiple related DTOs
  - A request class paired with its endpoint class (or similar narrow pairings)
  - Test files may contain multiple test classes when they cover the same region/feature of code (e.g., several validator test classes for the same feature, multiple endpoint test classes for one resource)
- Enums are not classes/structs and may coexist with a related class in the same file
- Nested types (inside a class) are fine

## No Region-Separator Comments

- Never write comments whose purpose is to visually separate sections of code (e.g., `// ---------- Users ----------` or `#region`)
- If code needs separation into visual groups, it usually needs to be split into separate methods or files instead
- Regular explanatory comments on specific lines/blocks are fine

## Comments

- Default to no comments. Don't restate what the code already says; don't narrate a fix or rule
- Prefer XML `///` summaries on public types/members over `//` narration; tighter visibility (`private`/`internal`) beats explaining a member
- Only add a comment when the *why* is non-obvious — a hidden constraint, a workaround, behavior that would surprise a reader. Include enough context that a future reader can judge whether the reason still applies

## Members and Language Features

- Always write the intended access modifier explicitly (`private`, `internal`, `public`) — never rely on implicit defaults
- Prefer `var` everywhere
- Prefer `using` directives over fully-qualified type names — add the `using` rather than writing `Quartermaster.Server.Cli.AdminInitCommand`. If a name genuinely collides, rename or fully-qualify at the single call site
- **No type/namespace aliases** (`using X = Y;`) and no `global::` — they paper over a naming or namespace problem rather than fixing it
- Use fluent/method-chain LINQ (`.Where().Select()`); never query-comprehension syntax (`from x in y select`). Applies to LinqToDB recursive CTEs too — express the recursive part as `self.SelectMany(prev => table.InnerJoin(...).Select(...))`
- Prefer LINQ `Count(predicate)` over `Where(predicate).Count()`
- Expression-bodied members for trivial properties/indexers/accessors are fine; not for methods/constructors (matches `.editorconfig`)
- Avoid expression-bodied properties (`=>`) when the value involves allocation or `GetType()`/`typeof().Name` — use an auto-property with initializer (`{ get; } = ...`) so it's computed once
- **One statement per line** — never combine declarations or statements with `;` on the same line
- **Method calls:** no space between the method name and `(` — write `Method()`, not `Method ()`
- **Extension members:** prefer the C# 14 `extension(...) { … }` block syntax over the legacy `this`-parameter form when a class holds more than one extension on the same receiver — the receiver is named once, the members are grouped, and the shape mirrors a regular type. A single extension on a one-off receiver can stay on the legacy `static Foo(this T x)` form; there's no real readability gain from a one-member block.

## Switch, Boolean, and Pattern Matching

- Never put curly braces on `case` blocks. If local variable names collide between cases, rename them
- Use `&&` and `||`, never the `and`/`or` pattern-matching keywords — they're harder to scan. Applies to pattern-matching expressions too: write `x == A || x == B`, not `x is A or B`
- Null checks use `== null` / `!= null`, never the "is empty pattern" form: write `if (x != null)`, not `if (x is { } y)` or `if (x is not null)`. Pattern-matching syntax for what's conceptually a null check is unreadable. If you need to capture the unwrapped value, use a `var` after the check (or `.Value` on the nullable)

## Exceptions

- Never use a bare `catch` or `catch (Exception)` without logging. Always catch `Exception ex` and log the full exception (`{ex}`, never just `{ex.Message}`, which drops the stack and inner exceptions)
- Reserve exceptions for genuine bugs and adapter-layer translation; don't use them for expected control flow

## Locking

- This project is .NET 10. Use `System.Threading.Lock` instead of `object` as a lock target — `.editorconfig` has `csharp_prefer_system_threading_lock = true`

## Blazor Components

- Never use `@code { }` blocks in `.razor` files; always use a code-behind file (`.razor.cs`)
- This applies to all components and pages — keep markup and logic separated

## DTO ↔ Entity Mapping

- All entity-to-DTO and DTO-to-entity mapping is **hand-written and inline** at the call site
- Do not introduce mapping libraries (Mapperly, AutoMapper, etc.)
- Do not extract single-purpose `*Mapper` helper classes either — keep the mapping visible where the data is shaped
- The visibility tradeoff is intentional: a few extra `new XxxDTO { ... }` blocks at the use site are easier to review than indirection through a mapper, and code review catches missing/wrong properties

## Documentation

- Never write into README.md files; use `Quartermaster.Documentation/` directory instead
- Implementation plans go in `Quartermaster.Documentation/plans/`

## Commits

- Never commit changes without explicit user request
- Never run any git command (commit, init, add, push, status, etc.) without an explicit user request

## Migrations

- One migration per release. While pre-production, fold new schema changes into the existing in-flight migration rather than adding a new one

## Self-Review Before Declaring Done

- After every code change, sweep the diff against the rules in this file (especially using-directives vs. fully-qualified types, brace style, explicit access modifiers, no `@code` in `.razor`) before reporting the task complete
