# Architecture

## Windows planning preview

`Nomad.Windows.App` owns the WinForms wizard. Windows Known Folder APIs locate redirected folders. A fixed, noninteractive Windows PowerShell query retrieves CIM and storage-provider information. User-selected paths are never passed to PowerShell. The process runs without elevation, with a deadline, and reports unavailable data as unknown.

`Nomad.Core` owns selection normalization, metadata scanning and the plan format. Scans use explicitly selected paths, include hidden entries, and report blocked subtrees. Matching uses ordinal path spelling to preserve case-distinct Windows names. Files are mapped under a per-drive archive below the future Linux home. Standard home-folder mapping, Windows file-identity deduplication and alternate data stream accounting remain open.

The only output is a JSON plan saved to a new file chosen in the wizard. The plan is a point-in-time observation of a live filesystem. File contents, sharing locks, snapshot consistency, and backup readability are outside the preview's scope.

## Planned handoff

1. An elevated Windows backup component will use a supported snapshot mechanism, record stable source identities, and write a resumable manifest on independent media. A verification pass will independently read the saved bytes and check every selected entry.
2. Boot staging will use a pinned and authenticated Arch image. An installer environment booted from internal storage must remain available independently of anything in the approved erase scope.
3. The Linux agent will re-resolve disks, confirm the complete backup from its own environment, and validate the immutable approved plan before handing the allowlisted operation to a pinned Arch installation backend.
4. Restore will validate destination paths, verify bytes and practical permissions, and generate a report. First boot will verify the KDE session, login, graphics, networking and audio on the qualified hardware.

See the [safety model](SAFETY_MODEL.md) for the authorization boundary and the [implementation status](IMPLEMENTATION_STATUS.md) for acceptance criteria.
