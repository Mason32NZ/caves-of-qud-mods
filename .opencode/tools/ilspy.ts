import { tool } from "@opencode-ai/plugin";
import path from "path";

export default tool({
  description: "Decompile .NET assemblies using ILSpy command-line tool. Supports outputting C# source code or IL (Intermediate Language) from DLL/EXE files.",
  args: {
    assembly: tool.schema.string().describe("Path to the .NET assembly file (DLL or EXE) to decompile"),
    output: tool.schema.string().optional().describe("Output directory or file path for decompiled code"),
    language: tool.schema.enum(["cs", "il"]).optional().default("cs").describe("Output language format (C# or IL)"),
    project: tool.schema.boolean().optional().default(false).describe("Generate a .csproj project file for the decompiled code"),
    types: tool.schema.string().optional().describe("Comma-separated list of type names to decompile (wildcards supported with *)"),
    excludeTypes: tool.schema.string().optional().describe("Comma-separated list of type names to exclude from decompilation"),
    recursive: tool.schema.boolean().optional().default(false).describe("Recurse into all nested assemblies in the input directory"),
    noResume: tool.schema.boolean().optional().default(false).describe("Do not resume a partial decompilation session"),
    decompileAnonymousMethods: tool.schema.boolean().optional().default(true).describe("Decompile anonymous methods/lambdas"),
    decompileExpressionTrees: tool.schema.boolean().optional().default(true).describe("Decompile expression trees"),
    decompileAsyncMethods: tool.schema.boolean().optional().default(true).describe("Decompile async methods"),
    decompileEnumerators: tool.schema.boolean().optional().default(true).describe("Decompile enumerators/yield return"),
    decompileAutoProperties: tool.schema.boolean().optional().default(true).describe("Decompile automatically implemented properties"),
    decompileAutoEvents: tool.schema.boolean().optional().default(true).describe("Decompile automatically implemented events"),
    decompileUsingDeclarations: tool.schema.boolean().optional().default(true).describe("Decompile using declarations"),
    decompileAnonymousTypes: tool.schema.boolean().optional().default(true).describe("Decompile anonymous types"),
    decompileQueryExpressions: tool.schema.boolean().optional().default(true).describe("Decompile query expressions"),
    decompileLock: tool.schema.boolean().optional().default(true).describe("Decompile lock statements"),
    decompileSwitchOnString: tool.schema.boolean().optional().default(true).describe("Decompile switch on string"),
    decompileDelegateConstruction: tool.schema.boolean().optional().default(true).describe("Decompile delegate construction"),
    decompileUnsafeCode: tool.schema.boolean().optional().default(true).describe("Decompile unsafe code (pointers, fixed statements)"),
    alwaysGenerateExceptionVariableForCatchBlocks: tool.schema.boolean().optional().default(false).describe("Always generate exception variable in catch blocks"),
    referencePaths: tool.schema.string().optional().describe("Additional reference assembly paths for better decompilation (semicolon-separated)"),
  },
  async execute(args, context) {
    const assemblyPath = path.resolve(args.assembly);
    
    // Build command arguments
    const cmdArgs: string[] = [assemblyPath];
    
    if (args.output) {
      cmdArgs.push("-o", path.resolve(args.output));
    }
    
    if (args.language === "il") {
      cmdArgs.push("--il");
    }
    
    if (args.project) {
      cmdArgs.push("-p");
    }
    
    if (args.types) {
      cmdArgs.push("-t", args.types);
    }
    
    if (args.excludeTypes) {
      cmdArgs.push("--exclude-types", args.excludeTypes);
    }
    
    if (args.recursive) {
      cmdArgs.push("-r");
    }
    
    if (args.noResume) {
      cmdArgs.push("--no-resume");
    }
    
    // Handle boolean decompilation options
    if (!args.decompileAnonymousMethods) {
      cmdArgs.push("--decompile-anon-methods", "false");
    }
    
    if (!args.decompileExpressionTrees) {
      cmdArgs.push("--decompile-expr-trees", "false");
    }
    
    if (!args.decompileAsyncMethods) {
      cmdArgs.push("--decompile-async", "false");
    }
    
    if (!args.decompileEnumerators) {
      cmdArgs.push("--decompile-enums", "false");
    }
    
    if (!args.decompileAutoProperties) {
      cmdArgs.push("--decompile-auto-props", "false");
    }
    
    if (!args.decompileAutoEvents) {
      cmdArgs.push("--decompile-auto-events", "false");
    }
    
    if (!args.decompileUsingDeclarations) {
      cmdArgs.push("--decompile-using", "false");
    }
    
    if (!args.decompileAnonymousTypes) {
      cmdArgs.push("--decompile-anon-types", "false");
    }
    
    if (!args.decompileQueryExpressions) {
      cmdArgs.push("--decompile-queries", "false");
    }
    
    if (!args.decompileLock) {
      cmdArgs.push("--decompile-lock", "false");
    }
    
    if (!args.decompileSwitchOnString) {
      cmdArgs.push("--decompile-switch-string", "false");
    }
    
    if (!args.decompileDelegateConstruction) {
      cmdArgs.push("--decompile-delegates", "false");
    }
    
    if (!args.decompileUnsafeCode) {
      cmdArgs.push("--decompile-unsafe", "false");
    }
    
    if (args.alwaysGenerateExceptionVariableForCatchBlocks) {
      cmdArgs.push("--catch-exceptions");
    }
    
    if (args.referencePaths) {
      cmdArgs.push("--reference-path", args.referencePaths);
    }
    
    try {
      // Execute ilspycmd using Bun's shell
      const result = await Bun.$`ilspycmd ${cmdArgs}`.text();
      return result;
    } catch (error) {
      return `Error running ilspycmd: ${error}`;
    }
  },
});
