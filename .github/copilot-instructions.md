# SQL Database Project Power Tools - Copilot Instructions

## Repository Summary

**SQL Database Project Power Tools** is a Visual Studio VSIX extension for SQL Database Projects. Features: database import, schema comparison, static analysis, E/R diagrams.

**Tech Stack:** C#, .NET Framework 4.8 (VSIX), .NET 8.0 (CLI tools), .NET Standard 2.0 (contracts). **Build:** MSBuild (VSIX), dotnet CLI (libraries). **Size:** ~7 projects.

## Repository Structure

### Root Directory
- `SqlProjectPowerTools.sln` - Main solution
- `Directory.Build.Props` - Analyzers, code style (applied to all projects)
- `.editorconfig` - ATC coding rules v1.0.1
- `.github/workflows/vsix.yml` - CI/CD pipeline

### Source (`/src`)
- **`Vsix/`** - Main VSIX (.NET Framework 4.8, old-style csproj). UI (WPF), commands, handlers. Embeds `dacfxtool.exe.zip`.
- **`ExtensionPack/`** - Extension pack VSIX (.NET Framework 4.7.2)
- **`DacFXTool/`** - CLI tool (.NET 8.0), built by `BuildCmdlineTool.cmd`
- **`DacFXToolLib/`** - Core library (.NET 8.0): schema extraction, comparison, E/R diagrams
- **`SqlServer.Rules.Report/`** - Static analysis (.NET 8.0)
- **`Contracts/`** - Shared contracts (.NET Standard 2.0)
- **`Lib/`** - Binary deps: `dacfxtool.exe.zip` (17+ MB), `DropDownButtonLib.dll`

### Tests (`/test`)
- **`SmokeTest/`** - Sample SQL projects (Classic, SdkStyle, MsSdkStyle, ClassLib). **No automated unit tests exist.**

## Build Instructions

**CRITICAL:** This project has **two different build systems** that must be used together.

### Prerequisites
- **.NET SDK 8.0 or later** (for DacFXTool, DacFXToolLib, etc.)
- **Visual Studio 2022 with VSIX development workload** (for building VSIX projects)
- **MSBuild** (comes with Visual Studio)
- **7-Zip** (for `BuildCmdlineTool.cmd` script; located at `C:\Program Files\7-Zip\7z.exe` on Windows)

### Build Steps (Full Build)

**ALWAYS follow this sequence:**

1. **Restore:** `dotnet restore SqlProjectPowerTools.sln`

2. **Build .NET projects:**
   ```bash
   dotnet build src/DacFXToolLib/DacFXToolLib.csproj -c Release
   dotnet build src/DacFXTool/DacFXTool.csproj -c Release
   ```

3. **Package CLI tool (Windows only, typically skip):**
   ```cmd
   cd src\DacFXTool && BuildCmdlineTool.cmd && cd ..\..
   ```
   - Requires 7-Zip at `C:\Program Files\7-Zip\7z.exe`
   - Creates `dacfxtool.exe.zip` in `src/Lib/` (already committed)
   - **Only needed when DacFXTool changes**

4. **Build VSIX with MSBuild:**
   ```bash
   msbuild SqlProjectPowerTools.sln /property:Configuration=Release /p:DeployExtension=false /p:ZipPackageCompressionLevel=normal /v:m
   ```
   - **Must use MSBuild** (not `dotnet build`)
   - Takes ~30-60 seconds
   - Output: `src/Vsix/bin/Release/Vsix.vsix`

### Incremental Build

For .NET SDK projects only: `dotnet build src/<ProjectName>/<ProjectName>.csproj -c Release`

**Do NOT use `dotnet build` on VSIX projects (Vsix, ExtensionPack).**

### Common Build Issues

- **dacfxtool.exe.zip not found:** Should exist in `src/Lib/` (committed). Restore from git if missing.
- **MSBuild not found:** Install VS 2022 with VSIX workload or use Developer Command Prompt.
- **VSIX build fails:** Install "Visual Studio extension development" workload in VS Installer.

## Testing

**NO automated unit tests exist.** Manually test via VS experimental instance (F5 in VS) using `test/SmokeTest/` sample projects.

## Code Quality

**Static Analysis** enforced at build via `Directory.Build.Props`:
- Analyzers: StyleCop, SonarAnalyzer, SecurityCodeScan
- Level: `latest-all`, `EnforceCodeStyleInBuild=true`
- Most rules are `error` severity

**Code Style** (`.editorconfig`, ATC v1.0.1):
- File-scoped namespaces, var for built-in types, expression-bodied members
- PascalCase (types/methods), camelCase (params/locals/private fields)
- No public/protected fields (use properties)
- **Build fails on style violations**

Fix via `dotnet format src/<Project>/<Project>.csproj` (VSIX projects: fix manually).

## CI/CD Pipeline

**Workflow:** `.github/workflows/vsix.yml` (Windows only)
**Triggers:** Push to `main`, PRs, manual dispatch

**Steps:**
1. Setup .NET 8.0.x
2. Restore: `dotnet restore SqlProjectPowerTools.sln`
3. Build CLI tool: `BuildCmdlineTool.cmd` (non-PR only)
4. Build VSIX: `msbuild SqlProjectPowerTools.sln /property:Configuration=Release /p:DeployExtension=false /p:ZipPackageCompressionLevel=normal /v:m`
5. Sign and publish (main branch only)

**No tests run in CI** (none exist).

## Development Workflow

1. **Identify project:** UI→`Vsix/`, Core→`DacFXToolLib/`, CLI→`DacFXTool/`, Shared→`Contracts/`, Analysis→`SqlServer.Rules.Report/`
2. **Edit code:** Follow `.editorconfig` (file-scoped namespaces, properties not fields)
3. **Build:** `dotnet build src/<Project>/<Project>.csproj` or MSBuild for full solution
4. **Fix analyzer errors:** Build shows SA####, CA####, IDE#### codes (refer to `.editorconfig`)
5. **Test:** F5 in VS launches experimental instance, use `test/SmokeTest/` projects

## Key Dependencies

**Vsix:** Community.VisualStudio.Toolkit.17, MvvmLight, Microsoft.VisualStudio.Data.Framework
**DacFXToolLib:** ErikEJ.EntityFrameworkCore.SqlServer.Dacpac, Microsoft.Data.SqlClient, EF Core
**SqlServer.Rules.Report:** Microsoft.SqlServer.DacFx, ErikEJ.DacFX.TSQLSmellSCA

## Important Notes

1. **Use MSBuild for full builds** (not `dotnet build`)
2. **Don't modify `src/Lib/dacfxtool.exe.zip`** - regenerate via `BuildCmdlineTool.cmd`
3. **Code analysis is strict** - fix all errors before committing
4. **No automated tests** - test manually in VS experimental instance
5. **Windows-only development** (VSIX requires Windows + VS)
6. **Build times:** .NET projects 5-10s each, MSBuild 30-60s
7. **Template nupkg** must exist in Vsix project at build time

## Troubleshooting

**Analyzer errors:** Check `.editorconfig` for rule, fix code to match style
**VSIX install fails:** Ensure VS 2022+, built with MSBuild, `DeployExtension=false` used
**Missing refs in VSIX:** Verify `dacfxtool.exe.zip` in `src/Lib/`, template nupkg in `src/Vsix/`
**Changes not in VSIX:** Rebuild with MSBuild; if DacFXTool changed, run `BuildCmdlineTool.cmd`

---

**Trust these instructions.** Only search if instructions are incomplete or incorrect.
