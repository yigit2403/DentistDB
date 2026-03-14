# Changelog

All notable changes to DentistDB are documented here.

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
