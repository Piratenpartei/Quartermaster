# Quartermaster Code Style Guide

## Language

- All code must be in English: variable names, class names, comments, API contracts
- Only user-facing display strings (labels, messages, UI text) may be in German
- The frontend display language is German; refer to `Quartermaster.Documentation/Translations.md` for term mappings

## If Statements

- Never write code on the same line as an if statement; the body must be on the next line
- If statements must always have braces `{}`, with one exception:
  - A single simple statement on the following line (no else) may omit braces

```csharp
// OK: single simple statement, no else
if (value == null)
    return;

// OK: braces required when else is present
if (value == null) {
    return defaultValue;
} else {
    return value;
}

// WRONG: no braces with else
if (value == null)
    return defaultValue;
else
    return value;

// WRONG: code on same line as if
if (value == null) return;
```

## Documentation

- Never write into README.md files; use `Quartermaster.Documentation/` directory instead
- Implementation plans go in `Quartermaster.Documentation/plans/`

## Commits

- Never commit changes without explicit user request
