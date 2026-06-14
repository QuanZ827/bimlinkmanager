# BIM Link Manager

Batch-link Autodesk Construction Cloud (ACC) models into your Revit project. Browse the
hub → project → folder tree, check the models you want, set workset, positioning, and
reference type once, and create every link in a single pass — instead of
`Insert → Link Revit`, one model at a time.

![License: MIT](https://img.shields.io/badge/license-MIT-blue)

## Why

Linking cloud-hosted models in Revit is a one-at-a-time chore. The official automation
route — a registered APS app with per-account ACC provisioning — is impractical when you
work across ACC environments you don't administer: you can't ask every project's admin to
provision a personal tool. BIM Link Manager takes a different path: if you can already open
a cloud model in Revit, you already have everything needed to link it.

## How it works

When you open a cloud model, Revit has signed you in to Autodesk and holds an active
session token. BIM Link Manager retrieves that existing token — via reflection against
`SSONET.dll`, which ships inside the Revit install directory — and uses it to call the
Autodesk Platform Services (APS) Data Management API for hub, project, and folder browsing,
then creates the links with `RevitLinkType.Create`. Same user, same permissions: no
separate app registration, no provisioning, no stored credentials.

**This mechanism is not original to this project.** It was documented years ago by
[ForgeExplorer](https://github.com/leyarx/ForgeExplorer) (by leyarx, MIT-licensed), and
discussed on the Autodesk forums as far back as 2019. BIM Link Manager simply packages that
known technique into a batch-linking workflow.

Because it depends on an **undocumented, internal** Revit component, the reflection is
wrapped defensively at every step. If the token can't be retrieved (e.g. you're not signed
in), the tool reports it clearly and asks you to sign in to Autodesk from Revit's File
menu. This approach may break with future Revit releases — **use at your own risk.**

## Requirements

- Autodesk Revit 2024, 2025, 2026, or 2027
- An Autodesk Construction Cloud account with access to the projects you want to link
- Signed in to Autodesk inside Revit (opening any cloud model once is enough)

## Install

1. Download the latest installer from [Releases](../../releases).
2. Run it. The add-in installs for Revit 2024–2027 under your user profile.
3. Start Revit — a **BIM Link Manager** tab appears on the ribbon.

> Prefer to build it yourself? See [Build from source](#build-from-source) below. The
> installer is produced by `Installer/build-msi.bat` (WiX v4).

## Usage

1. Open your host Revit project (the model you want to link *into*).
2. **BIM Link Manager** tab → **Cloud Links** panel → **Batch Link**.
3. Sign in to ACC if prompted, then browse Hub → Project → Folder.
4. Check the models to link. Models already linked into the current document are detected
   automatically — they show a **LINKED** tag, are dimmed, and are skipped by select-all so
   you can't queue a duplicate.
5. Set the workset, positioning system, and reference type (overlay / attachment), then run
   the batch and watch per-model progress as each link is created.

## Build from source

```
dotnet build BimLinkManager.sln -c Release
```

Targets `net48` (Revit 2024) and `net8.0-windows` (Revit 2025–2027). Autodesk API
assemblies are referenced via the [Nice3point](https://github.com/Nice3point/Revit.Api)
NuGet packages with `ExcludeAssets=runtime`, so **no Autodesk-owned DLLs are included in
this repository** — they resolve against your local Revit install at runtime.

To produce the multi-version installer, run `Installer/build-msi.bat` (requires the
[WiX Toolset](https://wixtoolset.org/) v4).

## Not affiliated with Autodesk

This is an independent, community-built tool. It is **not** produced, endorsed, or
supported by Autodesk. "Autodesk", "Revit", and "Autodesk Construction Cloud" are
trademarks of Autodesk, Inc. The tool relies on undocumented internal Revit behavior that
Autodesk may change at any time.

## Credits & prior art

- [ForgeExplorer](https://github.com/leyarx/ForgeExplorer) by leyarx — the session-token
  retrieval technique this tool builds on (MIT).
- Autodesk Platform Services (APS) Data Management API.
- Fonts: [Hanken Grotesk](https://github.com/displaay/Hanken-Grotesk) and
  [JetBrains Mono](https://github.com/JetBrains/JetBrainsMono), both SIL OFL.

## License

[MIT](LICENSE) © 2026 Zhequan Zhang
