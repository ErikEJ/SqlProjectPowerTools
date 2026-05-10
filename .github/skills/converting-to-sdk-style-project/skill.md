---
name: converting-to-sdk-style-project
description: Convert a legacy (non-SDK-style) Visual Studio extension (VSIX) project to modern SDK-style csproj format. Use when the user asks how to modernize a VSIX csproj, convert a Visual Studio extension from old-style to SDK-style, simplify a VSIX project file, remove verbose MSBuild XML from an extension project, migrate a VSIX project to Microsoft.NET.Sdk, or update a Visual Studio extension project to use auto-globbing and modern NuGet PackageReferences. This skill is specifically for VSIX extension projects — not general class libraries, console apps, or other project types. Covers VSSDK and VSIX Community Toolkit (in-process) extensions targeting .NET Framework 4.8.
---

# Converting a Visual Studio Extension (VSIX) Project to SDK-Style

SDK-style projects (`<Project Sdk="Microsoft.NET.Sdk">`) replace the legacy verbose `.csproj` format with a minimal, human-readable file. They auto-glob source files, auto-generate assembly attributes, and use implicit imports — eliminating hundreds of lines of boilerplate.

**Scope:** This skill is specifically for **VSIX extension projects** (VSSDK and VSIX Community Toolkit, in-process) targeting **.NET Framework 4.8**. It is **not** for general class libraries, console apps, or other project types. VisualStudio.Extensibility (out-of-process) projects already use SDK-style and don't need conversion.

---

## Step-by-step conversion

### 1. Replace the `<Project>` root element

**Before (legacy):**

```xml
<?xml version="1.0" encoding="utf-8"?>
<Project ToolsVersion="15.0" DefaultTargets="Build" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
```

**After (SDK-style):**

```xml
<Project Sdk="Microsoft.NET.Sdk">
```

Remove the `ToolsVersion`, `DefaultTargets`, and `xmlns` attributes. The SDK declaration replaces all of them.

---

### 2. Simplify the main PropertyGroup

Replace the sprawling legacy properties with a minimal set.

**Remove these legacy properties** (the SDK or NuGet packages handle them):

- `VSToolsPath` and its `Condition`
- `Configuration` / `Platform` conditional defaults
- `SchemaVersion`
- `ProjectTypeGuids`
- `ProjectGuid`
- `OutputType` (defaults to `Library`)
- `AppDesignerFolder`
- `RootNamespace` (defaults to project name)
- `AssemblyName` (defaults to project name)
- `IncludeAssemblyInVSIXContainer`
- `IncludeDebugSymbolsInVSIXContainer`
- `IncludeDebugSymbolsInLocalVSIXDeployment`
- `CopyBuildOutputToOutputDirectory`
- `CopyOutputSymbolsToOutputDirectory`
- `StartAction`, `StartProgram`, `StartArguments` (replaced by `<Deploy>` in the solution file)

**Replace** `TargetFrameworkVersion` with `TargetFramework`:

```xml
<!-- Before -->
<TargetFrameworkVersion>v4.8</TargetFrameworkVersion>

<!-- After -->
<TargetFramework>net48</TargetFramework>
```

**Add** modern C# and VSIX settings:

```xml
<PropertyGroup>
  <TargetFramework>net48</TargetFramework>
  <Nullable>enable</Nullable>
  <LangVersion>latest</LangVersion>
  <UseWPF>true</UseWPF>

  <!-- VSIX settings -->
  <VSSDKBuildToolsAutoSetup>true</VSSDKBuildToolsAutoSetup>
  <VsixDeployOnDebug>true</VsixDeployOnDebug>
  <GeneratePkgDefFile>true</GeneratePkgDefFile>
  <UseCodebase>true</UseCodebase>
</PropertyGroup>
```

**Only include `<UseWPF>true</UseWPF>`** if the project contains XAML files. Without it, the temporary `_wpftmp.csproj` that MSBuild creates during WPF/XAML compilation does not include all source files. This causes `global using` directives (and any other top-level declarations in non-XAML code files) to be invisible during XAML compilation, producing missing-namespace errors. Including `UseWPF` unconditionally is harmless when there is no XAML and prevents subtle build failures if XAML is added later.

**Keep** `GeneratePkgDefFile` and `UseCodebase` if they were already present — these are VSIX-specific and still needed.

---

### 3. Remove per-configuration PropertyGroups

**Delete entirely** the `Debug|AnyCPU` and `Release|AnyCPU` conditional PropertyGroups:

```xml
<!-- DELETE these blocks -->
<PropertyGroup Condition=" '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' ">
  <DebugSymbols>true</DebugSymbols>
  <DebugType>full</DebugType>
  <Optimize>false</Optimize>
  <OutputPath>bin\Debug\</OutputPath>
  <DefineConstants>DEBUG;TRACE</DefineConstants>
  <ErrorReport>prompt</ErrorReport>
  <WarningLevel>4</WarningLevel>
</PropertyGroup>
```

The SDK provides sensible defaults for Debug and Release configurations. Only add per-config overrides if you have genuinely custom settings.

---

### 4. Add the VSIX ProjectCapability

Add a `ProjectCapability` item to enable VSIX container creation:

```xml
<ItemGroup>
  <ProjectCapability Include="CreateVsixContainer" />
</ItemGroup>
```

This replaces the legacy `ProjectTypeGuids` approach for identifying the project as a VSIX project.

---

### 5. Remove explicit Compile items (use auto-globbing)

SDK-style projects automatically include all `*.cs` files. **Delete all `<Compile Include="..." />` entries** for regular source files.

**For auto-generated files** that have metadata (like `AutoGen`, `DesignTime`, `DependentUpon`), change `Include` to `Update`:

```xml
<!-- Before -->
<Compile Include="source.extension.cs">
  <AutoGen>True</AutoGen>
  <DesignTime>True</DesignTime>
  <DependentUpon>source.extension.vsixmanifest</DependentUpon>
</Compile>

<!-- After -->
<Compile Update="source.extension.cs">
  <AutoGen>True</AutoGen>
  <DesignTime>True</DesignTime>
  <DependentUpon>source.extension.vsixmanifest</DependentUpon>
</Compile>
```

The `Update` verb applies metadata to the file that was already auto-included by the SDK glob. Using `Include` would cause a duplicate-item error.

Do the same for `VSCommandTable.cs` or any other generated file:

```xml
<Compile Update="VSCommandTable.cs">
  <AutoGen>True</AutoGen>
  <DesignTime>True</DesignTime>
  <DependentUpon>VSCommandTable.vsct</DependentUpon>
</Compile>
```

---

### 6. Remove legacy Import elements

**Delete all of these** — the SDK and NuGet packages handle them automatically:

```xml
<!-- DELETE -->
<Import Project="$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props" ... />
<Import Project="$(MSBuildToolsPath)\Microsoft.CSharp.targets" />
<Import Project="$(VSToolsPath)\VSSDK\Microsoft.VsSDK.targets" ... />
```

Also remove any commented-out `BeforeBuild`/`AfterBuild` target blocks and empty `<ItemGroup />` elements.

---

### 7. Update NuGet packages

Update `Microsoft.VSSDK.BuildTools` to version **18.5** or later and simplify its asset metadata:

```xml
<!-- Before -->
<PackageReference Include="Microsoft.VSSDK.BuildTools" Version="17.14.2120">
  <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
  <PrivateAssets>all</PrivateAssets>
</PackageReference>

<!-- After -->
<PackageReference Include="Microsoft.VSSDK.BuildTools" Version="18.5.40034">
  <PrivateAssets>all</PrivateAssets>
</PackageReference>
```

Optionally add the `Microsoft.VisualStudio.SDK` metapackage if the project uses many individual VS interop assemblies — it replaces them all with a single reference.

Keep other packages like `Community.VisualStudio.Toolkit.17` and `Community.VisualStudio.VSCT` as-is.

---

### 8. Clean up framework references

**Remove** references that are now implicit with the SDK or covered by NuGet:

- `System` — always implicit
- Any VS interop assemblies replaced by `Microsoft.VisualStudio.SDK` (if added)

**Keep** framework references that are **not** covered by NuGet packages:

```xml
<ItemGroup>
  <Reference Include="PresentationCore" />
  <Reference Include="PresentationFramework" />
  <Reference Include="System.ComponentModel.Composition" />
  <Reference Include="System.Design" />
</ItemGroup>
```

WPF assemblies (`PresentationCore`, `PresentationFramework`) and MEF (`System.ComponentModel.Composition`) must remain as explicit references since they're not included in the SDK metapackage.

---

### 9. Delete Properties/AssemblyInfo.cs

SDK-style projects **auto-generate** assembly attributes (`AssemblyTitle`, `AssemblyVersion`, `AssemblyFileVersion`, etc.). Delete `Properties/AssemblyInfo.cs` entirely.

If the file contains polyfills (such as `IsExternalInit` for C# `init` keyword support on .NET Framework 4.8), move them to a standalone file:

**IsExternalInit.cs:**

```csharp
// Polyfill for C# 'init' keyword support on .NET Framework 4.8.
// This type is provided by the runtime in .NET 5+ but must be
// defined manually when targeting older frameworks.
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
```

---

### 10. Update the solution file

> **⚠️ This must be the last edit.** Modifying the `.sln`/`.slnx` file causes Visual Studio to reload the solution, which interrupts any running automation. Always perform all other changes first, then update the solution file as the final step.

#### Convert .sln to .slnx (if applicable)

If the solution uses the legacy `.sln` format, convert it to `.slnx` (XML-based solution format). In Visual Studio, right-click the solution in Solution Explorer and select **Save As Solution XML File**, or use the CLI:

```text
dotnet sln migrate
```

#### Add the Deploy element

In the `.slnx` file, you MUST add a `<Deploy>` element to the VSIX project entry. This replaces the old `StartAction`/`StartProgram`/`StartArguments` pattern from the legacy csproj and is **required for F5 debugging** to deploy the VSIX to the experimental instance:

```xml
<Solution>
  <Configurations>
    <Platform Name="Any CPU" />
    <Platform Name="arm64" />
    <Platform Name="x86" />
  </Configurations>
  <Project Path="src/MyExtension.csproj" Id="...">
    <Platform Solution="*|arm64" Project="arm64" />
    <Platform Solution="*|x86" Project="x86" />
    <Deploy Solution="Debug|Any CPU" />
  </Project>
</Solution>
```

Without the `<Deploy>` element, pressing F5 will build but **not deploy** the extension to the experimental VS instance.

---

## Complete before/after example

### Before (legacy csproj) — ~120 lines

```xml
<?xml version="1.0" encoding="utf-8"?>
<Project ToolsVersion="15.0" DefaultTargets="Build" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <PropertyGroup>
    <VSToolsPath Condition="'$(VSToolsPath)' == ''">$(MSBuildExtensionsPath32)\Microsoft\VisualStudio\v$(VisualStudioVersion)</VSToolsPath>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>
  <Import Project="$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props" Condition="Exists('...')" />
  <PropertyGroup>
    <Configuration Condition=" '$(Configuration)' == '' ">Debug</Configuration>
    <Platform Condition=" '$(Platform)' == '' ">AnyCPU</Platform>
    <SchemaVersion>2.0</SchemaVersion>
    <ProjectTypeGuids>{82b43b9b-a64c-4715-b499-d71e9ca2bd60};{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}</ProjectTypeGuids>
    <ProjectGuid>{16DBD2AC-922B-45C1-8D25-A65682C3F4FC}</ProjectGuid>
    <OutputType>Library</OutputType>
    <AppDesignerFolder>Properties</AppDesignerFolder>
    <RootNamespace>MyExtension</RootNamespace>
    <AssemblyName>MyExtension</AssemblyName>
    <TargetFrameworkVersion>v4.8</TargetFrameworkVersion>
    <GeneratePkgDefFile>true</GeneratePkgDefFile>
    <UseCodebase>true</UseCodebase>
    <IncludeAssemblyInVSIXContainer>true</IncludeAssemblyInVSIXContainer>
    <IncludeDebugSymbolsInVSIXContainer>false</IncludeDebugSymbolsInVSIXContainer>
    <IncludeDebugSymbolsInLocalVSIXDeployment>true</IncludeDebugSymbolsInLocalVSIXDeployment>
    <CopyBuildOutputToOutputDirectory>true</CopyBuildOutputToOutputDirectory>
    <CopyOutputSymbolsToOutputDirectory>true</CopyOutputSymbolsToOutputDirectory>
    <StartAction>Program</StartAction>
    <StartProgram Condition="'$(DevEnvDir)' != ''">$(DevEnvDir)devenv.exe</StartProgram>
    <StartArguments>/rootsuffix Exp</StartArguments>
  </PropertyGroup>
  <PropertyGroup Condition=" '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' ">
    <DebugSymbols>true</DebugSymbols>
    <DebugType>full</DebugType>
    <Optimize>false</Optimize>
    <OutputPath>bin\Debug\</OutputPath>
    <DefineConstants>DEBUG;TRACE</DefineConstants>
    <ErrorReport>prompt</ErrorReport>
    <WarningLevel>4</WarningLevel>
  </PropertyGroup>
  <PropertyGroup Condition=" '$(Configuration)|$(Platform)' == 'Release|AnyCPU' ">
    <DebugType>pdbonly</DebugType>
    <Optimize>true</Optimize>
    <OutputPath>bin\Release\</OutputPath>
    <DefineConstants>TRACE</DefineConstants>
    <ErrorReport>prompt</ErrorReport>
    <WarningLevel>4</WarningLevel>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="MyCommand.cs" />
    <Compile Include="MyPackage.cs" />
    <Compile Include="Properties\AssemblyInfo.cs" />
    <Compile Include="source.extension.cs">
      <AutoGen>True</AutoGen>
      <DesignTime>True</DesignTime>
      <DependentUpon>source.extension.vsixmanifest</DependentUpon>
    </Compile>
    <Compile Include="VSCommandTable.cs">
      <AutoGen>True</AutoGen>
      <DesignTime>True</DesignTime>
      <DependentUpon>VSCommandTable.vsct</DependentUpon>
    </Compile>
  </ItemGroup>
  <ItemGroup>
    <VSCTCompile Include="VSCommandTable.vsct">
      <ResourceName>Menus.ctmenu</ResourceName>
      <Generator>VsctGenerator</Generator>
      <LastGenOutput>VSCommandTable.cs</LastGenOutput>
    </VSCTCompile>
  </ItemGroup>
  <ItemGroup>
    <Content Include="Resources\LICENSE.txt">
      <CopyToOutputDirectory>Always</CopyToOutputDirectory>
      <IncludeInVSIX>true</IncludeInVSIX>
    </Content>
    <None Include="source.extension.vsixmanifest">
      <SubType>Designer</SubType>
      <Generator>VsixManifestGenerator</Generator>
      <LastGenOutput>source.extension.cs</LastGenOutput>
    </None>
  </ItemGroup>
  <ItemGroup>
    <Reference Include="PresentationCore" />
    <Reference Include="PresentationFramework" />
    <Reference Include="System" />
    <Reference Include="System.ComponentModel.Composition" />
    <Reference Include="System.Design" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Community.VisualStudio.VSCT" Version="16.0.29.6" PrivateAssets="all" />
    <PackageReference Include="Community.VisualStudio.Toolkit.17" Version="17.0.549" ExcludeAssets="Runtime">
      <IncludeAssets>compile; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Microsoft.VSSDK.BuildTools" Version="17.14.2120">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
  </ItemGroup>
  <Import Project="$(MSBuildToolsPath)\Microsoft.CSharp.targets" />
  <Import Project="$(VSToolsPath)\VSSDK\Microsoft.VsSDK.targets" Condition="'$(VSToolsPath)' != ''" />
</Project>
```

### After (SDK-style csproj) — ~80 lines

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net48</TargetFramework>
    <Nullable>enable</Nullable>
    <LangVersion>latest</LangVersion>
    <UseWPF>true</UseWPF>

    <!-- VSIX settings -->
    <VSSDKBuildToolsAutoSetup>true</VSSDKBuildToolsAutoSetup>
    <VsixDeployOnDebug>true</VsixDeployOnDebug>
    <GeneratePkgDefFile>true</GeneratePkgDefFile>
    <UseCodebase>true</UseCodebase>
  </PropertyGroup>

  <ItemGroup>
    <ProjectCapability Include="CreateVsixContainer" />
  </ItemGroup>

  <!-- Auto-generated files (use Update because SDK auto-includes *.cs) -->
  <ItemGroup>
    <Compile Update="source.extension.cs">
      <AutoGen>True</AutoGen>
      <DesignTime>True</DesignTime>
      <DependentUpon>source.extension.vsixmanifest</DependentUpon>
    </Compile>
    <Compile Update="VSCommandTable.cs">
      <AutoGen>True</AutoGen>
      <DesignTime>True</DesignTime>
      <DependentUpon>VSCommandTable.vsct</DependentUpon>
    </Compile>
  </ItemGroup>

  <!-- VSCT command table -->
  <ItemGroup>
    <AvailableItemName Include="VSCTCompile" />
  </ItemGroup>
  <ItemGroup>
    <None Remove="VSCommandTable.vsct" />
    <VSCTCompile Include="VSCommandTable.vsct">
      <ResourceName>Menus.ctmenu</ResourceName>
      <Generator>VsctGenerator</Generator>
      <LastGenOutput>VSCommandTable.cs</LastGenOutput>
    </VSCTCompile>
  </ItemGroup>

  <!-- VSIX manifest -->
  <ItemGroup>
  <None Remove="source.extension.vsixmanifest" />
    <None Update="source.extension.vsixmanifest">
      <SubType>Designer</SubType>
      <Generator>VsixManifestGenerator</Generator>
      <LastGenOutput>source.extension.cs</LastGenOutput>
    </None>
  </ItemGroup>

  <!-- Resources included in VSIX -->
  <ItemGroup>
    <Content Include="Resources\LICENSE.txt">
      <CopyToOutputDirectory>Always</CopyToOutputDirectory>
      <IncludeInVSIX>true</IncludeInVSIX>
    </Content>
    <Content Include="Resources\Icon.png">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
      <IncludeInVSIX>true</IncludeInVSIX>
    </Content>
  </ItemGroup>

  <!-- .NET Framework assemblies not covered by NuGet packages -->
  <ItemGroup>
    <Reference Include="PresentationCore" />
    <Reference Include="PresentationFramework" />
    <Reference Include="System.ComponentModel.Composition" />
    <Reference Include="System.Design" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Community.VisualStudio.VSCT" Version="16.0.29.6" PrivateAssets="all" />
    <PackageReference Include="Community.VisualStudio.Toolkit.17" Version="17.0.549" ExcludeAssets="Runtime">
      <IncludeAssets>compile; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Microsoft.VSSDK.BuildTools" Version="18.5.40034">
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
  </ItemGroup>

</Project>
```

---

## Checklist

Use this checklist to verify the conversion is complete:

- [ ] `<Project Sdk="Microsoft.NET.Sdk">` is the root element
- [ ] `TargetFramework` is `net48` (not `TargetFrameworkVersion`)
- [ ] Per-configuration PropertyGroups (Debug/Release) are removed
- [ ] Legacy properties removed (`ProjectTypeGuids`, `ProjectGuid`, `SchemaVersion`, `OutputType`, `StartAction`, etc.)
- [ ] `<UseWPF>true</UseWPF>` is present (required for correct XAML compilation and `global using` support)
- [ ] `<ProjectCapability Include="CreateVsixContainer" />` is present
- [ ] All `<Compile Include="...">` removed; auto-generated files use `<Compile Update="...">`
- [ ] XAML code-behind files use `<Compile Update>` with `<DependentUpon>` for nesting
- [ ] XAML `<Page>` items use `Update` (not `Include`) since `UseWPF` auto-globs them
- [ ] `VSCTCompile` has `<None Remove>` and `<AvailableItemName>` for Solution Explorer visibility
- [ ] `source.extension.vsixmanifest` remains `None` (VSSDK requires this build action)
- [ ] All `<Import>` elements removed (`Microsoft.Common.props`, `Microsoft.CSharp.targets`, `Microsoft.VsSDK.targets`)
- [ ] `Microsoft.VSSDK.BuildTools` updated to 18.5+
- [ ] `Properties/AssemblyInfo.cs` deleted or kept with `<GenerateAssemblyInfo>false</GenerateAssemblyInfo>` if it has custom attributes
- [ ] `System` reference removed (implicit in SDK)
- [ ] Solution file is `.slnx` format with `<Deploy Solution="Debug|Any CPU" />` on the VSIX project
- [ ] Project builds and deploys with F5
