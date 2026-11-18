# CARE Radiology — DICOM Enabler

[![Build CARE DICOM Enabler](https://github.com/care-ecosystem/care_radiology_dicom_enabler/actions/workflows/build.yml/badge.svg?branch=main)](https://github.com/care-ecosystem/care_radiology_dicom_enabler/actions/workflows/build.yml)

Three lightweight Windows services installed inside the hospital network that bridges DICOM imaging devices (X-ray, CT, MRI, Ultrasound) and CARE HMIS. It pulls the day's worklist down to the modality and pushes acquired images up to CARE — no retyping at the console, no studies stranded on a workstation.

| Service | Role | Listens on |
| --- | --- | --- |
| `CARE_MWL_Service` | Modality Worklist SCP — answers the device's "who is scheduled?" (C-FIND) | AE `MODALITYSCP`, port 2008 |
| `CARE_StoreSCP_Service` | Receives images pushed by the device (C-STORE) into `./SCP` | AE `STORAGESCP`, port 2007 |
| `CARE_SCU_Service` | Polls `./SCP` and uploads new files to CARE | — (outbound HTTPS) |

Flow: CARE order → device worklist query → acquire → C-STORE → `./SCP` → upload to CARE → study visible on the service request.

## Download and use

1. Install **MySQL** (root password `care`, or note your own).
2. Download the ZIP from the [Releases page](https://github.com/care-ecosystem/care_radiology_dicom_enabler/releases) and extract it (right-click → **Properties** → **Unblock** first if Windows marks it blocked).
3. Create two empty folders next to the EXE: `SCP` and `FAILED_SCP`.
4. Run `scripts\Initializer.bat` **as administrator** to create the local database. When asked, paste the facility's **CARE service-account token** (input is hidden). It is stored encrypted for this machine only; re-run the Initializer to replace it.
5. Run `CARE_DICOM_Enabler.exe` **as administrator** → **Configuration** tab → set AE titles and the **Facility ID** → **Save**.
6. **Install Services**, then **Start Services**. Check the **Log** tab for errors.

Verify: all three services **Running** in `services.msc`, and ports 2007/2008 listening —
`netstat -ano | findstr LISTENING | findstr ":2008 :2007"`

Full step-by-step with screenshots, troubleshooting and DCMTK test commands: **[Setup Guide](https://egovhealthcare.atlassian.net/wiki/external/NjFjNTEwN2ExYWQwNDMwODg3OGI3MDJiMmMxZmJkMzU)**.

## Latest release

**[v0.0.2](https://github.com/care-ecosystem/care_radiology_dicom_enabler/releases/tag/v0.0.2)** (pre-release, staging) — asset `CARE_DICOM_Enabler-v0.0.2.zip`.
Debug builds can fetch the full worklist without device linkage ([#75](https://github.com/care-ecosystem/care_radiology_dicom_enabler/pull/75)) · [full changelog](https://github.com/care-ecosystem/care_radiology_dicom_enabler/compare/v0.0.1...v0.0.2).

Releases are cut by pushing a tag; the CARE server URL and static API key for that environment are baked into the build:

```
git tag v0.0.3    && git push origin v0.0.3      # -> staging release
git tag jh-v1.0.0 && git push origin jh-v1.0.0   # -> built with the "jh" environment
```

> The baked static API key is readable in the `.exe.config` files inside a published ZIP. Treat it as rotatable.
> The CARE APIs the enabler calls today (worklist, modalities, DICOM upload, scan status) are authenticated with the facility's service-account token (`Authorization: Token <token>`). It is never baked into a build: the Initializer stores it in `care_config` (`care_service_token`), encrypted with Windows DPAPI for the machine, and the services decrypt it only when they make a call.

## Build from source

- Visual Studio 2019 or later, .NET Framework 4.7.2, run as Administrator
- `nuget restore CARE_DICOM_Enabler.sln` then build `CARE_DICOM_Enabler.sln` (fo-dicom, MySql.Data, Serilog, MaterialSkin.2 — see `packages.config`)
- CARE settings live in each service's `App.config` (`careBaseUrl`, static API key in `careToken` / `staticAPIKey`); the service-account token is set by `Initializer.bat`; the UI's **Configuration** tab writes the rest to `cfg/common.cfg` and the `care_config` table

CI (`.github/workflows/build.yml`) builds on every push/PR to `main` and on tags, and can be run manually with overrides for build configuration, platform, CARE server URL/token, worklist from-date, refresh interval and upload retries. Values resolve as: manual input → environment secret/var → workflow default → committed `App.config`.

## Know more

- [What the DICOM Enabler is and why](https://egovhealthcare.atlassian.net/wiki/external/NzVkNGIyYzg2NGUxNGZiYTg4NjU2ODNkNThhZDdiYzA)
- [Setup Guide](https://egovhealthcare.atlassian.net/wiki/external/NjFjNTEwN2ExYWQwNDMwODg3OGI3MDJiMmMxZmJkMzU) — install, verification, troubleshooting
