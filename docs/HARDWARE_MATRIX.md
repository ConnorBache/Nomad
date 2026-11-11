# Hardware matrix

The Windows planning preview targets Windows 10 and Windows 11 x64. Compatibility testing has yet to qualify any configuration for an actual migration.

| Area | First VM target | Excluded or pending validation |
| --- | --- | --- |
| Firmware | x64 UEFI, GPT, Secure Boot disabled in the VM configuration | Legacy BIOS, MBR, ARM, enabled Secure Boot |
| Source storage | One disposable Windows system disk, basic GPT with NTFS | Dynamic disks, Storage Spaces, hardware RAID, dual boot, ambiguous recovery or OEM partitions |
| Backup | Separate disposable virtual disk; verify identity and accessibility in both OS environments | Same physical disk, unknown storage topology, network backup |
| Encryption | Unencrypted Windows volume and test data | BitLocker, EFS and Linux disk encryption |
| Graphics and input | VM graphics and standard USB/HID inputs | Physical GPU families, hybrid graphics, suspend and resume |
| Network and power | Wired VM NIC, reliable package mirror, stable host power | Wireless setup, offline installation, low battery |
| Desktop | Official Arch Linux x86-64, KDE Plasma, SDDM, NetworkManager and PipeWire | Physical hardware support until hardware tests pass |
