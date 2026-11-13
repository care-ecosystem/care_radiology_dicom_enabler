// Copyright (c) 2012-2022 fo-dicom contributors.
// Licensed under the Microsoft Public License (MS-PL).

using System;

namespace Worklist_SCP.Model
{

    /// <summary>
    /// This class contains the most important values that are transmitted per worklist
    /// </summary>
    [Serializable]
    public class WorklistItem
    {

        public string AccessionNumber { get; set; }

        public string PatientID { get; set; }

        public string Surname { get; set; }

        public string Forename { get; set; }

        public string Title { get; set; }

        public string Sex { get; set; }

        public DateTime DateOfBirth { get; set; }

        public string ReferringPhysician { get; set; }

        public string PerformingPhysician { get; set; }

        public string Modality { get; set; }

        public DateTime ExamDateAndTime { get; set; }

        public string ExamRoom { get; set; }

        public string ExamDescription { get; set; }

        public string StudyUID { get; set; }

        public string ProcedureID { get; set; }

        public string ProcedureStepID { get; set; }

        public string HospitalName { get; set; }

        /// <summary>
        /// Scheduled Station AE Title (0040,0001) returned when the C-FIND does not ask for one: the
        /// first of ScheduledAETs, or empty when there are none (a CARE item) - there is no fixed one.
        /// </summary>
        public string ScheduledAET { get; set; }

        /// <summary>
        /// The Scheduled Station AE Titles of the DICOM devices the request is scheduled on in CARE
        /// (each device's ae_title). A C-FIND asking for one of them matches the item and gets it back.
        /// Empty when CARE gave none, or for the List / Database backends, which match ScheduledAET instead.
        /// </summary>
        public System.Collections.Generic.List<string> ScheduledAETs { get; set; } = new System.Collections.Generic.List<string>();

        /// <summary>
        /// </summary>
        public bool HasScheduledDevices { get; set; }

        public string ServiceRequestId { get; set; }

        public string FacilityId { get; set; }

        public string PatientUHID { get; set; }

        public string TechnicianInstruction { get; set; }

        public string PatientInstruction { get; set; }

        public string Priority { get; set; }

        public string ProcedureCode { get; set; }

    }
}
