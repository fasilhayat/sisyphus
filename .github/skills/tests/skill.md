---
name: tests
description: 'Generates a comprehensive test coverage report and cyclomatic complexity (CC) score for the .NET solution. Use when asked for test coverage, CC scores, or a coverage and complexity report.'
---

# Test Coverage and Complexity Skill

## Description

This skill generates a comprehensive test coverage report and cyclomatic complexity (CC) score for the entire .NET solution and each individual project.

## Trigger

This skill is triggered when the user requests:
- "Generate test coverage report"
- "Show me test coverage"
- "What is the cyclomatic complexity?"
- "CC score for solution"
- "Coverage and complexity report"

## Prerequisites

The following tools/packages must be available:
- `dotnet` CLI
- `coverlet.collector` NuGet package (installed in test projects)
- `dotnet-tool` for cyclomatic complexity (optional: `Gendarme` or `Metrics`)

## Steps

### 1. Run Tests with Coverage

Execute the following command to run all tests with coverage collection:

```powershell
dotnet test --collect:"XPlat Code Coverage" --results-directory "./TestResults"
```

### 2. Locate Coverage File

Find the generated coverage XML file:

```powershell
$coverageFile = Get-ChildItem -Path "./TestResults" -Recurse -Filter *.xml | Select-Object -Last 1
```

### 3. Parse Coverage Data

Parse the XML to extract coverage per project/assembly:

```powershell
[xml]$coverage = Get-Content $coverageFile.FullName
```

### 4. Calculate Cyclomatic Complexity

For each .cs file, calculate cyclomatic complexity by counting decision points:

```powershell
# Simple CC calculation: count if, while, for, foreach, switch, case, catch, conditional ? :
$files = Get-ChildItem -Path "." -Recurse -Filter *.cs | Where-Object { $_.FullName -notmatch "obj|bin|TestResults" }
```

Count complexity patterns:
- `if` = +1
- `else if` = +1
- `while` = +1
- `for` = +1
- `foreach` = +1
- `switch` = +1
- `case` = +1
- `catch` = +1
- Ternary `?` = +1
- Logical `&&` or `||` inside conditions = +1 each

### 5. Generate Markdown Report

Format the output as `test-coverage-report.md`:

## Output Format

The skill generates `test-coverage-report.md` with the following structure:

```markdown
# Test Coverage and Complexity Report

Generated: YYYY-MM-DD HH:MM

## Solution Summary

| Project | Line Coverage | Branch Coverage | Avg Complexity | Test Passed |
|---------|---------------|------------------|----------------|-------------|
| Oasis.Resilience | XX% | XX% | X.XX | ✓/✗ |
| Oasis.Resilience.Test.Unit | XX% | XX% | X.XX | ✓/✗ |
| Demo | XX% | XX% | X.XX | N/A |
| ResilienceWithAkka | XX% | XX% | X.XX | N/A |
| ResilienceWithAop | XX% | XX% | X.XX | N/A |

## Detailed Coverage by Class

### Oasis.Resilience

| Class | Line Rate | Branch Rate | Complexity |
|-------|----------|------------|------------|
| Attributes.SupervisionAttribute | XX% | XX% | X |
| Attributes.FanOutAttribute | XX% | XX% | X |
| ... | ... | ... | ... |

## Cyclomatic Complexity by File

| File | Complexity | Rating |
|------|------------|--------|
| Oasis.Resilience/Attributes/SupervisionAttribute.cs | X | Low/Moderate/High |
| ... | ... | ... |

### Complexity Ratings
- **1-10**: Low complexity (simple)
- **11-20**: Moderate complexity
- **21-50**: High complexity
- **50+**: Very high complexity (refactor recommended)

## Test Results

- **Total Tests**: XX
- **Passed**: XX
- **Failed**: XX
- **Skipped**: XX

## Recommendations

1. Increase coverage for: [list uncovered classes]
2. Refactor high-complexity methods in: [list files with CC > 20]
3. Add unit tests for: [list untested features]
```

## Example Usage

**User**: "Generate test coverage report"

**Assistant**: 
1. Runs `dotnet test` with coverage collection
2. Parses coverage XML
3. Calculates cyclomatic complexity for all .cs files
4. Generates `test-coverage-report.md`
5. Outputs summary table to console

## Notes

- Coverage is collected only for projects with `coverlet.collector` installed
- Cyclomatic complexity is calculated using a simplified heuristic
- For production use, consider integrating:
  - `Microsoft.CodeAnalysis.Metrics` for accurate CC
  - `ReportGenerator` for richer coverage reports
  - `SonarQube` or `CodeClimate` for comprehensive analysis

## File Output

The generated report is saved to:
```
C:\Users\fasil\source\repos\sisyphus\test-coverage-report.md
```

## Error Handling

If coverage file is not found:
- Check that `coverlet.collector` is installed in test projects
- Verify tests ran successfully
- Check `TestResults` directory exists

If complexity calculation fails:
- Ensure .cs files are accessible
- Check for syntax errors in source files
