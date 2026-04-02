# ILSpy Tool for OpenCode

This directory contains the OpenCode tool definition for `ilspycmd` - the command-line interface for the ILSpy .NET decompiler.

## Overview

The ILSpy tool allows you to decompile .NET assemblies (DLL/EXE files) back into readable C# source code or Intermediate Language (IL). This is useful for:

- Understanding how libraries work internally
- Recovering lost source code
- Debugging and analysis
- Learning from existing implementations

## Installation

Before using this tool, you need to install `ilspycmd`:

```bash
dotnet tool install --global ilspycmd
```

### Requirements

- .NET SDK 6.0 or later
- ilspycmd global tool

## Tool Configuration

The tool is defined in `ilspy.json` with the following capabilities:

### Required Parameters

- `assembly` (string): Path to the .NET assembly file (DLL or EXE)

### Optional Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `output` | string | Output directory or file path |
| `language` | enum (cs/il) | Output language (C# or IL) |
| `project` | boolean | Generate .csproj project file |
| `types` | string | Filter types to decompile (wildcards supported) |
| `excludeTypes` | string | Types to exclude from decompilation |
| `recursive` | boolean | Recurse into nested assemblies |
| `noResume` | boolean | Don't resume partial decompilation |

### Decompilation Options

All decompilation features are enabled by default but can be disabled:

- `decompileAnonymousMethods` - Lambda expressions and anonymous methods
- `decompileExpressionTrees` - Expression tree construction
- `decompileAsyncMethods` - Async/await patterns
- `decompileEnumerators` - Yield return and enumerators
- `decompileAutoProperties` - Auto-implemented properties
- `decompileAutoEvents` - Auto-implemented events
- `decompileUsingDeclarations` - Using statements and declarations
- `decompileAnonymousTypes` - Anonymous type creation
- `decompileQueryExpressions` - LINQ query syntax
- `decompileLock` - Lock statements
- `decompileSwitchOnString` - String switch statements
- `decompileDelegateConstruction` - Delegate creation
- `decompileUnsafeCode` - Pointers and unsafe code
- `alwaysGenerateExceptionVariableForCatchBlocks` - Exception variables in catch

## Usage Examples

### Basic Decompilation

Decompile an assembly to the current directory:

```
ilspy path/to/assembly.dll
```

### Output to Specific Directory

```
ilspy assembly.dll -o ./decompiled-output
```

### Generate IL Code

Instead of C#, output Intermediate Language:

```
ilspy assembly.dll --language il
```

### Generate Project File

Create a complete project structure with .csproj:

```
ilspy assembly.dll -p -o ./my-project
```

### Filter Specific Types

Decompile only types matching a pattern:

```
ilspy assembly.dll -t "MyNamespace.MyClass*" -o ./filtered
```

### Exclude Internal Types

Decompile only public APIs:

```
ilspy assembly.dll --exclude-types "Internal.*" -o ./public-api
```

### Decompile Multiple Assemblies

Process all DLLs in a directory recursively:

```
ilspy ./assemblies -r -o ./decompiled
```

### With Reference Paths

Provide additional references for better decompilation:

```
ilspy assembly.dll --reference-path "./libs;./refs" -o ./output
```

## Advanced Scenarios

### Disable Specific Decompilation Features

To get rawer output without certain transformations:

```
ilspy assembly.dll \
  --decompile-anon-methods false \
  --decompile-async false \
  -o ./raw-output
```

### Resume Interrupted Sessions

By default, ilspycmd resumes partial decompilations. Force a fresh start:

```
ilspy assembly.dll --no-resume -o ./fresh-output
```

## Output Structure

When decompiling to a directory:

```
output/
├── Namespace/
│   ├── Class1.cs
│   ├── Class2.cs
│   └── SubNamespace/
│       └── Class3.cs
└── AssemblyInfo.cs
```

With `-p` (project flag), you'll also get:

```
output/
├── AssemblyName.csproj
├── AssemblyName.sln
└── ... (source files)
```

## Troubleshooting

### Assembly Resolution Errors

If you see errors about missing dependencies, provide reference paths:

```
ilspy assembly.dll --reference-path "C:\Program Files\dotnet\shared\Microsoft.NETCore.App\6.0.0"
```

### Incomplete Decompilation

Some assemblies may be obfuscated or use features that can't be perfectly decompiled. Try:

1. Disabling specific decompilation features
2. Outputting IL instead of C# for problematic types
3. Using `--no-resume` to force a fresh decompilation

### Permission Errors

Ensure you have read access to the assembly and write access to the output directory.

## Security Considerations

- Only decompile assemblies you have permission to analyze
- Decompiled code may not compile without modifications
- Obfuscated code may produce confusing or misleading output
- Some assemblies may be protected by legal restrictions

## Links

- [ILSpy GitHub](https://github.com/icsharpcode/ILSpy)
- [ilspycmd NuGet](https://www.nuget.org/packages/ilspycmd/)
- [OpenCode Documentation](https://docs.opencode.ai)

## License

This tool definition follows the same license as the OpenCode project. ILSpy itself is licensed under MIT.
