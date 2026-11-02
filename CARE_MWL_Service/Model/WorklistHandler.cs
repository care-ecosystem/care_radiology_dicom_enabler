// Copyright (c) 2012-2022 fo-dicom contributors.
// Licensed under the Microsoft Public License (MS-PL).

using FellowOakDicom;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Worklist_SCP.Model
{

    public static class WorklistHandler
    {

        public static IEnumerable<DicomDataset> FilterWorklistItems(DicomDataset request, List<WorklistItem> allWorklistItems)
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
            if (request.Contains(DicomTag.ScheduledProcedureStepSequence))
            {
                procedureStep = request.GetSequence(DicomTag.ScheduledProcedureStepSequence).First();

                // Required Matching keys
                var scheduledStationAET = procedureStep.GetSingleValueOrDefault(DicomTag.ScheduledStationAETitle, string.Empty);
                if (!string.IsNullOrEmpty(scheduledStationAET))
                {
                    exams = exams.Where(x => x.ScheduledAET == scheduledStationAET);
                }

                var performingPhysician = procedureStep.GetSingleValueOrDefault(DicomTag.PerformingPhysicianName, string.Empty);
                if (!string.IsNullOrEmpty(performingPhysician))
                {
                    exams = exams.Where(x => x.PerformingPhysician == performingPhysician);
                }

                var modality = procedureStep.GetSingleValueOrDefault(DicomTag.Modality, string.Empty);
                if (!string.IsNullOrEmpty(modality))
                {
                    exams = exams.Where(x => x.Modality == modality);
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

            var results = exams.ToList();

            //  Parsing result
            foreach (var result in results)
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
                var requestedProcedureCode = new DicomDataset();
                if (!string.IsNullOrWhiteSpace(result.ProcedureCode))
                {
                    requestedProcedureCode.AddOrUpdate(DicomTag.CodeValue, result.ProcedureCode);
                    requestedProcedureCode.AddOrUpdate(DicomTag.CodingSchemeDesignator, "99CARE");
                    requestedProcedureCode.AddOrUpdate(DicomTag.CodeMeaning, result.ExamDescription ?? string.Empty);
                }
                AddIfExistsInRequest(resultDataset, request, DicomTag.RequestedProcedureCodeSequence, requestedProcedureCode); // T1C

                // Scheduled Procedure Step sequence T1
                // add results to procedure step dataset
                // Return if requested
                if (procedureStep != null)
                {
                    AddIfExistsInRequest(resultingSPS, procedureStep, DicomTag.ScheduledStationAETitle, result.ScheduledAET); // T1

                    // Taken from the CARE service request date. Return key type 1 needs a value, so an
                    // item without one is reported as scheduled now.
                    var scheduledDateTime = result.ExamDateAndTime != default ? result.ExamDateAndTime : DateTime.Now;
                    AddIfExistsInRequest(resultingSPS, procedureStep, DicomTag.ScheduledProcedureStepStartDate, scheduledDateTime); // (0040,0002) T1
                    AddIfExistsInRequest(resultingSPS, procedureStep, DicomTag.ScheduledProcedureStepStartTime, scheduledDateTime); // (0040,0003) T1
                    AddIfExistsInRequest(resultingSPS, procedureStep, DicomTag.Modality, result.Modality); // T1

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

                // Send Reponse Back
                yield return resultDataset;
            }
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


        internal static void AddIfExistsInRequest<T>(DicomDataset result, DicomDataset request, DicomTag tag, T value)
        {
            // Only send items which have been requested
            if (request.Contains(tag))
            {
                if (value == null)
                {
                    value = default;
                }

                result.AddOrUpdate(tag, value);
            }
        }


    }
}
