# 0001 Independent implementation

**Decision:** Build Nomad independently under GPL-3.0-or-later. Use Windows-native discovery and storage interfaces, version the Windows-to-Linux handoff, and evaluate a pinned official `archinstall` release in a later VM milestone.

## Source review

- iGloo uses a WPF wizard, separate ISO authentication and migration components, and installer plugins. Its direct-install service prepares a FAT staging volume and configures a one-shot UEFI boot entry. Its normal installer policy preserves Windows and installs Linux in unallocated space. Its documented storage and fallback behavior informs the boot-staging lab. Nomad needs its own erase authorization and independent backup boundary. [Repository structure](https://github.com/gillesduif/iGloo/blob/main/docs/reference/repository-structure.md) · [Direct-install source](https://github.com/gillesduif/iGloo/blob/main/src/Igloo.Preflight/DirectInstallService.cs) · [Safety model](https://github.com/gillesduif/iGloo/blob/main/docs/reference/safety-model.md)
- Tunic has a Windows PowerShell staging layer, an installer wrapper, and a Python/GTK component. It demonstrates launching an installer from internal storage and getting the live environment off partitions needed for installation. Its Ubuntu/Mint Ubiquity dependency and backup model leave substantial work for Nomad. [Tunic](https://github.com/mikeslattery/tunic) · [Windows source](https://github.com/mikeslattery/tunic/blob/master/tunic.ps1) · [Python source](https://github.com/mikeslattery/tunic/blob/master/run.py)
- Archinstall provides declarative configuration and a separate credentials input. In the VM milestone, validate a pinned configuration against the exact Arch image and installer version, inspect how it resolves the intended disk and partitions, and retain the ability to block the install before the first write. [Archinstall](https://github.com/archlinux/archinstall#running-from-a-declarative-configuration-file-or-url)

The preview uses C# and WinForms with the .NET 10 SDK. It carries no upstream installer code. Any later code reuse needs a source revision, attribution, and license review in the change that introduces it.
