# Nomad — Implementation Specification

**Status:** Initial product and technical specification (2026-11-11)  
**Project:** Nomad (working name)  
**License:** Open source; GPL-3.0-or-later if code is derived from iGloo  
**Primary platform:** Windows 10/11 (source) → Arch Linux x86-64 (destination)  
**Purpose of this document:** Give an AI coding agent or human contributor a concrete, testable specification to implement, without guessing about destructive operations.

## 1. Product definition

Nomad is a graphical Windows application that replaces Windows with a functioning Arch Linux installation while preserving the Windows user's personal files and explicitly selected additional files and directories. The intended experience is **download → configure in a wizard → review → confirm → unattended migration and reboot → usable Arch desktop**. A nontechnical user must not be required to use a terminal, manually partition disks, prepare a bootable USB, edit firmware settings, or install drivers.

Nomad is **not** a Linux distribution, a dual-boot manager, a generic backup tool, or a promise that Windows `.exe` applications work natively on Linux. Its product value is orchestration: inspect the Windows installation, determine whether migration can be performed safely, collect choices up front, create an auditable plan, install and configure Arch, move approved data, and explain what transferred.

**Core design principles:**

1. **Minimal intervention.** Make reasonable defaults and consolidate consequential choices in one pre-migration wizard. After confirmation, perform everything automatically that is safe and supported.
2. **Preserve data deliberately.** The entire current user's home/profile directory is selected by default, including hidden content. Users can add directories, files, partitions and, where authorized, other user profiles.
3. **Safety before convenience.** Never run an unverified disk operation, silently delete data, or treat a hash as a substitute for an independent backup. Unsupported configurations must stop with an understandable message.
4. **Reconstruct instead of blindly copying Windows.** Install Linux components natively. Transfer Windows-only application installations as archived files unless a compatibility adapter explicitly supports them.
5. **Transparency.** Show what will be transferred, what will be erased, what can be restored, and what will not work after migration.
6. **Free and open source.** No subscriptions, advertising, telemetry by default, or proprietary cloud dependency.
7. **Maintainable.** Prefer existing official installers and tested operating-system APIs over custom partition/NTFS tooling.

### 1.1 Scope and delivery distinction

**Ultimate goal:** One-click in-place migration, potentially with no separate backup disk, including difficult near-full-disk scenarios.

**First safe release (MVP):** A Windows wizard, no USB **installer**, a verified independent backup destination for every selected file, unattended Arch installation, verified restore, configured desktop. A backup disk or network location is allowed even though installation media is not required. The user may need to connect a backup drive; this is a clear MVP constraint, not an invisible promise.

**Later research goal:** Same-disk migration with no independent backup only where formal safety proofs, recoverability and extensive testing establish acceptable behavior. Never implement speculative “copy, delete, grow partition, repeat” logic as if deleting a file immediately makes its blocks available to a Linux partition.

## 2. Supported platforms and explicit limits

### MVP supported matrix

- Windows 10/11 x86-64, running with administrative privileges.
- One ordinary internal SSD/HDD containing Windows, GPT partition table, UEFI firmware, and NTFS Windows data volume.
- A separate, writable backup volume with enough verified available capacity for **all** selected data, migration metadata and a conservative overhead allowance.
- Supported CPU, RAM, GPU, storage-controller, wireless/network, and Secure Boot combination **confirmed by the preflight detector**. Unsupported components block rather than being guessed through.
- Sufficient free disk space to stage a verified installer boot environment without destroying user data.
- Stable external power (or safe battery/power thresholds), internet connectivity for required image/package downloads, and a trusted source of Arch packages.
- One local Windows user account in the MVP, with other-account migration added after ownership and permissions are handled correctly.

### Unsupported by default until separately engineered and tested

- Legacy BIOS/MBR; ARM systems; Windows on dynamic disks; Storage Spaces; hardware RAID or unusual firmware/controller configurations.
- Multi-OS systems and existing Linux partitions where erase boundaries cannot be established without risk.
- Full-disk or volume encryption whose keys cannot be validated; EFS-encrypted files that cannot be exported into readable data.
- Enterprise-managed devices where policy, escrow, enrollment or licensing prevents a safe migration.
- Source disks with known filesystem errors, failing hardware, inaccessible sectors, or insufficient staging/backup space.
- Secure Boot setups without a tested, signed, trusted boot path. Never silently disable Secure Boot or tell a novice that an unsupported path is safe.
- Restoring Windows executables as runnable native Linux packages.

The preflight screen shall give an exact, user-readable reason for every unsupported result and offer only supported recovery paths.

## 3. Required user experience

Nomad runs as a normal graphical Windows executable. It asks for elevation only when required and explains why.

### Wizard screens (in order)

1. **Welcome.** “Replace Windows with Arch Linux while keeping your chosen files.” Explain that Windows will be removed, Windows-only programs may not run, and a verified backup is required for the initial release.
2. **Computer check.** Automatically detect Windows edition/build, UEFI/GPT, processor, RAM, GPU, controllers, network hardware, connected volumes, BitLocker/device encryption, Secure Boot, disk health, partition layout, installed programs, and free space. Display `Ready`, `Needs action`, or `Not supported`, with explanations. No raw error codes unless expandable.
3. **Choose what to keep.** Current user's complete Windows profile selected by default. Show Documents, Desktop, Downloads, Pictures, Videos, Music, AppData and other profile data as a comprehensible tree. Allow inclusion of arbitrary files/directories and volumes including `C:\Program Files`, `C:\Program Files (x86)` and `C:\ProgramData`. Allow exclusions explicitly. Show sizes, inaccessible entries, and special cases (cloud placeholders, encrypted files, links).
4. **Applications and settings.** Inventory Windows applications. Explain categories: native Linux reinstall available; Linux alternative; Wine/Proton candidate (informational until tested); archived only; unsupported. Let users select supported restores, but avoid requiring them to understand compatibility layers.
5. **Choose Linux experience.** Default: standard Arch Linux with a familiar, fully configured **KDE Plasma** desktop. Provide simple preferences for account name, password, locale, keyboard, time zone, appearance, disk encryption if supported, and automatic updates behavior. Avoid asking about filesystems, initramfs, bootloaders, or package managers.
6. **Backup destination and verification.** Select an independent physical drive or supported backup location. Check capacity, permissions, filesystem limits, encryption/privacy, reliability, and whether the target is secretly on the disk to be erased. Backup must complete and verify before destructive steps can be unlocked.
7. **Review and confirm.** Display source disk by make/model/size and destination identity; what will be erased; total selected bytes/files; transfer manifest summary; planned Linux partition layout; app outcomes; risk/limitations; and recovery information. Require explicit acknowledgment that Windows will be erased. This is the only deliberately consequential confirmation after setup.
8. **Migration progress.** Show distinct stages: backup, verification, boot staging, restart, Arch installation, data restore, verification, finishing. After confirmation and reboot, do not present an interactive installer or command prompt.
9. **First boot and report.** Show a working Arch login/desktop. Provide a plain-language report of copied files, native apps installed, archived Windows programs, warnings, skipped items (which should ordinarily be zero for selected data), backup retention, and instructions for manual recovery.

If a blocker appears after confirmation, halt with a clear recovery option. Do not guess, silently skip data, or continue to wipe Windows.

### UX details

- Every destructive action is visible before execution; progressive-disclosure advanced controls are allowed.
- No shell commands shown to nontechnical users. Developer logs remain available separately.
- Keyboard and accessibility support, consistent progress reporting, cancelability while still reversible, and distinct warnings when cancellation is no longer safe.
- No login to a Nomad service and no account creation apart from the Linux account.
- Default language English, localization-ready strings throughout.

## 4. Data preservation policy

### 4.1 Default selection

The complete active Windows user profile (normally `C:\Users\<name>\`) is selected, including:

- Desktop, Documents, Downloads, Pictures, Music, Videos, Favorites, Saved Games, Contacts, and any redirected Known Folders.
- Hidden files/folders such as `.ssh`, `.gitconfig`, projects, configuration and keys.
- `AppData\Roaming`, `AppData\Local`, `AppData\LocalLow`, browser data, game saves and other per-user application state.
- Files in the profile root and discovered directories not matching known folder names.

Resolve Windows Known Folders through Windows APIs; never assume the paths are fixed. If folders are redirected to OneDrive or another disk, honor the actual locations. Cloud-only placeholders must be hydrated and verified or listed as blockers; a placeholder is not the file's content.

### 4.2 User-selected extra locations

Allow granular selection of:

- `C:\Program Files` and `C:\Program Files (x86)` (including individual programs).
- `C:\ProgramData` and custom application data directories.
- `C:\Projects`, `D:\Games`, `E:\Backups`, other folders, files, partitions, and external volumes.
- Other Windows user profiles **only with explicit selection and authorization**.

A Program Files selection is a request to **preserve files**, not a promise that installed programs remain usable. Preserve the original absolute Windows path in migration metadata. Restore unmatched Windows directories under a clearly named archive such as `~/Migrated Windows Files/C/Program Files/...`, **never** into `/usr`, `/bin`, or Linux system package paths.

### 4.3 Mapping and fidelity

- Map standard personal content to appropriate Linux locations under `/home/<new-user>/` while preserving relative paths.
- Preserve unknown profile directories under a clearly labeled original-Windows-profile archive or a documented mapping, with no collisions.
- Preserve byte-for-byte file contents, modification times when possible, original paths in a manifest, and record metadata that cannot translate exactly (e.g., NTFS ACLs or alternate data streams).
- Handle files differing only by letter case, long paths, Unicode, sparse files, symlinks, junctions and other reparse points, inaccessible/locked files, and duplicates deterministically.
- Avoid circular traversal and following links outside selected roots unless a path is independently selected.
- For incompatible Windows metadata, record rather than silently discard; restore practical Linux permissions and ownership without indiscriminately marking files executable.
- Copy secrets (SSH private keys, browser profiles, saved credentials) only into appropriately restricted, encrypted destinations. Do not print secrets in logs.
- Explicitly distinguish browser/profile data copied as files from credentials that have actually been restored and tested. Windows DPAPI-protected material may not be portable by raw copy.
- Treat app-specific databases, game saves and open files as consistency-sensitive. Use Windows snapshot/copy APIs where appropriate; if a consistent copy cannot be obtained, identify the file as unresolved and block destructive migration.

### 4.4 Data manifest

Scan selected source roots to create a deterministic manifest containing source volume stable ID, original absolute path, item type, size, mtime, file identity when available, optional content hash, destination path, backup location, status, errors and metadata notes. Store the manifest in at least two safe places, including the independent backup. Generate and verify content hashes for all selected regular files before treating the backup as complete.

States per file: `discovered → readable → backed_up → backup_verified → restored → restore_verified`; `excluded` and `blocked` must be explicit. No selected item may disappear from reporting.

Never automatically delete the backup at migration completion.

## 5. Windows applications and environmental continuity

MVP shall detect installed applications through appropriate Windows inventory mechanisms, but treat such inventory as incomplete. Provide a lightweight mapping catalog:

| Windows item | MVP behavior |
|---|---|
| Browsers | Install an appropriate Linux browser; migrate bookmarks/profile content where supported and verified |
| VS Code and common developer tools | Offer native Linux installation; preserve settings and projects where compatible |
| Steam | Install native Steam if selected; preserve game data and saves according to tested rules |
| Office/document editors | Preserve documents; offer a Linux alternative; don't claim full compatibility |
| Windows-only executables | Archive selected folders and clearly label as not automatically executable |
| Other applications | Archive selected files, include app in migration report, no automatic compatibility claims |

Additional capabilities may later provide tested Wine/Proton setups, Linux equivalents, driver mapping, browser credential migration, and app-specific importers. They must be pluggable, versioned, and independently testable. Never copy an arbitrary Windows Registry hive into Linux and claim its settings are migrated.

## 6. Target Arch installation

- **Actual Arch Linux** from official repositories, not merely an Arch derivative.
- KDE Plasma as the default desktop, configured for sensible Windows-user expectations; include a graphical login manager, file manager, browser, audio, networking, updates, fonts, and basic utilities.
- Stable package state using a versioned, tested package profile, hardware-specific drivers, and no reliance on unreviewed AUR packages.
- Automatically configure UEFI boot, initramfs, networking, time zone, locales, input, user/group permissions, firewall defaults, sound, graphics acceleration, power management and a functional desktop session.
- Establish a working update mechanism that does not require routine terminal intervention. Arch is rolling-release, so updates and recovery must be thoughtfully designed, not promised to be maintenance-free without implementation.
- Use a deliberately simple disk layout for the MVP: GPT, appropriate EFI System Partition, Linux root filesystem (ext4 initially), swap strategy, and `/home` in the root filesystem. Any existing ESP must be assessed for adequate capacity; resizing is **not** a casual default.
- Optional disk encryption is offered only if the unattended boot and recovery flows are fully implemented and tested.
- Recovery information, package manifest and migration results remain accessible in the installed system.

Use official `archinstall` APIs/configuration or a well-audited Arch provisioning approach in an isolated Linux environment. Never scrape and automate the interactive TUI as the primary interface.

## 7. Recommended architecture

A modular implementation that allows reuse of iGloo components is preferable to a monolith. A candidate stack is **C#/.NET for Windows integration and GUI**, **Python for the Linux installer/restore agent**, and a **versioned JSON schema** for exchanging immutable plans and status. This is a proposal, not a requirement to preserve iGloo's exact technology choices.

```text
Nomad/
  README.md
  LICENSE
  docs/
    PRODUCT_SPEC.md
    ARCHITECTURE.md
    SAFETY_MODEL.md
    MIGRATION_FORMAT.md
    HARDWARE_MATRIX.md
    RECOVERY.md
    ADR/
  schemas/
    migration-plan.schema.json
    inventory.schema.json
    journal.schema.json
    report.schema.json
  src/
    Nomad.Windows.App/        # Graphical wizard
    Nomad.Windows.Preflight/  # Hardware, encryption and disk inventory
    Nomad.Windows.Scanner/    # Profile and extra-path discovery
    Nomad.Windows.Backup/     # Backup, hashes, consistency, validation
    Nomad.Windows.Staging/    # Verified installer staging and UEFI boot setup
    Nomad.Core/               # Shared plans, state, validation, policy
  linux/
    installer/                # Unattended Arch installation
    restore/                  # Restore and verification
    firstboot/                # Configure desktop, drivers, reports
  tests/
    unit/
    integration/
    destructive-vm/
    fixtures/
  tools/
    build/
    test-vms/
```

### Components and responsibilities

**Preflight service.** Read-only hardware inventory and filesystem inspection; produces a compatibility verdict and an explanation. It does not modify disks.

**Data scanner.** Enumerates selected roots and Windows Known Folders, detects special files, measures size, identifies unsupported content, and writes a complete inventory.

**Planner.** Produces a signed/hash-bound immutable migration plan: source and target disk GUIDs, selected data, required staging/backup space, erase boundaries, boot settings, Arch package profile, and precise expected state transitions. It refuses ambiguous plans.

**Backup agent.** Copies selected data to a genuinely independent destination; computes and verifies per-file hashes; ensures manifests, logs and recovery files survive restart. It uses safe filesystem APIs and supports resumable copying. No file is marked verified on a partial write.

**Boot stager.** Downloads authenticated Arch installer components, verifies signatures/checksums against trusted release metadata, creates a tested internal-disk boot route and one-time UEFI entry, and provides a fallback to Windows **before** any destructive point. It must be proven not to depend on files on a partition it will erase.

**Linux installer agent.** Boots in its own trusted environment, validates the plan and disk identities again, checks the independent backup is accessible and verified, then performs the approved partition changes and Arch installation. Do not infer disk IDs from `/dev/sda` or drive letters.

**Restore agent.** Restores selected user data, applies path mappings, resolves conflicts without overwriting unrelated content, verifies restored content, and records discrepancies.

**First-boot agent.** Applies supported desktop/application settings, confirms network and graphical session health, writes the final report and disables temporary services and boot overrides.

**Recovery subsystem.** Offers safe stop/retry behavior, logs, backup recovery instructions, and a bootable rescue strategy. Distinguish recovery of **selected data** from restoration of a full Windows installation; full Windows rollback requires an actual restorable image and is not promised by a file-only backup.

### Plan and journal contracts

Create a schema-validated plan containing fields such as:

```json
{
  "schemaVersion": "1.0",
  "migrationId": "generated-uuid",
  "source": { "diskGuid": "...", "windowsVolumeGuid": "..." },
  "target": { "diskGuid": "...", "eraseScope": "approved-windows-disk" },
  "dataSelection": { "roots": ["C:\\Users\\Example"], "exclusions": [] },
  "backup": { "volumeId": "...", "manifestPath": "...", "verified": false },
  "linux": { "distribution": "arch", "desktop": "plasma", "username": "example" },
  "planHash": "...",
  "approval": { "sourceDiskConfirmed": false, "eraseApproved": false }
}
```

This is an illustrative schema shape, **not** a sufficient authorization for erase. Real authorization must bind the verified plan hash, current source/target identities, backup proof, user confirmation and active transition state. Secrets must not be placed in plaintext manifest fields.

The append-only journal shall record transition name, prior state, timestamp, operation ID, plan hash, affected disk/volume IDs, outcome and recoverability. Flush state at checkpoints so that process crashes and power loss never cause an ambiguous resumed action.

## 8. Migration state machine

```text
NEW
 → PREFLIGHT_PASSED
 → INVENTORY_COMPLETE
 → PLAN_REVIEWED
 → BACKUP_COMPLETE
 → BACKUP_VERIFIED
 → INSTALLER_STAGED
 → READY_TO_REBOOT
 → LINUX_ENVIRONMENT_STARTED
 → PRE_ERASE_REVALIDATED
 → DESTRUCTIVE_INSTALL_STARTED
 → ARCH_INSTALLED
 → DATA_RESTORED
 → RESTORE_VERIFIED
 → FIRST_BOOT_VALIDATED
 → COMPLETE
```

Also support `BLOCKED`, `CANCELLED`, `RETRYABLE_FAILURE`, and `RECOVERY_REQUIRED` with explicit allowed transitions. Prior to `DESTRUCTIVE_INSTALL_STARTED`, cancel should leave Windows bootable or provide a tested reversal plan. After that boundary, do not claim automatic Windows rollback; preserve backups and provide recovery.

**Hard preconditions for destructive installation:**

1. The exact plan was approved and is unchanged.
2. Target disk identity and geometry still match the preflight snapshot.
3. Installer image and boot path are authenticated and validated.
4. Every selected source file has an independently stored, verified backup, or the migration is halted with an explicit unresolved-file report.
5. The backup is on media not included in the erase scope, and is readable from the installer environment.
6. All selected data is represented by a manifest entry, including errors and exceptions; no unresolved selected entries exist.
7. Power, storage health, encryption and firmware checks still pass.
8. The execution plan is narrowly scoped and reject-by-default for unrecognized partitions.

Operations must be **idempotent where possible**; every irreversible step must have a checked precondition, explicit checkpoint and documented recovery behavior. An interrupted restore is restartable without duplicating or corrupting verified files.

## 9. Disk, boot and storage safety

- Use Windows-native, supported resize APIs; never write a home-built NTFS shrinker.
- Never assume Windows `C:` is physical disk 0 or Linux `/dev/sda`; use stable volume identifiers and verify device topology again after reboot.
- Never repartition all disks because a command defaulted to whole-disk erase. Apply allowlists and unit-test the exact generated partition plan.
- Do not erase EFI, OEM/recovery, external, or additional user-data partitions until individually included in an explicit and reviewed plan. If removal is necessary for full replacement, show and validate it explicitly.
- Avoid installing onto a running/mounted source volume. Reboot into an authenticated installer with the necessary environment available independently of partitions being erased.
- A UEFI `BootNext` entry may provide a one-shot installer boot. Test firmware variability and ensure Windows remains the fallback before the destructive boundary.
- Secure Boot must only be supported with a real tested trust/signature chain. Never bypass or disable it silently.
- Validate BitLocker/device encryption status, require access to recovery material where applicable, and treat suspend vs decrypt as distinct operations. A suspended BitLocker volume is **still encrypted**. No secrets or unencrypted backup should be left exposed without user understanding.
- **Do not implement single-disk streaming copy-and-delete in MVP.** Ordinary file deletion frees NTFS filesystem blocks; it does not transfer those blocks to an ext4 partition. Partition shrinking frees contiguous space only at the partition boundary and may be limited by unmovable files. Reclaiming space progressively is a separately designed on-disk migration protocol requiring much more than copying files.
- Keep the independent backup after migration until the user elects to remove it.

## 10. Failure and recovery expectations

Build a failure matrix describing action, data state and recovery for: download corruption, no network, insufficient free space, backup target disconnect, source read errors, file changed during copy, system sleep, power loss, failed Windows shrink, EFI capacity issue, UEFI boot failure, wrong-disk discovery, installer failure before/after erase, package mirror failure, damaged Linux bootloader, GPU display failure, and interrupted restore.

For every failure, specify:

- Whether original Windows remains intact and bootable.
- Whether the backup contains all selected user data and how the user accesses it.
- Whether an automatic retry is safe and idempotent.
- Which user action, if any, is required.
- What log and recovery report are produced.

Never advertise *zero risk*. A backup and checksum verification reduce risk; they cannot prevent simultaneous disk loss, filesystem bugs, hardware faults, or all user mistakes. Never show `Complete` if restore verification failed.

## 11. Security and privacy

- Open source with reproducible/reviewable builds when practical, and a maintained dependency manifest/SBOM.
- Signed releases and authenticated update/download channels; verify Arch installation artifacts and package integrity with their supported trust model.
- Privileged operations isolated behind narrowly scoped services with validated input; do not run arbitrary user-selected paths as shell commands.
- No telemetry by default, no automatic upload of file manifests, private paths, credentials, hardware IDs or logs.
- Secret-handling policy for stored account credentials, browser credentials, SSH keys, network passwords and encryption keys. Protect sensitive backups at rest and in transit.
- Secrets do not appear in CLI arguments, JSON logs, crash reports or temporary files. Remove temporary sensitive state when it is no longer needed.
- All paths are treated as untrusted: normalize, detect traversal/reparse loops, validate restore roots, and prevent overwriting Linux system paths or escaping user destinations.
- A compromised update server must not be able to inject unverified installer commands or arbitrary packages.
- Include `SECURITY.md` with coordinated disclosure instructions.

## 12. Tests and acceptance criteria

### Automated tests

- Unit tests for file mapping, Windows path normalization, Known Folder discovery, plan validation, conflicts, symlink/reparse handling, cloud placeholder detection, selected-file accounting, backup hash checks and restore permissions.
- Property-based tests for manifest consistency: every selected file has exactly one explicit result; no excluded item is silently reclassified.
- Integration tests with synthetic large profiles, deep trees, millions of small files, zero-byte files, files over 4 GiB, sparse files, Unicode, case collisions, ACL failures, hard links, alternate streams and changed/locked files.
- A **default dry-run mode** that creates inventories and plans but has no API access to write partitions or configure UEFI entries.
- Fault-injection tests after every state transition, including simulated crashes and loss of backup connection.
- Virtual-machine end-to-end tests using disposable GPT/UEFI Windows images with an independent backup disk, covering both success and controlled failure.
- Physical-hardware validation on an explicit list of models/configurations before claiming general support.

### MVP end-to-end definition of done

1. A supported Windows 10/11 computer can launch Nomad and complete the pre-migration wizard with **no terminal**.
2. Nomad identifies the source disk and separate backup device correctly and will not erase any non-approved disk or volume.
3. The current user's complete selected profile, including hidden content, and at least one arbitrary additional directory such as `C:\Program Files\SelectedApp` are backed up and fully hash-verified.
4. No selected file is silently skipped. If backup is incomplete, **the Windows installation is not erased**.
5. Nomad stages and authenticates an Arch installer without a separate USB installer and reboots into it unattended on supported hardware.
6. Arch installs with a usable KDE Plasma desktop, correct user account, functioning networking, audio, graphics, bootloader and essential GUI tools.
7. Data is restored into documented Linux destinations, preserves original-path mapping, verifies file contents and produces a migration report.
8. Selected Windows application files are retained as archive data; they are not falsely reported as installed functional Linux software.
9. Cancellation or failure before the destructive boundary leaves the original Windows system usable (subject to documented tested recovery).
10. Failure after the destructive boundary preserves independent selected-file backups and gives clear, tested restoration instructions.
11. Automated VM tests cover success, backup disconnection, invalid installer, modified disk identity, incorrect erase plan, crash/restart and restore interruption.
12. Release documentation describes precisely the supported hardware and explicitly warns that a file-only backup does **not** preserve a bootable Windows OS.

### Non-goals for MVP

- Zero-backup near-full-disk migration.
- Automatic universal Wine/Proton compatibility for arbitrary Windows applications.
- Full Windows rollback without a restorable Windows system image.
- Multi-disk RAID, BitLocker scenarios not explicitly validated, BIOS/MBR, enterprise domain enrollment, or universal Secure Boot support.
- Generic distribution selection; Nomad targets Arch.
- Automatic migration of every possible application-specific secret, license, Windows service or registry entry.

## 13. Implementation roadmap for an AI coding agent

**Phase 0 — Investigate and specify.** Inspect the current iGloo and Tunic repositories and official Arch tooling; review licenses, platform support and source code. Create an architectural decision record on whether to fork iGloo, reuse specific patterns, or start independently. Preserve attribution and comply with GPL terms. Produce a credible safety model and hardware support matrix **before coding destructive behavior**.

**Phase 1 — Windows read-only prototype.** Implement the wizard shell, preflight, volume inventory, profile scanning and arbitrary directory selection. Generate a complete JSON manifest and plan. Produce a running GUI and tests. Absolutely no disk writing beyond Nomad's own ordinary app data.

**Phase 2 — Backup and restore engine.** Implement snapshot-aware copying, resume, hashing, verification, special-file detection, archive path mapping and a restore utility against disposable test destinations. Validate that unknown files are not skipped. Add independent backup checks.

**Phase 3 — Non-destructive Arch installation lab.** Use isolated VMs, separate blank target disks and official Arch tooling. Build unattended install, package profile, first boot and post-install health checks. Do not touch users' Windows partitions.

**Phase 4 — Boot staging and safety integration.** Add authenticated internal-disk boot, `BootNext` or equivalent supported one-time boot mechanism, installer revalidation, persistent journal and automatic fallbacks in VMs. Test supported/unsupported Secure Boot paths.

**Phase 5 — Controlled full replacement in VMs.** After verified backup, test destructive replacement of *disposable test Windows disks* and automated restore. Inject faults into every stage and verify backup recoverability. Require code review and explicit opt-in safety gates.

**Phase 6 — Limited hardware alpha.** Publish only for a narrow validated hardware matrix, label as experimental, and require complete verified independent backup. Expand support based on documented successes and failures.

**Phase 7 — Enhancements.** Better app migration, user-account mapping, cloud storage, more hardware, alternate desktops, advanced encryption and safer storage options.

**Phase 8 — Research, not an MVP commitment.** Investigate in-place same-disk migration without a second copy. Start with a disk/partition graph model and formal invariants, a simulator, fully disposable images and exhaustive fault injection. Do not expose such a mode to ordinary users without an independently reviewed recovery design.

### Work rules for coding agents

- Implement phases in sequence. Do **not** implement unsafe shortcuts just to demonstrate a full migration.
- Start by generating `ARCHITECTURE.md`, `SAFETY_MODEL.md`, schemas, fixtures, an implementation backlog, and a working read-only GUI.
- For every feature, add automated tests and explicit support/unsupported behavior.
- All operations modifying boot settings, partition tables, filesystems or encryption must be separately gated and disabled by default during development.
- No destructive command may run against non-virtual/disposable devices in CI or test tools. Prefer mocked device interfaces and explicit allowlisted disposable test disk IDs.
- Never infer that a file copy succeeded from its existence; validate content and metadata as the contract requires.
- Avoid broad fallback logic (`catch and continue`) for backup, restore and disk operations; fail closed with human-readable reasons.
- Record decisions and unresolved risks instead of inventing unsupported guarantees.
- Keep a prioritized issue list with acceptance criteria and screenshots or test evidence for completed wizard and migration stages.

## 14. Important technical references

- iGloo: https://github.com/gillesduif/iGloo — open-source Windows→Linux migration wizard (GPL-3.0-or-later), its architecture/safety documentation and Windows/Linux handoff are relevant reference material.
- Tunic: https://github.com/mikeslattery/tunic — older open-source Windows-launched Linux installer without USB media.
- Archinstall: https://github.com/archlinux/archinstall and https://archinstall.archlinux.page/ — official Arch installation library/guided tooling.
- ArchWiki, dual boot and EFI: https://wiki.archlinux.org/title/Dual_boot_with_Windows
- Microsoft, partition shrinking: https://learn.microsoft.com/windows-server/storage/disk-management/shrink-a-basic-volume
- Microsoft, BitLocker: https://learn.microsoft.com/en-us/windows/security/operating-system-security/data-protection/bitlocker/operations-guide

## 15. One-paragraph build directive

**Build Nomad as a free, open-source, graphical Windows application that replaces a supported Windows installation with standard Arch Linux and KDE Plasma after a single setup wizard and explicit confirmation. Preserve the entire selected user profile and arbitrary selected extra directories (including Program Files) through an independently verified, resumable backup-and-restore process. Boot a verified Arch installer from internal storage without requiring a USB installer, safely and automatically perform a narrowly scoped installation, restore and verify the selected data, configure the Linux environment, and produce a complete migration report. Use existing proven tooling wherever possible, with iGloo as a reference or possible starting point. Never perform irreversible storage operations without plan validation, verified independent data protection and test evidence. Treat same-disk copy-and-delete migration and universal Windows-app compatibility as future research, not implied MVP features.**
