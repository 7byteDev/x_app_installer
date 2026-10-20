# Technical architecture and implementation plan

## 1. Boundaries and solution structure

UI -> provider/queue abstractions -> shared services. Providers discover release metadata; they never execute software. Core contains service contracts and version comparison, Models carries data, Providers loads and resolves catalog entries, DependencyResolver orders a graph, Downloads streams bytes, Security verifies packages, Installation executes and confirms outcomes, Logging emits structured entries, UI binds observable view models. No software-specific switch exists in the queue. Source strategies are reusable modules selected by catalog metadata; source-specific parsers live in Providers.

## 2. Metadata schema

`catalog/apps.json` is deployment content and `apps.schema.json` documents its format. Every definition has an immutable ID/name, configurable categories, description and visual identity, homepage/source, repository/asset selection, format/arguments, architecture policy, dependency edges with minimum versions, registry detection pattern, download host allowlist, optional publisher, signature/hash policy, elevation requirement and availability state. Version/download information is a runtime `ReleaseInfo`; application version numbers are never stored in the catalog. Catalog is trusted local application configuration, not a remote plugin execution channel. Add a JSON definition for an existing source type; introduce an `IApplicationProvider` strategy for a genuinely new feed.

Future extensions: maximum/exclusive version constraints, runtime-vs-application prerequisite types, provider-specific options with declared capability descriptors, reboot-policy metadata, custom install-location detection, size enrichment, signed catalog updates and isolated provider plugin loading. These are design extensions, not implemented claims.

## 3. Dependency model

Directed edge A -> B means A requires B. Resolve the selected roots recursively using three-color DFS. A gray node indicates a cycle; an unknown ID is an actionable configuration error. Omit prerequisites only when detected versions satisfy both lower and upper bounds; still inspect their transitive prerequisites. Append after visiting children, deduplicate by ID. UI retains manual roots, rebuilds automatic additions, explains which selected applications require them and prevents direct unchecking of automatic entries. Failure or skipping blocks downstream applications while independent queue entries continue. Existing compatible dependencies are not reinstalled. All three initial POC applications are independent; synthetic graph cases exercise transitive resolution.

## 4. Installation state machine

Queued -> Checking -> [AlreadyInstalled | Downloading -> Verifying -> Installing -> Confirming -> Succeeded]. Any fallible step -> Failed with step/message/exit code -> Retry from Checking, Skip to next entry, or Cancel remaining. Unstarted dependents of failed entries -> Skipped. Cancellation before execution -> Cancelled. Cancellation during an active installer waits for the process to exit, verifies that result and stops later work. Success includes confirmed installed version; exit code 3010 is successful with restart-required status.

## 5. UI and view models

MainWindow is view orchestration: dialogs, navigation and official-link launching. MainViewModel owns catalog items, category/search filtering, selection graph, queue/refresh lifetimes, settings, progress and activity events. AppItem exposes selection, version, status and automatic-selection explanation with INotifyPropertyChanged. UI-thread updates use DispatcherQueue and Progress<T>; network/file/process work is asynchronous. Views contain no application catalog. Layout: header, left category list, center application list, right details, bottom two progress bars/activity log and action bar. Settings is a native ContentDialog.

## 6. Security model

Only HTTPS, only exact allowlisted hosts, redirect limit six. No shell-based download scripts or execution-policy bypass. Hash verification always runs if metadata supplies it. Windows native Authenticode policy checks signature and certificate trust; provider publisher is checked for Node/Git. Signature setting cannot disable mandatory provider policies. The explicitly unsigned 7-Zip package requires a release SHA-256; invalid signatures do not receive that exception. A file lock covers verification through execution to prevent concurrent package replacement. Do not request elevation for the UI. Installer invocation uses documented provider arguments and direct process launching. Restore-point and portable handling are unavailable until their behavior is verified. Never automatically accept third-party license terms.

## 7. Error model

Version failures remain per application; other apps load. Download retries transient transport/I/O errors at most twice after the first attempt. Corrupt verified files are removed before retry. Installer failure includes application, state, exit code, message and resolution. Retry repeats release/installed checks, Skip records the failure and continues independent entries, Cancel records unstarted cancellation. Logs carry DEBUG/INFO/SUCCESS/WARNING/ERROR; disk logs are optional. Cleanup failure after successful installation is a warning, not installation failure. Final summary separates successful, failed, skipped, current and cancelled.

## 8. Current coverage and remaining acceptance

All 14 catalog integrations are enabled with live release resolution. Shared deployment supports MSI, EXE, hash-verified portable ZIP and integrity-verified npm tarballs. Executable installers without documented unattended parameters are explicitly interactive. Dependency edges include minimum and exclusive-maximum versions, optional edges, audited notes and source links. All graph constraints are merged before queue creation and revalidated before dependent execution.

Installed-version discovery includes a shared, bounded conventional-folder index, registry views, configured roots, runtime environment, native executable metadata, full-JDK release/PE inspection and managed receipts. Discovery never launches candidate binaries. The common-folder index stops at two seconds or 2,500 enumerated directories, limits depth to three, and ignores junctions/network drives. Registry detection and online release resolution are separate; local results appear first. Detected roots are displayed in Details and can be overridden in Settings. Vendor-specific runtime metadata stays in source modules and catalog definitions; archive layout/version/PATH and npm identity are metadata-backed.

Native Windows caption space is separate from application header actions; minimum window size keeps the three-column layout usable. UI smoke tests exercise all selections, automatic dependencies and lock/release behavior, category/search and all release providers.

Still required for release certification: disposable Windows VM first-install/upgrade/UAC/reboot acceptance across all providers, interactive installer handoff cases, exhaustive signer rollover/older-OS coverage, and broader archive fault injection. Restore-point creation and unrestricted PowerShell installer execution are not enabled. MCP registrations, model downloads, account/license setup and Windows feature changes remain explicitly separate actions.
