# Code Signing Policy

**Current status:** Windows signing is being set up via **Azure Artifact Signing** (a paid Microsoft
service), issued under the maintainer's own validated identity — so the publisher shown to users will be
the maintainer's real name. Until that is active, Windows builds ship **unsigned** (see
[INSTALLATION_GUIDE.md](INSTALLATION_GUIDE.md) for the one-time SmartScreen bypass). A prior SignPath
Foundation application was declined at this early stage for lack of public-visibility signals, with an
invitation to reapply as the project grows. This document defines how signing is governed once a
certificate is in place — the roles, process, and privacy commitments below apply regardless of provider.

APRS Command is a free, open-source (GPL v3) cross-platform APRS client for amateur radio. This
document describes who is trusted to build, review, and approve signed releases, how signing is
performed, and how the project handles user data — the information the SignPath Foundation and end
users need to trust a signed build.

---

## Project roles

APRS Command is currently maintained by a single volunteer developer, so one person holds all three
roles below. The roles are stated explicitly so the trust model is clear and so it remains correct if
additional maintainers join later.

| Role | Who | Responsibility |
|---|---|---|
| **Authors / Committers** | [James Rospopo — KE4CON](https://github.com/KE4CON) (repository owner) | Trusted to commit and merge changes to the source in version control without additional review. |
| **Reviewers** | [James Rospopo — KE4CON](https://github.com/KE4CON) | Every change proposed by a non-committer (a pull request) is reviewed by a maintainer before it is merged. |
| **Approvers** | [James Rospopo — KE4CON](https://github.com/KE4CON) | Every signing request is manually approved by a maintainer, who decides whether a given release may be code signed. |

GitHub permission reference: the sole owner/committer is the repository owner,
[KE4CON](https://github.com/KE4CON), on [github.com/KE4CON/APRS-Command](https://github.com/KE4CON/APRS-Command).

## What is signed, and how

- **Only the project's own binaries, built from the project's own source, are signed** — specifically the
  Windows executable and the Windows installer (`APRSCommand-*-windows-x64-Setup.exe`).
- **Builds are produced by the project's GitHub Actions release workflow** (`.github/workflows/release.yml`)
  from a tagged commit (`v*.*.*`) in the public repository — not on a developer's personal machine.
- **Every signing request is approved manually** by an Approver; there is no automatic or unattended
  signing.
- When signing is active, the signing certificate's private key is held by the signing provider's secure
  infrastructure (an HSM); the project never possesses or handles the private key.
- macOS and Linux builds are distributed unsigned, as is standard for open-source software distributed
  outside an app store.

## File metadata

Signed binaries carry accurate file metadata, configured in the build and enforced at release time:

- **Product name:** APRS Command
- **Author / Publisher:** James Rospopo (KE4CON) — a sole individual developer, not a company (the Windows "Company" metadata field holds the author's name)
- **Copyright:** © 2026 James Rospopo (KE4CON)
- **File description:** A cross-platform APRS client for amateur radio operators.
- **Version:** the released tag version (e.g. `1.0.0`)

## Account security

All maintainer accounts with commit or signing authority have **multi-factor authentication (MFA)
enabled** on GitHub and on SignPath.

## Privacy policy

APRS Command is a **local desktop application with no backend server, no account system, and no
telemetry, analytics, or crash reporting of any kind.** Nothing is collected from users or sent to the
project maintainer. All settings — including any API credentials an operator enters — are stored only in
a local settings file in that operator's own OS user profile, and leave the machine only in the specific,
user-enabled network requests the application documents (for example APRS-IS, or optional integrations
the operator explicitly turns on).

The project's dedicated privacy policy is published as [PRIVACY.md](../PRIVACY.md); the full,
source-backed security and data-handling detail is in [SECURITY.md](../SECURITY.md). Every claim in both
can be verified against the source code.

## Reporting and contact

Security issues: see [SECURITY.md](../SECURITY.md) (GitHub Issues, or private vulnerability reporting on
the repository). General contact: via [github.com/KE4CON/APRS-Command](https://github.com/KE4CON/APRS-Command).

---

*This policy is published so that anyone — the SignPath Foundation, end users, and future maintainers —
can see exactly who is trusted to sign APRS Command releases and how user data is handled.*
