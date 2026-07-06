# User Personas — Asset Tracking System

## P1 — SSgt Maria Delgado, Unit Custodian ("the customer")

Equipment custodian for a 120-person unit. Manages ~800 devices alongside her primary duties. Currently keeps a spreadsheet and a binder of signed hand receipts; inventory season means weeks of re-keying PDFs.

Goals: upload the inventory PDF and have the system do the typing; know immediately when something failed to parse; find any device by serial in seconds.
Frustrations: duplicate serials from typos; no proof of who changed what; tools that fight her CAC.
Success looks like: uploads Monday's receipt scan, gets 46 of 50 assets auto-created, fixes 4 flagged rows, done in 15 minutes.

## P2 — Capt James Okafor, Squadron Admin

Oversees 6 units. Accountable to leadership for property accuracy. Needs roll-up visibility without micromanaging custodians.

Goals: squadron dashboard (counts, status breakdown, stale records); approve inter-unit transfers; audit view when something goes missing.
Frustrations: getting six different spreadsheets in six formats; no way to see history when equipment is unaccounted for.
Success looks like: monthly property review takes an afternoon, not a week.

## P3 — Dana Whitfield, System Administrator

Civilian IT specialist administering the platform for the whole organization. Owns the review queue, user provisioning, and data quality.

Goals: burn down the review queue efficiently (side-by-side doc vs extracted fields); provision users and roles fast; trust the audit log completely; bulk import legacy spreadsheets.
Frustrations: parsers that fail silently; irreversible destructive actions; access requests via email with no workflow.
Success looks like: review queue under 20 items, every decision traceable, zero "who deleted this?" mysteries.

## P4 — A1C Tyler Brooks, Unit Member

Junior member who occasionally needs to check what's assigned to him or look up a device's status.

Goals: log in with CAC, search, read. Nothing else.
Frustrations: being shown buttons he's not allowed to use; complex UI for a 30-second task.
Success looks like: CAC in, serial number in the search box, answer, done in under a minute.

## Design implications

- Delgado drives the upload/review UX — the core loop must be fast and error-transparent.
- Okafor drives dashboards and approval workflows — read-mostly, aggregate views.
- Whitfield drives admin ergonomics — keyboard-friendly review queue, safe destructive actions (confirm + undo via soft delete), audit search.
- Brooks justifies aggressive permission-based UI trimming — hide, don't disable, what a role can't do.
