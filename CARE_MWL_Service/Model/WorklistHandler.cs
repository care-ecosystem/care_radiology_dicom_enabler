// Copyright (c) 2012-2022 fo-dicom contributors.
// Licensed under the Microsoft Public License (MS-PL).

using FellowOakDicom;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Serilog.Events;

namespace Worklist_SCP.Model
{

    public static class WorklistHandler
    {

        /// <param name="log">
        /// Told about everything that is not sent to the device: items that do not match the query, an
        /// item left out because a CARE value is not valid DICOM (the other items are still returned),
        /// values sent empty or replaced, and requested keys this server does not return. Optional.
        /// </param>
        /// <param name="deviceRule">
        /// </param>
        /// <param name="includeUnmapped">
        /// </param>
        public static IEnumerable<DicomDataset> FilterWorklistItems(DicomDataset request, List<WorklistItem> allWorklistItems, Action<LogEventLevel, string> log = null, bool deviceRule = false, bool includeUnmapped = false)
        {
            var exams = allWorklistItems.AsQueryable();

            if (request.TryGetSingleValue(DicomTag.PatientID, out string patientId))
            {
                exams = exams.Where(x => x.PatientID.Equals(patientId));
            }

            var patientName = request.GetSingleValueOrDefault(DicomTag.PatientName, string.Empty);
            if (!string.IsNullOrEmpty(patientName))
            {
                exams = AddNameCondition(exams, patientName);
            }

            DicomDataset procedureStep = null;
            string requestedScheduledAET = null;
            if (request.Contains(DicomTag.ScheduledProcedureStepSequence))
            {
                procedureStep = request.GetSequence(DicomTag.ScheduledProcedureStepSequence).First();

                // Required Matching keys
                // Scheduled Station AE Title: an item scheduled on several DICOM devices matches the AE
                // title of any of them
                var scheduledStationAET = procedureStep.GetSingleValueOrDefault(DicomTag.ScheduledStationAETitle, string.Empty).Trim();
                if (!string.IsNullOrEmpty(scheduledStationAET))
                {
                    requestedScheduledAET = scheduledStationAET;
                    if (!deviceRule)
                        exams = exams.Where(x => x.ScheduledAETs != null && x.ScheduledAETs.Count > 0
                            ? x.ScheduledAETs.Contains(scheduledStationAET)
                            : x.ScheduledAET == scheduledStationAET);
                }

                var performingPhysician = procedureStep.GetSingleValueOrDefault(DicomTag.PerformingPhysicianName, string.Empty);
                if (!string.IsNullOrEmpty(performingPhysician))
                {
                    exams = exams.Where(x => x.PerformingPhysician == performingPhysician);
                }

                var modality = procedureStep.GetSingleValueOrDefault(DicomTag.Modality, string.Empty);
                if (!string.IsNullOrEmpty(modality))
                {
                    exams = exams.Where(x => string.Equals(x.Modality, modality.Trim(), StringComparison.OrdinalIgnoreCase));
                }

                // if only date is specified, then using standard matching
                // but if both are specified, then MWL defines a combined match
                var scheduledProcedureStepStartDateTime = procedureStep.GetSingleValueOrDefault(DicomTag.ScheduledProcedureStepStartDateTime, string.Empty);
                if (!string.IsNullOrEmpty(scheduledProcedureStepStartDateTime))
                {
                    exams = AddDateCondition(exams, scheduledProcedureStepStartDateTime);
                }

                // Scheduled Procedure Step Start Date (0040,0002) and Time (0040,0003) are matched against
                // the CARE service request date. When both are given they form one combined date-time range.
                procedureStep.TryGetString(DicomTag.ScheduledProcedureStepStartDate, out string scheduledStartDate);
                procedureStep.TryGetString(DicomTag.ScheduledProcedureStepStartTime, out string scheduledStartTime);
                exams = AddScheduledStartCondition(exams, scheduledStartDate, scheduledStartTime);

                // Optional (but commonly used) matching keys.
                var procedureStepLocation = procedureStep.GetSingleValueOrDefault(DicomTag.ScheduledProcedureStepLocation, string.Empty);
                if (!string.IsNullOrEmpty(procedureStepLocation))
                {
                    exams = exams.Where(x => x.ExamRoom.Equals(procedureStepLocation));
                }

                var procedureDescription = procedureStep.GetSingleValueOrDefault(DicomTag.ScheduledProcedureStepDescription, string.Empty);
                if (!string.IsNullOrEmpty(procedureDescription))
                {
                    exams = exams.Where(x => x.ExamDescription.Equals(procedureDescription));
                }
            }

            if (deviceRule)
            {
                string requestingAET = requestedScheduledAET;
                bool namedAET = !string.IsNullOrEmpty(requestingAET) && !requestingAET.Contains("*") && !requestingAET.Contains("?");
                if (namedAET)
                    exams = exams.Where(x => x.HasScheduledDevices
                        ? x.ScheduledAETs != null && x.ScheduledAETs.Contains(requestingAET, StringComparer.Ordinal)
                        : includeUnmapped);
                else
                    exams = exams.Where(x => false);

                var noAeTitle = (allWorklistItems ?? new List<WorklistItem>())
                    .Where(x => x.HasScheduledDevices && (x.ScheduledAETs == null || x.ScheduledAETs.Count == 0)).ToList();
                if (log != null && noAeTitle.Count > 0)
                    log(LogEventLevel.Warning, $"{noAeTitle.Count} scheduled item(s) sent to no device: they are mapped to DICOM devices in CARE " +
                        "that have no valid AE title (blank or longer than 16 characters). Set the devices' AE titles in CARE. " +
                        string.Join("; ", noAeTitle.Select(Describe)));
            }

            var results = exams.ToList();

            // Scheduled items the query keys filtered out
            if (log != null && allWorklistItems != null && results.Count < allWorklistItems.Count)
            {
                var notMatching = allWorklistItems.Except(results).ToList();
                log(LogEventLevel.Information, $"{notMatching.Count} of {allWorklistItems.Count} scheduled item(s) not sent: they do not match the query keys. " +
                    string.Join("; ", notMatching.Select(DescribeMatchFields)));
            }

            bool unsupportedKeysLogged = false;
            foreach (var result in results)
            {
                // fo-dicom rejects a value its VR does not allow (e.g. too long); that item is skipped
                // instead of failing the whole C-FIND
                DicomDataset resultDataset;
                try
                {
                    resultDataset = BuildResultDataset(request, procedureStep, requestedScheduledAET, result, log);
                }
                catch (Exception ex)
                {
                    log?.Invoke(LogEventLevel.Error, $"Worklist item NOT SENT: {ex.Message}. Item: {Describe(result)}");
                    continue;
                }

                // Once per C-FIND: keys the device asked for that this server never returns
                if (log != null && !unsupportedKeysLogged)
                {
                    unsupportedKeysLogged = true;
                    LogUnsupportedKeys(request, procedureStep, resultDataset, log);
                }

                // Send Reponse Back
                yield return resultDataset;
            }
        }


        /// <summary>
        /// Everything known about a worklist item, for the log (patient name and birth date are left out:
        /// the accession number and service request identify the order in CARE).
        /// </summary>
        private static string Describe(WorklistItem item)
        {
            return $"AccessionNumber={item.AccessionNumber}, ServiceRequestId={item.ServiceRequestId}, PatientID={item.PatientID}, " +
                   $"Modality={item.Modality}, ScheduledStationAETitles=[{string.Join("|", item.ScheduledAETs ?? new List<string>())}], " +
                   $"ScheduledDateTime={(item.ExamDateAndTime != default ? item.ExamDateAndTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) : "(none)")}, " +
                   $"ProcedureDescription={item.ExamDescription}, ProcedureCode={item.ProcedureCode}, RequestedProcedureID={item.ProcedureID}, " +
                   $"ScheduledProcedureStepID={item.ProcedureStepID}, StudyInstanceUID={item.StudyUID}, Priority={item.Priority}, " +
                   $"ReferringPhysician={item.ReferringPhysician}, Institution={item.HospitalName}";
        }


        /// <summary>The fields the query keys are matched on, for an item that did not match.</summary>
        private static string DescribeMatchFields(WorklistItem item)
        {
            return $"{item.AccessionNumber} (ServiceRequestId={item.ServiceRequestId}, Modality={item.Modality}, " +
                   $"ScheduledStationAETitles=[{string.Join("|", item.ScheduledAETs ?? new List<string>())}], " +
                   $"ScheduledDateTime={(item.ExamDateAndTime != default ? item.ExamDateAndTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) : "(none)")})";
        }


        /// <summary>Logs the keys in the query that are missing from the response: this server does not return them.</summary>
        private static void LogUnsupportedKeys(DicomDataset request, DicomDataset procedureStep, DicomDataset response, Action<LogEventLevel, string> log)
        {
            var missing = request.Where(e => !response.Contains(e.Tag)).Select(e => e.Tag.ToString() + " " + e.Tag.DictionaryEntry.Name).ToList();
            if (procedureStep != null && response.TryGetSequence(DicomTag.ScheduledProcedureStepSequence, out DicomSequence responseSps) && responseSps.Items.Count > 0)
            {
                var sps = responseSps.Items[0];
                missing.AddRange(procedureStep.Where(e => !sps.Contains(e.Tag))
                    .Select(e => "ScheduledProcedureStepSequence." + e.Tag.ToString() + " " + e.Tag.DictionaryEntry.Name));
            }
            if (missing.Count > 0)
                log(LogEventLevel.Information, $"Query keys not returned (not supported by this server, so the device gets no value for them): {string.Join(", ", missing)}");
        }


        /// <summary>The C-FIND response dataset for one worklist item: the keys the query asked for.</summary>
        private static DicomDataset BuildResultDataset(DicomDataset request, DicomDataset procedureStep, string requestedScheduledAET, WorklistItem result, Action<LogEventLevel, string> log)
        {
            var resultingSPS = new DicomDataset();
            var resultDataset = new DicomDataset();
            var resultingSPSSequence = new DicomSequence(DicomTag.ScheduledProcedureStepSequence, resultingSPS);

            if (procedureStep != null)
            {
                resultDataset.Add(resultingSPSSequence);
            }

            // add results to "main" dataset
            AddIfExistsInRequest(resultDataset, request, DicomTag.AccessionNumber, result.AccessionNumber);    // T2
            AddIfExistsInRequest(resultDataset, request, DicomTag.InstitutionName, result.HospitalName);
            AddIfExistsInRequest(resultDataset, request, DicomTag.ReferringPhysicianName, result.ReferringPhysician); // T2

            AddIfExistsInRequest(resultDataset, request, DicomTag.PatientName, result.Surname + "^" + result.Forename + "^^" + result.Title); //T1
            AddIfExistsInRequest(resultDataset, request, DicomTag.PatientID, result.PatientID); // T1
            AddIfExistsInRequest(resultDataset, request, DicomTag.PatientBirthDate, result.DateOfBirth); // T2
            AddIfExistsInRequest(resultDataset, request, DicomTag.PatientSex, result.Sex); //T2

            // Modalities put this on every image of the study, so it comes from the worklist item
            // (stable per order) rather than DicomUID.Generate(), which would change on every C-FIND.
            AddIfExistsInRequest(resultDataset, request, DicomTag.StudyInstanceUID, result.StudyUID); // T1

            AddIfExistsInRequest(resultDataset, request, DicomTag.RequestingPhysician, result.ReferringPhysician); //T2
            AddIfExistsInRequest(resultDataset, request, DicomTag.RequestedProcedureDescription, result.ExamDescription); //T1C

            AddIfExistsInRequest(resultDataset, request, DicomTag.RequestedProcedureID, result.ProcedureID); // T1
            AddIfExistsInRequest(resultDataset, request, DicomTag.RequestedProcedureComments, result.PatientInstruction); // T3

            // procedure_id is CARE's unique code identifying the type of exam ordered (e.g. "xray spine" = 1),
            // distinct from RequestedProcedureID above (which identifies this specific order/request).
            // Built only when the query asks for it, so a code the device does not want cannot fail its item
            var requestedProcedureCode = new DicomDataset();
            if (request.Contains(DicomTag.RequestedProcedureCodeSequence) && !string.IsNullOrWhiteSpace(result.ProcedureCode))
            {
                // Code Value (0008,0100) is SH, at most 16 characters; a longer code (e.g. an 18-digit
                // SNOMED CT id) goes in Long Code Value (0008,0119) instead, never both
                string procedureCode = result.ProcedureCode.Trim();
                if (procedureCode.Length > 16)
                    log?.Invoke(LogEventLevel.Warning, $"Procedure code {procedureCode} ({procedureCode.Length} characters) not sent in Code Value (0008,0100), which allows 16: " +
                        $"sent in Long Code Value (0008,0119) - a device that reads only Code Value gets no procedure code. AccessionNumber={result.AccessionNumber}, ServiceRequestId={result.ServiceRequestId}");
                AddValue(requestedProcedureCode, procedureCode.Length <= 16 ? DicomTag.CodeValue : DicomTag.LongCodeValue, procedureCode);
                AddValue(requestedProcedureCode, DicomTag.CodingSchemeDesignator, "99CARE");
                AddValue(requestedProcedureCode, DicomTag.CodeMeaning, result.ExamDescription ?? string.Empty);
            }
            AddIfExistsInRequest(resultDataset, request, DicomTag.RequestedProcedureCodeSequence, requestedProcedureCode); // T1C

            // Scheduled Procedure Step sequence T1
            // add results to procedure step dataset
            // Return if requested
            if (procedureStep != null)
            {
                // The AE title the C-FIND asked for (or the CARE backend's requesting device) when it is one of
                // the item's, else its first. An item mapped to no device and with no AE title is scheduled for
                // whichever device asks, so it gets the requesting AE title instead of an empty one.
                string matchingAET = requestedScheduledAET == null || result.ScheduledAETs == null ? null
                    : result.ScheduledAETs.FirstOrDefault(aet => string.Equals(aet, requestedScheduledAET, StringComparison.Ordinal));
                string scheduledAET = matchingAET
                    ?? (!result.HasScheduledDevices && string.IsNullOrWhiteSpace(result.ScheduledAET) && requestedScheduledAET != null
                        ? requestedScheduledAET
                        : result.ScheduledAET);
                if (procedureStep.Contains(DicomTag.ScheduledStationAETitle) && string.IsNullOrWhiteSpace(scheduledAET))
                    log?.Invoke(LogEventLevel.Warning, $"Scheduled Station AE Title (0040,0001) sent empty: the item is scheduled on no DICOM device with an AE title in CARE. " +
                        $"AccessionNumber={result.AccessionNumber}, ServiceRequestId={result.ServiceRequestId}");
                AddIfExistsInRequest(resultingSPS, procedureStep, DicomTag.ScheduledStationAETitle, scheduledAET); // T1

                // Taken from the CARE service request date. Return key type 1 needs a value, so an
                // item without one is reported as scheduled now.
                var scheduledDateTime = result.ExamDateAndTime != default ? result.ExamDateAndTime : DateTime.Now;
                if (result.ExamDateAndTime == default && (procedureStep.Contains(DicomTag.ScheduledProcedureStepStartDate) || procedureStep.Contains(DicomTag.ScheduledProcedureStepStartTime)))
                    log?.Invoke(LogEventLevel.Warning, $"Scheduled Procedure Step Start Date/Time (0040,0002/0003) not in CARE (no service request date): sent as now, {scheduledDateTime:yyyy-MM-dd HH:mm:ss}. " +
                        $"AccessionNumber={result.AccessionNumber}, ServiceRequestId={result.ServiceRequestId}");
                AddIfExistsInRequest(resultingSPS, procedureStep, DicomTag.ScheduledProcedureStepStartDate, scheduledDateTime); // (0040,0002) T1
                AddIfExistsInRequest(resultingSPS, procedureStep, DicomTag.ScheduledProcedureStepStartTime, scheduledDateTime); // (0040,0003) T1
                // A modality that is not a valid CS value (over 16 characters, or characters such as
                // "-") would throw and fail the whole C-FIND, so it is sent empty instead.
                bool validModality = IsValidCodeString(result.Modality);
                if (!validModality && procedureStep.Contains(DicomTag.Modality))
                    log?.Invoke(LogEventLevel.Warning, $"Modality (0008,0060) \"{result.Modality}\" not sent (sent empty): a DICOM modality allows at most 16 uppercase letters, digits, spaces and underscores. " +
                        $"AccessionNumber={result.AccessionNumber}, ServiceRequestId={result.ServiceRequestId}");
                AddIfExistsInRequest(resultingSPS, procedureStep, DicomTag.Modality, validModality ? result.Modality : string.Empty); // T1

                AddIfExistsInRequest(resultingSPS, procedureStep, DicomTag.ScheduledPerformingPhysicianName, result.PerformingPhysician); //T2
                AddIfExistsInRequest(resultingSPS, procedureStep, DicomTag.ScheduledProcedureStepDescription, result.ExamDescription); // T1C
                AddIfExistsInRequest(resultingSPS, procedureStep, DicomTag.ScheduledProcedureStepID, result.ProcedureStepID); // T1
                AddIfExistsInRequest(resultingSPS, procedureStep, DicomTag.ScheduledStationName, result.ExamRoom); //T2
                AddIfExistsInRequest(resultingSPS, procedureStep, DicomTag.ScheduledProcedureStepLocation, result.ExamRoom); //T2
                AddIfExistsInRequest(resultingSPS, procedureStep, DicomTag.CommentsOnTheScheduledProcedureStep, result.TechnicianInstruction); //T3
            }

            // Put blanks in for unsupported fields which are type 2 (i.e. must have a value even if NULL)
            // In a real server, you may wish to support some or all of these, but they are not commonly supported
            AddIfExistsInRequest(resultDataset, request, DicomTag.ReferencedStudySequence, new DicomDataset());         // Ref//d Study Sequence
            AddIfExistsInRequest(resultDataset, request, DicomTag.Priority, result.Priority);                     // Priority
            AddIfExistsInRequest(resultDataset, request, DicomTag.PatientTransportArrangements, "");              // Transport Arrangements
            AddIfExistsInRequest(resultDataset, request, DicomTag.AdmissionID, "");                               // Admission ID
            AddIfExistsInRequest(resultDataset, request, DicomTag.CurrentPatientLocation, "");                    // Patient Location
            AddIfExistsInRequest(resultDataset, request, DicomTag.ReferencedPatientSequence, new DicomDataset());       // Ref//d Patient Sequence
            AddIfExistsInRequest(resultDataset, request, DicomTag.PatientWeight, "");                             // Weight
            AddIfExistsInRequest(resultDataset, request, DicomTag.ConfidentialityConstraintOnPatientDataDescription, ""); // Confidentiality Constraint

            return resultDataset;
        }


        //Splits patient name into 2 separte strings surname and forename and send then to the addstringcondition subroutine.
        internal static IQueryable<WorklistItem> AddNameCondition(IQueryable<WorklistItem> exams, string dicomName)
        {
            if (string.IsNullOrEmpty(dicomName) || dicomName == "*")
            {
                return exams;
            }

            var personName = new DicomPersonName(DicomTag.PatientName, dicomName);
            if (dicomName.Contains("*"))
            {
                var firstNameRegex = new Regex("^" + Regex.Escape(personName.First).Replace("\\*", ".*") + "$");
                var lastNameRegex = new Regex("^" + Regex.Escape(personName.Last).Replace("\\*", ".*") + "$");
                exams = exams.Where(x => firstNameRegex.IsMatch(x.Forename) || lastNameRegex.IsMatch(x.Surname));
            }
            else
            {
                exams = exams.Where(x => (x.Forename.Equals(personName.First) && x.Surname.Equals(personName.Last)));
            }

            return exams;
        }


        internal static IQueryable<WorklistItem> AddDateCondition(IQueryable<WorklistItem> exams, string dateCondition)
        {
            if (!string.IsNullOrEmpty(dateCondition) && dateCondition != "*")
            {
                var range = new DicomDateTime(DicomTag.ScheduledProcedureStepStartDate, dateCondition).Get<DicomDateRange>();
                exams = exams.Where(x => range.Contains(x.ExamDateAndTime));
            }
            return exams;
        }


        // Applies DICOM range matching for the scheduled start date and time query keys:
        // a single value matches its whole day (or hour/minute/second for a partial time),
        // "A-B", "A-" and "-B" are inclusive ranges, and an empty value or "*" matches everything.
        internal static IQueryable<WorklistItem> AddScheduledStartCondition(IQueryable<WorklistItem> exams, string dateCondition, string timeCondition)
        {
            var hasDate = TryParseDateRange(dateCondition, out DateTime? firstDate, out DateTime? lastDate);
            var hasTime = TryParseTimeRange(timeCondition, out TimeSpan? fromTime, out TimeSpan? toTimeExclusive);

            if (hasDate)
            {
                // Combined match: the time range bounds the first and last day of the date range.
                DateTime? startAt = firstDate?.Add(hasTime ? fromTime ?? TimeSpan.Zero : TimeSpan.Zero);
                DateTime? endBefore = lastDate?.Add(hasTime ? toTimeExclusive ?? TimeSpan.FromDays(1) : TimeSpan.FromDays(1));

                exams = exams.Where(x => (!startAt.HasValue || x.ExamDateAndTime >= startAt.Value)
                                      && (!endBefore.HasValue || x.ExamDateAndTime < endBefore.Value));
            }
            else if (hasTime)
            {
                exams = exams.Where(x => (!fromTime.HasValue || x.ExamDateAndTime.TimeOfDay >= fromTime.Value)
                                      && (!toTimeExclusive.HasValue || x.ExamDateAndTime.TimeOfDay < toTimeExclusive.Value));
            }

            return exams;
        }


        // Returns false (no condition) for an empty, universal ("*") or unreadable value.
        private static bool TryParseDateRange(string value, out DateTime? first, out DateTime? last)
        {
            first = null;
            last = null;
            if (string.IsNullOrWhiteSpace(value) || value.Trim() == "*")
            {
                return false;
            }

            var parts = value.Trim().Split('-');
            if (parts.Length > 2)
            {
                return false;
            }

            if (!TryParseDicomDate(parts[0], out first))
            {
                return false;
            }

            if (parts.Length == 1)
            {
                last = first;
            }
            else if (!TryParseDicomDate(parts[1], out last))
            {
                return false;
            }

            return first.HasValue || last.HasValue;
        }


        // An empty part is an open end of a range and parses to null.
        private static bool TryParseDicomDate(string value, out DateTime? date)
        {
            date = null;
            value = value.Trim().Replace(".", string.Empty); // older devices send YYYY.MM.DD
            if (value.Length == 0)
            {
                return true;
            }

            if (DateTime.TryParseExact(value, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed))
            {
                date = parsed;
                return true;
            }
            return false;
        }


        // Returns false (no condition) for an empty, universal ("*") or unreadable value.
        // The upper bound is exclusive: "10" covers 10:00:00 up to (not including) 11:00:00.
        private static bool TryParseTimeRange(string value, out TimeSpan? fromTime, out TimeSpan? toTimeExclusive)
        {
            fromTime = null;
            toTimeExclusive = null;
            if (string.IsNullOrWhiteSpace(value) || value.Trim() == "*")
            {
                return false;
            }

            var parts = value.Trim().Split('-');
            if (parts.Length > 2)
            {
                return false;
            }

            if (!TryParseDicomTime(parts[0], out fromTime, out TimeSpan fromPrecision))
            {
                return false;
            }

            if (parts.Length == 1)
            {
                toTimeExclusive = fromTime + fromPrecision;
            }
            else
            {
                if (!TryParseDicomTime(parts[1], out TimeSpan? toTime, out TimeSpan toPrecision))
                {
                    return false;
                }
                toTimeExclusive = toTime + toPrecision;
            }

            return fromTime.HasValue || toTimeExclusive.HasValue;
        }


        // Reads HH, HHMM, HHMMSS or HHMMSS.FFFFFF; precision is the span the given digits cover.
        // An empty part is an open end of a range and parses to null.
        private static bool TryParseDicomTime(string value, out TimeSpan? time, out TimeSpan precision)
        {
            time = null;
            precision = TimeSpan.Zero;
            value = value.Trim().Replace(":", string.Empty); // older devices send HH:MM:SS
            if (value.Length == 0)
            {
                return true;
            }

            var parts = value.Split('.');
            var hms = parts[0];
            if ((hms.Length != 2 && hms.Length != 4 && hms.Length != 6) || !hms.All(char.IsDigit) || parts.Length > 2)
            {
                return false;
            }

            int hours = int.Parse(hms.Substring(0, 2));
            int minutes = hms.Length >= 4 ? int.Parse(hms.Substring(2, 2)) : 0;
            int seconds = hms.Length == 6 ? int.Parse(hms.Substring(4, 2)) : 0;
            if (hours > 23 || minutes > 59 || seconds > 59)
            {
                return false;
            }

            var parsed = new TimeSpan(hours, minutes, seconds);
            precision = hms.Length == 2 ? TimeSpan.FromHours(1) : hms.Length == 4 ? TimeSpan.FromMinutes(1) : TimeSpan.FromSeconds(1);

            if (parts.Length == 2)
            {
                var fraction = parts[1];
                if (hms.Length != 6 || fraction.Length == 0 || fraction.Length > 6 || !fraction.All(char.IsDigit))
                {
                    return false;
                }
                // 7 fractional digits = ticks (100 ns)
                parsed += TimeSpan.FromTicks(long.Parse(fraction.PadRight(7, '0')));
                precision = TimeSpan.FromTicks((long)Math.Pow(10, 7 - fraction.Length));
            }

            time = parsed;
            return true;
        }


        // CS VR: at most 16 characters, only uppercase letters, digits, space and underscore.
        private static bool IsValidCodeString(string value)
        {
            return value != null && value.Length <= 16
                && value.All(c => (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == ' ' || c == '_');
        }


        internal static void AddIfExistsInRequest<T>(DicomDataset result, DicomDataset request, DicomTag tag, T value)
        {
            // Only send items which have been requested
            if (request.Contains(tag))
            {
                if (value == null)
                {
                    value = default;
                }

                AddValue(result, tag, value);
            }
        }


        /// <summary>Adds a value; when DICOM does not allow it, the error names the tag and the value.</summary>
        private static void AddValue<T>(DicomDataset dataset, DicomTag tag, T value)
        {
            try
            {
                dataset.AddOrUpdate(tag, value);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"{tag.DictionaryEntry.Name} {tag} value \"{value}\" is not valid DICOM ({ex.Message})", ex);
            }
        }


    }
}
