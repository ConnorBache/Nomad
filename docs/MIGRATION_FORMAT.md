# Migration plan format

The preview emits a UTF-8 JSON document with `schemaVersion` `1.0`, `kind` `nomad-read-only-plan`, a `payload`, and a lowercase hexadecimal SHA-256 `planHash`. The hash covers the exact UTF-8 bytes of the JSON `payload` value, starting at its opening `{` and ending at its matching `}`. Formatting the payload after export changes the hash.

The payload contains a hardware snapshot, explicit roots and exclusions, a sorted metadata inventory, discovery issues, and the Arch/Plasma target. Identical observed metadata and selections produce identical bytes. Changes in free space, timestamps, discovered paths or provider results can change the hash between scans.

| Entry status | Meaning |
| --- | --- |
| `discovered` | Nomad observed metadata and can propose an archive path. |
| `excluded` | An explicit exclusion applies to the entry and any descendants. |
| `blocked` | Nomad encountered an inaccessible or unsupported item, or required metadata was unavailable. The note describes the condition. |

For a blocked directory the entry represents the unresolved subtree. Requested exclusions and root discovery issues remain visible even if a directory cannot be traversed. Metadata is advisory, including drive-to-volume associations. The proposed archive location is relative to the future Linux home, for example `Migrated Windows Files/C/Program Files/Example/file`. Future backup, journal, authorization, and report formats require separate versioned schemas and independent tests.

See the [schema](../schemas/migration-plan.schema.json) and [synthetic example](../tests/fixtures/example.nomad-plan.json).
