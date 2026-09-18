# Changelog

All notable changes to DentistDB are documented here.

## [0.4.0] - 2026-09-10

Clinical workflow release. The schema is upgraded automatically at startup (migration `ClinicalFeatures`).

### Added

- **Odontogram** per patient: tooth conditions (çürük, dolgu, kanal, kaplama, köprü, implant, eksik, protez, takip) coloured on the FDI chart, with per-tooth notes and a history built from treatment records.
- **Treatment plan** per patient: planned items from the price list with teeth and estimated price. One click turns an item into an appointment, a completed item into a treatment record, and completed unbilled items into an invoice.
- **Anamnesis**: structured medical history (allergies, medications, anticoagulants, diabetes, hypertension, heart disease, pregnancy, asthma, epilepsy, infectious disease, smoking), emergency contact and guardian. Risk flags feed the red banner on the patient card, agenda and dashboard.
- **Consent forms**: printable treatment consent and KVKK forms filled with patient and clinic data, templates editable in Settings, signature records per patient.
- **Prescription printing** from any treatment record with the clinic and dentist header.
- **Follow-up scheduling**: a "Kontrol" menu on completed appointments (1 hafta, 2 hafta, 1 ay, 3 ay, 6 ay) and repeating series on the appointment form with series cancel.
- **Daily cash report** (Gün Sonu): collections by method, invoices issued, due installments, appointment outcomes; printable.
- **Automatic nightly backup**: a background job copies the database and new scan files to a configurable folder at a configurable time, keeps the last N copies, and shows "Son yedek" in the sidebar. Manual "Şimdi yedekle" button.
- **Audit log**: every create, update and delete plus sign-in attempts, recorded with the acting account; filterable in Settings.
- **Scan viewer**: zoom, pan, rotate, flip, brightness/contrast, negative, side-by-side compare with synced pan/zoom, keyboard shortcuts.
- Clinic identity settings (dentist name, title, diploma no, address, phone, tax no) used on printouts.
- Printable day sheet with phone numbers, agenda keyboard shortcuts (N, ←, →, T, G, H, A, L, P), larger touch targets on touch devices.
- **Windows installer**: a single `DentistDB-Setup-<version>.exe` (Inno Setup) installs the app as a service, opens the LAN firewall, optionally sets up Tailscale, and shows the clinic address and generated PINs at the end. It ships a self-contained build, so the clinic PC needs no separate .NET runtime; upgrades keep data, PINs and settings, and data stays under ProgramData across uninstalls. Build it with the installer build script (installer/build-installer.ps1); the older zip plus Install-DentistDB.ps1 path still works.
- **Update notice**: the app polls the latest GitHub release twice a day in the background (never on the request path) and, for the admin, shows a "Yeni sürüm" pill in the sidebar and a download button on Ayarlar. Off in Development and tests (Updates:Enabled).
- **Release workflow**: pushing a `v*` tag builds the installer on GitHub Actions and attaches it to the release; the tag must match the csproj version.
- Installer: an existing install folder is emptied before copying (keeping the configured settings file) so stale files from older versions cannot linger, and an unchecked, twice-confirmed "start fresh" option wipes the database, images and keys (backups are kept).
- **Legacy data import**: a two-step tool (`tools/legacy-import/extract.ps1` + `tools/LegacyImport`) brings patient records across from the old Microsoft Access program. Names are title-cased, phones normalized, addresses assembled, and dates sanity-checked. TCKN is now optional on a patient (stored as null when unknown) so imported records without a national ID can be kept; such patients show a "TCKN eksik" badge and are prompted to complete it, while the new-patient form still requires a valid TCKN. Re-running the import updates rather than duplicates (matched on the old KisiNumara).
- **Tooth selector rebuilt**: numbers drawn inside the SVG (no drifting overlay buttons), click or drag to select several teeth, quadrant shortcuts (Üst çene, Alt çene, Sağ üst…), typed numbers field, removable chips, hover names (e.g. "16 · Üst sağ 1. büyük azı"), keyboard access, and a Kalıcı / Süt switch for deciduous teeth (51–85). Same chart powers the odontogram.

### Fixed

- Demo TCKNs now pass checksum validation.
- Client-side validation was initialised before jQuery Validation loaded, so Turkish messages and comma-decimal support never activated and invoice line totals mis-parsed "1.250,50".
- Price-list duplicate check compared names with SQLite's ASCII-only lower(), so "İşlem" and "işlem" were not seen as duplicates.
- Worker account could see and set the estimated price on the treatment plan form.
- Empty required text fields (patient name, backup folder) crashed with a 500 instead of a validation message.
- Agenda keyboard shortcuts navigated to HTML-encoded URLs (`&amp;`), dropping the date parameter.
- Edit forms now bind the record id from the route so a tampered form id cannot redirect the update.
- Scan upload: BMP files (the common export of panoramic device software such as Genoray Papaya / Triana) are accepted; the file type is decided by the extension and verified against the file header instead of trusting the browser, and DICOM/TIFF uploads get a message explaining to export as JPG/BMP.
- Tooth selector: a fast drag skipped small teeth because only the pointer's sampled positions were hit-tested; the path between events is now sampled. Deciduous chart teeth are spaced by arc length and aligned to the arch (no more overlapping shapes), and the quadrant shortcuts sit in a tidy 3-column grid.

## [0.3.0] - 2026-09-10

Complete overhaul of the UI and a number of correctness fixes. The database schema was rebuilt; this release is not an in-place upgrade of 0.1/0.2 databases.

### Added

- Appointment duration, live and server-side double-booking warning, one-click status changes (completed / no-show / cancelled), real month grid and week view, 30-day list view.
- Invoice line items with quantities and unit prices, an admin-managed procedure price list, invoice cancel/reopen/delete, payment reversal.
- Settings page: clinic name and working hours, in-app PIN change (PBKDF2 hashed, stored in the database), one-click SQLite backup download.
- Patient card with tabs (overview, appointments, treatments, images, invoices), photo avatars, age, next/last visit, outstanding balance.
- Global patient quick search in the top bar (Ctrl+K) with Turkish-insensitive matching.
- TCKN checksum validation and duplicate detection with the existing patient's name.
- PIN brute-force throttling, friendly 404/500 pages, print stylesheet for invoices.
- Test project with unit tests and end-to-end tests against the real app.

### Fixed

- Payment plans created on a new invoice were never saved (missing `SaveChanges`).
- Money fields typed as `1250,50` were rejected or silently misparsed under the tr-TR culture; decimals now accept both Turkish and invariant formats.
- Financial figures were shown to the Worker account on the dashboard.
- Demo patients were seeded into production databases.
- Missing `Home/Error` action made the production error handler return 404.
- Labels with lost Turkish diacritics ("Duzenle", "Arsivle", ...) restored throughout.
- Unused ASP.NET Identity and MySQL dependencies, committed build output and data-protection keys removed.

### Changed

- New light, dense clinical design (Bootstrap 5.3.3, no external fonts or CDNs).
- Schema managed by EF Core migrations instead of `EnsureCreated` plus hand-written `ALTER TABLE`s.
- Patient photos stored as binary instead of base64 text.

## [0.1.0] - 2026-03-14

Initial release of DentistDB.

### Features

- **Patients** – Create, view, edit, archive, and search patient records.
- **Appointments** – Schedule appointments with daily/weekly calendar views; track status (Scheduled, Completed, Cancelled, No-Show).
- **Previous Operations** – Record and review past dental procedures per patient.
- **Scans** – Upload, view, and manage X-ray and document scans (JPEG, PNG, PDF).
- **Billing** – Issue invoices and record payments; track outstanding balances.
- **Access Control** – PIN-based access for Admin and Worker roles without requiring user accounts.
- **Windows Service** – Ships with a PowerShell helper script to install the app as a Windows service.
- **Firewall helper** – PowerShell script to configure the Windows Firewall rule for the app port.
- **Production deployment** – Targets a Windows host with SQLite; VPN-only access via Tailscale or ZeroTier; full guidance in `DEPLOYMENT.md`.
