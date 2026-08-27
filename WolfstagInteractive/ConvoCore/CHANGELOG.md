# Changelog

All notable changes to the ConvoCore package are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Added

- **Collection variables** (`CollectionInt`, `CollectionString`) in `ConvoVariableStore`: named groups of sub-entries (string sub-key → int or string) for inventory-style state such as item counts, relationship maps, discovered locations, etc.
  - Full sub-key API on the store: `SetCollectionInt/String`, `TryGetCollectionInt/String`, `HasCollectionEntry`, `RemoveCollectionEntry`, `GetCollectionCount`, `GetCollectionKeys`, `ClearCollection`, `ResetVariable`. The backing dictionary is never exposed.
  - Authored Collections are never modified at runtime; the first change works on an in-memory copy, and `ResetVariable` restores the authored defaults.
  - Change events fire once per mutation with the affected sub-entry value as payload; `ClearCollection` fires a single event.
  - Inspector support: reorderable Collection Defaults list with duplicate/empty sub-key validation, plus a Play Mode summary row that highlights orange once the Collection has been modified during the session.
  - Save-snapshot schema bumped **1.0 → 1.1** (list-of-pairs representation). Existing 1.0 saves load unchanged through a pass-through migration step.

### Changed

- Variable Store inspector: the read-only column is now labeled **Read Only** (previously **R/O**).
