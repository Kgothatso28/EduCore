# EDUCORE 1.2 workspace and source inventory

## Workspace

- Resolved workspace root: `SMS/`
- Target: existing `SMS/EDUCORE 1.2/`
- Target state before work: only the existing `.qodo/agents/` and `.qodo/workflows/` directories; no source, configuration, or user files were present.
- Git metadata: no `.git` directory in the target or either source project. No branch or local history was available. Existing `.qodo/` content is preserved.
- Original projects: sibling directories `SMS/lav_sms-master/` and `SMS/SkyLearn-main/`. Neither is modified by this work.

## Source projects

### `lav_sms-master`

- Laravel 8 / PHP project using Composer; dependencies are pinned in `composer.lock`.
- Organized around `app/Http/Controllers`, custom role middleware, `app/Models`, repositories, form requests, migrations, Blade resources, and `routes/web.php`.
- Domain coverage includes users/roles, students, classes/sections, exams/marks, timetable, fee/payment records and receipts, library books/requests, dorms, and promotions.
- The README explicitly identifies some calendar/noticeboard, accountant/librarian, library, and study-material features as incomplete or work-in-progress.
- `tests/` contains only example feature/unit tests; no substantive tenant-isolation tests.
- README includes sample seeded login credentials. Treat these as unsafe and replace/remove them before any deployment.
- Runtime baseline is Laravel `^8.40`, PHP `^7.2 || ^8.0`. Laravel 8 security support ended in January 2023.

### `SkyLearn-main`

- Django project using Python and requirements pinned in `requirements/base.txt`; Django is pinned to `4.0.8`.
- Organized into Django apps: `accounts`, `core`, `course`, `result`, `search`, `quiz`, and `payments`.
- Domain coverage includes student/lecturer/parent identities, courses and resources, assessments/quizzes, grade calculation, semester results, and a payments app.
- `TODO.md` identifies school calendar/news integration, dynamic dashboard analytics, fee-payment integration, and other features as unfinished.
- Has Django test CI, but course and quiz test modules are placeholders; existing account tests focus on decorators and filters, not cross-tenant isolation.
- Development configuration defaults include SQLite, `DEBUG=True`, a fallback secret key, and a fixed allowed-host list. Django 4.0 is unsupported.

## Initial product implications

- LAVSMS is the stronger reference for traditional school administration; SkyLearn is the stronger reference for course content and online assessment.
- Neither is used as a code dependency or copied wholesale into EDUCORE. EDUCORE starts with a separate, modern C# architecture to avoid inheriting unsupported framework assumptions and single-school data models.
- Feature migration is deferred until the tenant/security foundation is verified. No business modules are considered implemented by this inventory.

## Inventory boundaries

This is a source-level inventory, not a full security audit, feature acceptance test, dependency audit, or production-readiness assessment. Findings describe the checked-out folders as inspected on 2026-10-15.
