---
name: formatting
description: 'Formats C# and JSON files in the repository: removes unused using statements, fixes indentation (4 spaces), and ensures coding conventions. Use when asked to format code, clean up using statements, fix indentation, or tidy up the codebase.'
---

# Code Formatting Skill

## Description

This skill formats code files in the repository to ensure:
- Unused `using` statements are removed
- Indentation is consistent (4 spaces for C# files)
- JSON files are properly formatted
- All files follow the project's coding conventions

## Trigger

This skill is triggered when the user requests:
- "Format the code"
- "Clean up using statements"
- "Fix indentation"
- "Tidy up the code"
- "Remove unused imports"

## Prerequisites

- `dotnet-format` CLI tool (optional, for automatic formatting)
- PowerShell for file processing

## Steps

### 1. Remove Unused Using Statements

For each `.cs` file, remove `using` statements that are not used in the file.

```powershell
# Find CS files excluding obj, bin, TestResults
$files = Get-ChildItem -Path "." -Recurse -Filter *.cs | Where-Object { $_.FullName -notmatch "obj|bin|TestResults|ReferenceAssemblies" }

foreach ($file in $files) {
    Write-Output "Processing: $($file.Name)"
    # Read file content
    $content = Get-Content $file.FullName -Raw
    # Simple heuristic: check for unused usings (this is basic, IDE is better)
    # For production, use `dotnet format` or IDE
}
```

### 2. Fix C# Indentation

Ensure consistent 4-space indentation:

```powershell
# For C# files, ensure proper indentation
# This is a basic approach - use `dotnet format` for better results

# Example: Convert tabs to spaces (4 spaces)
$files = Get-ChildItem -Path "." -Recurse -Filter *.cs -Exclude "*obj*","*bin*","*TestResults*"
foreach ($file in $files) {
    $content = Get-Content $file.FullName -Raw
    # Replace tabs with 4 spaces
    $content = $content -replace "`t", "    "
    Set-Content -Path $file.FullName -Value $content -NoNewline
}
```

### 3. Format JSON Files

For `.json` files (like `.csproj`, `.sln`):

```powershell
# Format JSON files with proper indentation
$files = Get-ChildItem -Path "." -Recurse -Filter *.json -Exclude "*obj*","*bin*","*TestResults*"
foreach ($file in $files) {
    try {
        $json = Get-Content $file.FullName -Raw | ConvertFrom-Json
        $formatted = $json | ConvertTo-Json -Depth 100
        Set-Content -Path $file.FullName -Value $formatted
    } catch {
        Write-Output "Skipping invalid JSON: $($file.Name)"
    }
}
```

### 4. Verify Coding Conventions

Check that files follow the project conventions:

```powershell
# Check that using statements are under namespace (not above)
$files = Get-ChildItem -Path "Oasis.Resilience" -Recurse -Filter *.cs -Exclude "*obj*","*bin*"
foreach ($file in $files) {
    $lines = Get-Content $file.FullName
    $content = Get-Content $file.FullName -Raw
    # Check if using is before namespace
    if ($content -match "using .+;\s+namespace") {
        Write-Output "WARNING: Using before namespace in: $($file.Name)"
    }
}
```

## Recommended Tools

For production use, install and use:

```bash
# Install dotnet-format
dotnet tool install -g dotnet-format

# Run formatting
dotnet format

# Or with specific options
dotnet format --verbosity diagnostic
```

## Example Usage

**User**: "Format the code and clean up using statements"

**Assistant**:
1. Runs formatting check on all `.cs` files
2. Removes unused `using` statements
3. Fixes indentation (4 spaces)
4. Formats JSON files
5. Reports files that were modified

## Notes

- **Best approach**: Use `dotnet format` CLI tool for reliable results
- **Basic heuristic**: The PowerShell approach is basic; IDEs do this better
- **Unused usings**: Hard to detect without compilation; use IDE or Roslyn analyzers
- **Namespace convention**: This project uses `using` statements **inside** the namespace block

## File Output

The skill modifies files in place. No separate output file is generated.

## Common Fixes Applied

### 1. Using Statements Inside Namespace

```csharp
// WRONG
using System;
namespace MyApp { }

// CORRECT (project convention)
namespace MyApp {
    using System;
}
```

### 2. Remove Unused Usings

```csharp
// BEFORE
namespace MyApp {
    using System;     // Used
    using System.Linq; // NOT used - remove
    using System.Collections.Generic; // Used
}

// AFTER
namespace MyApp {
    using System;
    using System.Collections.Generic;
}
```

### 3. Consistent Indentation

```csharp
// WRONG (tabs)
namespace MyApp {
	class Program {
		void Method() { }
	}
}

// CORRECT (4 spaces)
namespace MyApp {
    class Program {
        void Method() { }
    }
}
```

## Error Handling

If formatting fails:
- Check that files are not readonly
- Ensure `dotnet format` is installed (if using)
- Verify JSON files are valid before formatting

## Integration with CI/CD

Add to GitHub Actions workflow:

```yaml
- name: Format Check
  run: dotnet format --verify --verbosity diagnostic
```

If the check fails, run locally:
```bash
dotnet format
git add .
git commit -m "Apply code formatting"
```

---
*Skill defined in `.github/skills/formatting/skill.md`*
