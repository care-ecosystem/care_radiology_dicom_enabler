# Build/Installer Portability Issues

Findings from testing whether the artifact produced by `.github/workflows/build.yml`
will actually run after being built and installed on a machine other than the original
developer's. Verified by installing the .NET Framework 4.7.2/4.8 Developer Packs on a
clean machine and compiling each project the same way `build.yml` does
(`msbuild <project>.csproj /p:Configuration=Release /p:Platform=AnyCPU`).

## 1. Critical — three service projects fail to compile on any device

`CARE_MWL_Service.csproj`, `CARE_StoreSCP_Service.csproj`, and `CARE_SCU_Service.csproj`
have **no reference** (`ProjectReference` or otherwise) to `CARE.DAL\CARE.Common.csproj`,
even though their code depends on it directly:

| Project | File(s) using `CARE.Common` | Missing symbols |
|---|---|---|
| CARE_MWL_Service | `PlexusMWLService.cs`, `WorklistService.cs`, `Model\WorklistItemsProvider.cs` | `Plexus.*`, `ucls_DAL` |
| CARE_StoreSCP_Service | `PlexusStoreSCPService.cs`, `Network\CStoreSCP.cs` | `Plexus.*`, `ucls_DAL` |
| CARE_SCU_Service | `Plexus_SCU_Service.cs` | `Plexus.*`, `ucls_DAL` |

Only `CARE_DICOM_Enabler.csproj` (the WinForms GUI) has the correct reference:

```xml
<ProjectReference Include="CARE.DAL\CARE.Common.csproj">
  <Project>{14733993-275c-4f65-bdbe-ed1aa1292f86}</Project>
  <Name>CARE.Common</Name>
</ProjectReference>
```

Building any of the three service projects reproduces:

```
error CS0246: The type or namespace name 'Plexus' could not be found
error CS0246: The type or namespace name 'ucls_DAL' could not be found
```

This is **not** environment-specific — it is a missing line in the checked-in `.csproj`
files, so `msbuild CARE_DICOM_Enabler.sln` (what `build.yml` runs) fails to build these
three projects on any machine, including the GitHub Actions runner itself. Likely cause:
an incomplete Plexus → CARE rename.

**Impact:** the GUI's Server Manager screen installs Windows services with `sc create`
pointing at `Care_MWL_Service.exe` / `Care_StoreSCP_Service.exe` / `CARE_SCU_Service.exe`
in its own folder. `sc create` does not check that the target file exists, so "Install
Service" reports success, but the service fails to start (Windows error 2: "cannot find
the file specified") on every device, because those EXEs were never built.

### Fix

Add a `ProjectReference` to `CARE.DAL\CARE.Common.csproj` in each of the three service
projects, mirroring the GUI project:

1. `CARE_MWL_Service\CARE_MWL_Service.csproj`
2. `CARE_StoreSCP_Service\CARE_StoreSCP_Service.csproj`
3. `CARE_SCU_Service\CARE_SCU_Service.csproj`

```xml
<ItemGroup>
  <ProjectReference Include="..\CARE.DAL\CARE.Common.csproj">
    <Project>{14733993-275c-4f65-bdbe-ed1aa1292f86}</Project>
    <Name>CARE.Common</Name>
  </ProjectReference>
</ItemGroup>
```

(Note the `..\` prefix — these projects live one level below the repo root, unlike
`CARE_DICOM_Enabler.csproj` which is at the root.)

After adding the reference, rebuild each project and confirm the `CS0246` errors are
gone and `CARE.Common.dll` (`Plexus.DAL.dll`) appears alongside the service EXE in the
shared root `bin\Release\` output folder. Also re-run `integration-test.yml`'s
"Verify service EXEs exist" step once fixed (see item 4 below — its path assumption
needs correcting too).

## 2. High — secrets and environment baked into every build

- `cfg\common.cfg` is committed to git with real values (encrypted DB connection
  string, `authURL: https://staging.carehmis.dpdns.org/api/token/`,
  `deviceName: CARE-DICOM-Enabler`) and `build.yml` copies it verbatim into every
  artifact. Any device that installs the artifact as-is talks to the staging backend
  under a fixed device identity unless `GenerateConnectionString.exe` is rerun first.
- The AES key used to "encrypt" `common.cfg` values is a static, hardcoded string in
  source (`CARE.DAL\EncKey.cs`), so the values aren't confidential to anyone with repo
  access — it's obfuscation, not encryption.
- `CARE_MWL_Service\App.config` ships with `careToken="RADOMSECRET"` (placeholder) and
  the same staging `careBaseUrl`, not parameterized per device/build.
- Restored NuGet packages include known CVEs: BouncyCastle 1.8.5 (several moderate
  advisories) and System.Text.Json 6.0.5 (high severity, GHSA-8g4q-xg66-9fp4).

### Fix approach

- Remove real credentials/URLs from `cfg\common.cfg` and `App.config` in source
  control; replace with placeholder/template values and document that
  `GenerateConnectionString.exe` (or an equivalent first-run wizard) must be used to
  generate the real config per device/environment before first start.
- Consider deriving the AES key from a per-deployment secret (e.g. DPAPI, a
  machine-specific key, or a value supplied at install time) instead of a constant in
  source.
- Bump BouncyCastle and System.Text.Json to patched versions.

## 3. Medium — functional bugs independent of device

- `UserControls\uctrl_ServerManager.cs`: service name strings are inconsistent across
  `deployType` 2/3 — install creates `"Care Store SCP Service"`, the enable/disable
  check looks for `"Care_Store_SCP_Service"`, and Start/Stop reference
  `"Plexus_Store_SCP_Service"` / `"Plexus StoreSCU Service"` (names that are never
  installed). Only `deployType=1` (the checked-in default) is internally consistent.
  **Fix:** unify all four code paths (install, enable-check, start, stop) on the exact
  same service name string per deploy type.
- `CARE_StoreSCP_Service\PlexusStoreSCPService.cs` `OnStart` swallows all exceptions
  into a log file only — if the DICOM server fails to bind (bad/missing config, port in
  use), Windows still reports the service as "Running" with nothing listening.
  **Fix:** on a startup failure, either rethrow (so SCM marks the service as failed) or
  call `Stop()`/exit so the failure is visible instead of silent.
- `sc.exe` is invoked via the hardcoded literal path `C:\Windows\system32\sc.exe`
  instead of `%WINDIR%\system32\sc.exe` or relying on PATH. Low risk, but not portable
  to a non-default system drive.

## 4. Low — CI workflow path bug (separate from build.yml)

`.github/workflows/integration-test.yml`'s "Verify service EXEs exist" step checks
`CARE_MWL_Service\bin\Release\CARE_MWL_Service.exe`, but each service project's
`OutputPath` is `..\bin\Release\` (relative to its own project folder), which resolves
to the **repository root** `bin\Release\`, not a per-project subfolder. Verified
empirically: building `CARE.Common` (whose `OutputPath` is `bin\Release\` with no
`..\`) produced `CARE.DAL\bin\Release\Plexus.DAL.dll`, while no `CARE_MWL_Service\bin`
directory was ever created during a build attempt.

**Fix:** update `integration-test.yml` to check `bin\Release\CARE_MWL_Service.exe` and
`bin\Release\CARE_StoreSCP_Service.exe` (repo root), and update the subsequent steps
that reference `CARE_MWL_Service\bin\Release\cfg` / `CARE_StoreSCP_Service\bin\Release\SCP`
the same way.

## Suggested order of work

1. Add the missing `ProjectReference` to the 3 service projects (item 1) — nothing
   else can be verified until the solution actually compiles end-to-end.
2. Fix `integration-test.yml`'s path assumptions (item 4) so CI can validate the fix
   in (1).
3. Fix the service-name inconsistencies in `uctrl_ServerManager.cs` (item 3) so
   Start/Stop/Uninstall work for all `deployType` values, not just the default.
4. Remove real secrets from committed config and rotate/replace the static encryption
   key (item 2).
5. Harden `OnStart` failure handling so a broken config fails loudly instead of
   reporting "Running" with nothing listening (item 3).
