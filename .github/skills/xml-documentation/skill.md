---
name: xml-documentation
description: 'Add XML documentation comments to C# classes, interfaces, methods, properties, fields, and constructors. Use when asked to document C# code, add XML comments, improve code documentation, or when generating new C# code.'
---

# XML Documentation

Add XML documentation comments to all C# types and members in the codebase.

## When to Use This Skill

- When asked to document C# code
- When asked to add XML comments
- When generating new C# code
- When improving code documentation

## Prompts

Use this skill when the user says:
- "Add XML documentation to this file/project"
- "Document all classes and methods"
- "Add XML comments to the codebase"
- "Generate XML documentation"
- "Add doc comments"

## Requirements

Always add XML documentation comments to the following:

- **Classes**
- **Interfaces**
- **Methods**
- **Properties**
- **Member variables (fields)**
- **Constructors** (when using factory methods, builder patterns, or any non-class-style constructor pattern)
- **Records**
- **Enums**
- **Delegates**

### Tags to Use

- Use `<summary>` for all elements
- Use `<param>` tags to document method/factory parameters
- Use `<returns>` tags to document return values
- Use `<typeparam>` tags for generic type parameters
- Use `<see cref="..."/>` to reference other types or members
- Use `<remarks>` for additional context or implementation notes
- Use `<exception cref="...">` to document thrown exceptions

### Guidelines

- Keep descriptions concise but informative
- Document the purpose, not the implementation
- Use third-person imperative style (e.g., "Gets or sets", "Initializes", "Represents")
- For constructors: use "Initializes a new instance of the <see cref="ClassName"/> class"

## Example

```csharp
/// <summary>
/// Represents a user account in the system.
/// </summary>
public class User
{
    /// <summary>
    /// The unique identifier for the user.
    /// </summary>
    private Guid _id;

    /// <summary>
    /// Gets or sets the display name of the user.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="User"/> class.
    /// </summary>
    /// <param name="displayName">The display name for the user.</param>
    public User(string displayName)
    {
        DisplayName = displayName;
    }
}

/// <summary>
/// Creates a new user with default settings.
/// </summary>
/// <param name="name">The name of the user.</param>
/// <returns>A new <see cref="User"/> instance.</returns>
public static User CreateUser(string name)
{
    return new User(name);
}
```

## Step-by-Step Workflow

1. **Scan the file** for all types and members lacking XML documentation
2. **Add `<summary>`** to every class, interface, struct, enum, and record
3. **Add `<summary>`** to every method, property, and field
4. **Add `<param>`** tags for each method parameter
5. **Add `<returns>`** tags for methods with non-void return types
6. **Add `<exception>`** tags where exceptions are thrown
7. **Add `<typeparam>`** tags for generic type parameters
8. **Use `<see cref="..."/>`** when referencing other types
9. **Verify** no undocumented public or internal members remain

## Scope

Apply to all `.cs` files in the project:
- Library code (`Oasis.Resilience/`)
- Demo projects (`Demo/`)
- Example projects (`ResilienceWithAop/`, `ResilienceWithAkka/`)
