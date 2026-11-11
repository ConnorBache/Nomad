# Nomad

Nomad is an open-source Windows-to-Arch Linux migration project. The goal is a graphical setup wizard that preserves selected files, then installs standard Arch Linux with KDE Plasma after it verifies an independent backup.

## Current status

The Windows planning preview inventories hardware, installed applications, and file metadata and exports a versioned migration plan. It can select the current profile, redirected local Known Folders, and additional files, folders, or drive roots. The preview has no backup, boot staging, installer, or disk modification code. No hardware configuration is qualified for migration yet.

## Build and test

Use the .NET 10 SDK on a Windows x64 development system. Run application tests in a disposable Windows VM or a hosted Windows runner.

```powershell
dotnet build src/Nomad.Windows.App --configuration Release
dotnet run --project tests/Nomad.Tests --configuration Release
dotnet publish src/Nomad.Windows.App --configuration Release --runtime win-x64 --self-contained -p:PublishSingleFile=true --output artifacts/preview
```

The published `Nomad.exe` opens the planning wizard. Exports contain personal file paths and hardware identifiers. Keep them private and use the synthetic fixture when reporting an issue.

## Project documents

- [Product specification](docs/PRODUCT_SPEC.md)
- [Implementation status](docs/IMPLEMENTATION_STATUS.md)
- [Architecture](docs/ARCHITECTURE.md) and [upstream research](docs/ADR/0001-independent-implementation.md)
- [Safety model](docs/SAFETY_MODEL.md), [hardware matrix](docs/HARDWARE_MATRIX.md), and [recovery](docs/RECOVERY.md)
- [Migration format](docs/MIGRATION_FORMAT.md) and [JSON schema](schemas/migration-plan.schema.json)
- [Security reporting](SECURITY.md)

Nomad is available under the [GNU General Public License, version 3 or later](LICENSE).
