# Safety model

## Preview boundary

The current executable can read Windows and file metadata and create a new plan export. No privileged process, partition writer, boot manager, backup verifier, or migration executor is shipped. The plan kind is `nomad-read-only-plan`. Its SHA-256 checksum detects byte changes if independently checked. An executor must reject this format as authorization.

## Future pre-erase gate

The installer must reject the operation unless it can establish all of the following immediately before the first destructive write.

1. The explicit user approval binds to an unchanged plan, the exact source/target disk identity and geometry, and an allowlist of individually approved partitions.
2. The live installer is authenticated, and its own runtime and journal can survive removing the target partitions.
3. Every selected item has an explicit resolved manifest status. Every selected regular file exists in the independent backup and passes content verification in the installer environment.
4. The backup's physical disk identity differs from every disk in the erase scope and remains readable by the installer. A drive letter or partition number alone cannot establish independence.
5. The disk topology, firmware state, health, encryption and power checks match the supported configuration.

Authorization also requires the expected active journal state. Writes and journal checkpoints must be ordered and flushed so that an interruption can resume only from a known state. Unknown or conflicting state halts and preserves available recovery data. A separate file backup supports data recovery. A bootable Windows rollback requires a restorable Windows system image.

## Test boundaries

Use hosted Windows runners for synthetic metadata and contract tests. Destructive tests belong in isolated VMs with disposable, explicitly identified virtual disks. The preview includes no disk-mutating test helpers. Same-disk copy-and-delete remains a separate research milestone.
