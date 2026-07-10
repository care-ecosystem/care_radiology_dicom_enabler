# CARE DICOM Enabler — Issues List

> Static code review. Cannot be run on macOS (WinForms / .NET Framework 4.7.2).

---

## Critical

### C-1 · Crash on missing DICOM tags (StudyInstanceUID)
- **File:** `CARE_StoreSCP_Service/Network/CStoreSCP.cs:195`
- **Issue:** `GetSingleValue<string>(DicomTag.StudyInstanceUID).Trim()` throws if the tag is absent. Some modality emulators do not populate all tags.
- **Fix:**
  ```csharp
  var studyUid = request.Dataset.GetSingleValueOrDefault(DicomTag.StudyInstanceUID, Guid.NewGuid().ToString()).Trim();
  ```

---

### C-2 · Crash on missing patient/study DICOM tags
- **File:** `CARE_StoreSCP_Service/Network/CStoreSCP.cs:243–253`
- **Issue:** `GetString()` called on `PatientID`, `AccessionNumber`, `Modality`, `SeriesInstanceUID` etc. with no null guard. Any missing tag throws and the C-STORE fails.
- **Fix:** Replace all `GetString()` calls with `GetSingleValueOrDefault(..., string.Empty)`.

---

### C-3 · NullReferenceException masking real errors
- **File:** `CARE_SCU_Service/Plexus_SCU_Service.cs:133, 160, 185`
- **Issue:** `ex.InnerException.Message` is called without checking if `InnerException` is null. When it is null, this throws a second exception and the original error is lost entirely.
- **Fix:**
  ```csharp
  ex.InnerException?.Message ?? ex.Message
  ```

---

### C-4 · SQL Injection — AE Title / host address validation
- **File:** `CARE.DAL/ucls_DAL.cs:395`
- **Issue:** AE Title and host address are interpolated directly into SQL string. Any modality with a crafted AE Title can manipulate the query.
  ```csharp
  // vulnerable
  $"SELECT count(*) FROM dcm_servers WHERE aetitle='{callingAET}' and hostaddress='{hostAddress}'"
  ```
- **Fix:** Use parameterized queries:
  ```csharp
  string sql = "SELECT count(*) FROM dcm_servers WHERE aetitle=@aetitle AND hostaddress=@hostaddress";
  cmd.Parameters.AddWithValue("@aetitle", callingAET);
  cmd.Parameters.AddWithValue("@hostaddress", hostAddress);
  ```
- **Note:** Same pattern exists in `CARE_MWL_Service/DAL/ucls_DAL.cs` INSERT/UPDATE statements.

---

### C-5 · Copy-paste bug — ExamRoom data written to ExamDescription
- **File:** `CARE_MWL_Service/Model/WorklistItemsProvider.cs`
- **Issue:** `mwlItem.ExamDescription` is assigned the value of `exam_room` instead of `mwlItem.ExamRoom`. ExamRoom is never populated; ExamDescription contains wrong data. Worklist responses sent to modalities will have incorrect field values.
- **Fix:**
  ```csharp
  mwlItem.ExamRoom = dRow["exam_room"].ToString();
  ```

---

### C-6 · Deadlock risk — `.Result` on async Task inside timer
- **File:** `CARE_MWL_Service/Model/WorklistItemsProvider.cs:194, 344`
- **Issue:** `task.Result` blocks the timer thread. Combined with the WinForms synchronization context, this can deadlock and freeze the worklist refresh entirely.
- **Fix:** Make the method `async Task` and propagate `await` up the call chain, or use `task.GetAwaiter().GetResult()` as a minimum stopgap.

---

## Medium

### M-1 · `async void` silently swallows exceptions
- **File:** `CARE_SCU_Service/Plexus_SCU_Service.cs:114`
- **Issue:** `private async void DicomSCUFile(...)` — any unhandled exception inside crashes the service process with no log entry. Files that fail to push are never retried and never reported.
- **Fix:** Change to `private async Task DicomSCUFile(...)` and await all call sites.

---

### M-2 · Race condition on static DAL initialization
- **File:** `CARE_StoreSCP_Service/Network/CStoreSCP.cs:64–67`
- **Issue:** Multiple concurrent DICOM associations can both pass the `if (objDAL == null)` check simultaneously and create duplicate DAL instances, leading to unpredictable database behaviour.
- **Fix:**
  ```csharp
  Interlocked.CompareExchange(ref objDAL, new ucls_DAL(applicationPath), null);
  ```

---

### M-3 · Double server validation per C-STORE request
- **File:** `CARE_StoreSCP_Service/Network/CStoreSCP.cs:97, 200`
- **Issue:** `validateServer()` is called twice. The second call uses `Association.CallingAE` instead of the passed parameters, which can produce a different result from the first call.
- **Fix:** Remove the second call at line 200; the first call at line 97 is sufficient.

---

### M-4 · Crash if SCP folder does not exist
- **File:** `CARE_SCU_Service/Plexus_SCU_Service.cs:72`
- **Issue:** `Directory.GetFiles(dcmPushPath, "*.*", SearchOption.AllDirectories)` throws `DirectoryNotFoundException` on first run if the `SCP` folder has not been created yet.
- **Fix:**
  ```csharp
  if (!Directory.Exists(dcmPushPath)) Directory.CreateDirectory(dcmPushPath);
  ```

---

### M-5 · Two `Start()` overloads with inconsistent timer behaviour
- **File:** `CARE_MWL_Service/WorklistServer.cs:34, 53`
- **Issue:** The overload without a `backend` parameter initialises the timer differently from the overload with one. If the wrong overload is called, the worklist never refreshes from the CARE backend.
- **Fix:** Consolidate into a single `Start(int port, string aet, int backend)` method with a sensible default for `backend`.

---

### M-6 · Config validation failure logged at wrong level
- **File:** `CARE_StoreSCP_Service/Network/CStoreSCP.cs:163`
- **Issue:** When `checkserver` config value is invalid, the failure is logged as `Information`. It will not appear in filtered error views and will be silently ignored.
- **Fix:** Change to `_fileLogger.Error(...)`.

---

### M-7 · `openDBConnection()` failure not propagated
- **File:** `CARE.DAL/ucls_DAL.cs:110–138`
- **Issue:** `insertOrUpdateServer()` returns `true` even when `openDBConnection()` fails, so the caller believes the write succeeded when it did not.
- **Fix:** Check the return value of `openDBConnection()` and return `false` immediately if it fails.

---

## Low

### L-1 · Misleading timer constructor value
- **File:** `CARE_SCU_Service/Plexus_SCU_Service.cs:21`
- **Issue:** `new Timer(TimeSpan.FromHours(24).TotalMilliseconds)` — the 24-hour value passed to the constructor is overridden to 5000 ms in `OnStart`. The constructor argument is meaningless and misleading.
- **Fix:** Initialise with `Timeout.Infinite` and set the interval explicitly in `OnStart`.

---

### L-2 · Name filter uses OR instead of AND
- **File:** `CARE_MWL_Service/Model/WorklistHandler.cs:162`
- **Issue:** `firstNameRegex.IsMatch(x.Forename) || lastNameRegex.IsMatch(x.Surname)` returns results where either name matches. When both a first and last name filter are active, too many results are returned.
- **Fix:** Use `&&` to require both conditions when both filters are non-empty.

---

### L-3 · Mutable static fields read across threads without synchronisation
- **File:** `Global.cs`
- **Issue:** `_storagePath`, `_aeTitle`, and `deployType` are written from the UI thread and read from service threads. No lock or volatile keyword is used.
- **Fix:** Make fields `readonly` after initialisation, or use `volatile` / a lock for fields that must be mutable.

---

### L-4 · Age-to-DOB conversion loses precision
- **File:** `CARE_MWL_Service/Model/WorklistItemsProvider.cs`
- **Issue:** `DateTime.Now.AddYears(item.patient.age.Value * -1)` assumes the patient's birthday is today, which is wrong for every patient. Some modalities use DOB for age bracket filtering.
- **Fix:** Use the actual DOB from the API response if available. If not, document the approximation clearly.

---

### L-5 · XPath strings duplicated across the codebase
- **File:** `frm_Mainform.cs`, multiple UserControls
- **Issue:** XPath strings like `"/configurations/mwlaetitle"` are hardcoded in multiple places. A typo in one location causes a silent config read failure.
- **Fix:** Define as `private const string` in a shared config constants class.

---

### L-6 · Presentation contexts not explicitly rejected
- **File:** `CARE_StoreSCP_Service/Network/CStoreSCP.cs:107–117`
- **Issue:** Presentation contexts that are neither Verification nor Storage are silently ignored rather than explicitly rejected. Some modalities interpret this as a protocol error.
- **Fix:**
  ```csharp
  else
  {
      pc.SetResult(DicomPresentationContextResult.RejectAbstractSyntaxNotSupported);
  }
  ```

---

### L-7 · Typo in error message
- **File:** `CARE_StoreSCP_Service/Network/CStoreSCP.cs:163`
- **Issue:** `"Configuraion Value to check for server in valid"` — two errors: "Configuraion" and "in valid".
- **Fix:** `"Configuration value for checkserver is invalid. Verify the Server List tab."`

---

## Summary

| Severity | Count |
|---|---|
| Critical | 6 |
| Medium | 7 |
| Low | 7 |
| **Total** | **20** |

### Top 6 to fix first

1. **C-1 / C-2** — DICOM tag null guards — most likely to trigger with any real modality
2. **C-3** — `InnerException` null check — hides all SCU errors
3. **C-4** — SQL injection — security risk
4. **M-1** — `async void` — silent service crashes with no log
5. **C-5** — ExamRoom copy-paste — wrong data in every worklist response
6. **M-4** — SCP folder check — guaranteed crash on first run
