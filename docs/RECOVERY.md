# Recovery

The Windows planning preview leaves Windows bootable and has no recovery operations. Close it or cancel a scan. A scan interrupted before export can be run again. If an export is interrupted during the write, discard the partial JSON and choose a new output file.

Future VM milestones must validate these recovery paths before release.

| Failure | Expected response |
| --- | --- |
| Unreadable, changed or missing source file | Stop before erase. Retain Windows and identify the unresolved selection. Repeat from a fresh snapshot after resolving the problem. |
| Insufficient space, invalid download, or unavailable network | Stop before erase. Preserve Windows and independently verified data. Retry after the failed prerequisite is resolved. |
| Backup disconnect or failed verification | Stop. Reconnect and re-identify the independent backup, then verify it from the active environment before resuming. |
| Resize or firmware staging failure | Stop before erase. Validate the Windows fallback path and provide the failed checkpoint. |
| Wrong disk or altered partition layout | Stop before erase and require a new review. |
| Power loss or package failure after erase starts | Keep the independent backup. Re-enter an authenticated environment and reconcile the journal and approved disk before retrying. |
| Restore or desktop validation fails | Keep the backup. Resume restore from verified checkpoints or repair the desktop before marking the migration complete. |

A file backup preserves selected data. A Windows system image is required to restore the original bootable Windows installation.
