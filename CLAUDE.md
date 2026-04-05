# Quartermaster Code Style Guide

## Language

- All code must be in English: variable names, class names, comments, API contracts
- Only user-facing display strings (labels, messages, UI text) may be in German
- The frontend display language is German; refer to `Quartermaster.Documentation/Translations.md` for term mappings

## If Statements

- Never write code on the same line as an if statement; the body must be on the next line
- If/for/foreach statements must always have braces `{}`, with one exception:
  - A single simple statement on the following line (no else) may omit braces **only if the if/for/foreach itself fits on one line**
  - If the condition or iterator spans multiple lines, braces are always required even for a single statement

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
- Enums are not classes/structs and may coexist with a related class in the same file
- Nested types (inside a class) are fine

## No Region-Separator Comments

- Never write comments whose purpose is to visually separate sections of code (e.g., `// ---------- Users ----------` or `#region`)
- If code needs separation into visual groups, it usually needs to be split into separate methods or files instead
- Regular explanatory comments on specific lines/blocks are fine

## Blazor Components

- Never use `@code { }` blocks in `.razor` files; always use a code-behind file (`.razor.cs`)
- This applies to all components and pages — keep markup and logic separated

## Documentation

- Never write into README.md files; use `Quartermaster.Documentation/` directory instead
- Implementation plans go in `Quartermaster.Documentation/plans/`

## Commits

- Never commit changes without explicit user request
