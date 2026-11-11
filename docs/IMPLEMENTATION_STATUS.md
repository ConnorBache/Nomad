# Implementation status

| Phase | Status | Acceptance criteria still open |
| --- | --- | --- |
| 0. Investigation | ADR, safety model, hardware matrix and preview schema written | Select and pin the exact Arch image and archinstall version in the installation lab |
| 1. Windows planning preview | Wizard, hardware and volume inventory, registry application inventory, selection, metadata scanner, and JSON export implemented | Hosted Windows CI passed build, synthetic scanner and plan tests, and self-contained packaging. Verify keyboard navigation and GUI in a disposable Windows VM. Complete special-entry handling and scale tests. |
| 2. Backup and restore | Pending | Resolve source paths through a consistent snapshot. Verify every byte on independent media, reject ambiguous topology, and resume after interruption. |
| 3. Arch lab | Pending | On an isolated blank disk, install and boot Arch with KDE, then verify login, graphics, audio, networking, and updates. |
| 4. Boot staging | Pending | Authenticate the image and exercise one-time internal boot, failed boot recovery, installer-side identity checks, and journal restart. |
| 5. End-to-end VM replacement | Pending | Exercise an approved erase and restore with independent disks. Inject image tampering, wrong-disk detection, power loss, disconnects and interrupted restores. |
| 6. Hardware alpha | Pending | Record successful runs on individually identified physical configurations and publish an exact compatibility matrix. |
| 7. Enhancements | Pending | Expand independently tested app mappings, hardware and migration features. |
| 8. Same-disk research | Separate research milestone | Model and test recoverability using disposable images before proposing any user-facing support. |

Highest-priority Phase 1 issues are `Known Folder` fallback tests, case-sensitive selection and file identity, useful preflight explanations when Windows denies a query, and verifying large-directory cancellation. Phase 2 starts with snapshot consistency, verified manifests and physical independence tests.
