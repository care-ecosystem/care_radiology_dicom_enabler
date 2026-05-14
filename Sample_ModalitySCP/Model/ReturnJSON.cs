using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Sample_ModalitySCP.Model
{

    public class FullName
    {
        [JsonProperty("first_name")]
        public string FirstName { get; set; }

        [JsonProperty("middle_name")]
        public string MiddleName { get; set; }

        [JsonProperty("last_name")]
        public string LastName { get; set; }
    }

    public class Age
    {
        [JsonProperty("year")]
        public string Year { get; set; }

        [JsonProperty("month")]
        public string Month { get; set; }

        [JsonProperty("day")]
        public string Day { get; set; }
    }

    public class Mobile
    {
        [JsonProperty("code")]
        public string Code { get; set; }

        [JsonProperty("no")]
        public string Number { get; set; }
    }

    public class Phone
    {
        [JsonProperty("code")]
        public string Code { get; set; }

        [JsonProperty("no")]
        public string Number { get; set; }
    }

    public class PrimaryContact
    {
        [JsonProperty("mobile")]
        public Mobile Mobile { get; set; }

        [JsonProperty("phone")]
        public Phone Phone { get; set; }
    }

    public class Patient
    {
        [JsonProperty("patient_mrn")]
        public string PatientMrn { get; set; }

        [JsonProperty("full_name")]
        public FullName FullName { get; set; }

        [JsonProperty("age")]
        public Age Age { get; set; }

        [JsonProperty("gender")]
        public string Gender { get; set; }

        [JsonProperty("email")]
        public string Email { get; set; }

        [JsonProperty("primary_contact")]
        public PrimaryContact PrimaryContact { get; set; }
    }

    public class PurposeOfVisit
    {
        [JsonProperty("key")]
        public string Key { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }
    }

    public class Evaluation
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("start")]
        public string Start { get; set; }

        [JsonProperty("end")]
        public string End { get; set; }
    }

    public class General
    {
        [JsonProperty("in")]
        public string In { get; set; }

        [JsonProperty("out")]
        public string Out { get; set; }
    }

    public class Durations
    {
        [JsonProperty("general")]
        public General General { get; set; }

        [JsonProperty("evaluation")]
        public List<Evaluation> Evaluation { get; set; }
    }

    public class PopoverInfo
    {
        [JsonProperty("room")]
        public string Room { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }
    }

    public class EvaluationStatus
    {
        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("color")]
        public string Color { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("code")]
        public int Code { get; set; }

        [JsonProperty("popoverinfo")]
        public List<PopoverInfo> PopoverInfo { get; set; }

        [JsonProperty("starttime")]
        public string StartTime { get; set; }
    }

    public class Consultation
    {
        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("color")]
        public string Color { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("code")]
        public int Code { get; set; }

        [JsonProperty("popoverinfo")]
        public List<PopoverInfo> PopoverInfo { get; set; }
    }

    public class OverallStatusList
    {
        [JsonProperty("evaluation")]
        public EvaluationStatus Evaluation { get; set; }

        [JsonProperty("consultation")]
        public Consultation Consultation { get; set; }
    }

    public class PaymentDetails
    {
        [JsonProperty("total_price")]
        public int TotalPrice { get; set; }

        [JsonProperty("balance_total_price")]
        public int BalanceTotalPrice { get; set; }

        [JsonProperty("total_tax")]
        public int TotalTax { get; set; }

        [JsonProperty("total_discount_price")]
        public int TotalDiscountPrice { get; set; }

        [JsonProperty("credited_total_price")]
        public int CreditedTotalPrice { get; set; }

        [JsonProperty("debited_total_price")]
        public int DebitedTotalPrice { get; set; }

        [JsonProperty("basket_status")]
        public string BasketStatus { get; set; }
    }

    public class CasesheetCollection
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("key")]
        public string Key { get; set; }

        [JsonProperty("checked")]
        public bool Checked { get; set; }
    }

    public class Notification
    {
        [JsonProperty("enable")]
        public bool Enable { get; set; }
    }

    public class Status
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("color")]
        public string Color { get; set; }
    }

    public class Appointment
    {
        [JsonProperty("_id")]
        public string Id { get; set; }

        [JsonProperty("patient")]
        public Patient Patient { get; set; }

        [JsonProperty("status")]
        public List<string> Status { get; set; }

        [JsonProperty("doctors")]
        public List<string> Doctors { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("appointment_date")]
        public string AppointmentDate { get; set; }

        [JsonProperty("time_slot")]
        public string TimeSlot { get; set; }

        [JsonProperty("purpose_of_visit")]
        public List<PurposeOfVisit> PurposeOfVisit { get; set; }

        [JsonProperty("reference_sources")]
        public List<string> ReferenceSources { get; set; }

        [JsonProperty("planned_procedures")]
        public List<string> PlannedProcedures { get; set; }

        [JsonProperty("notes")]
        public string Notes { get; set; }

        [JsonProperty("visits")]
        public int Visits { get; set; }

        [JsonProperty("email_notification")]
        public Notification EmailNotification { get; set; }

        [JsonProperty("sms_notification")]
        public Notification SmsNotification { get; set; }

        [JsonProperty("client_id")]
        public string ClientId { get; set; }

        [JsonProperty("client_name")]
        public string ClientName { get; set; }

        [JsonProperty("durations")]
        public Durations Durations { get; set; }

        [JsonProperty("overall_status_list")]
        public OverallStatusList OverallStatusList { get; set; }

        [JsonProperty("nextAssignmentdepartments")]
        public string NextAssignmentDepartments { get; set; }

        [JsonProperty("assignAllDepartments")]
        public List<string> AssignAllDepartments { get; set; }

        [JsonProperty("overall_status")]
        public string OverallStatus { get; set; }

        [JsonProperty("status_list")]
        public List<Status> StatusList { get; set; }

        [JsonProperty("fee_type")]
        public string FeeType { get; set; }

        [JsonProperty("payment_details")]
        public PaymentDetails PaymentDetails { get; set; }

        [JsonProperty("casesheet_collection")]
        public List<CasesheetCollection> CasesheetCollection { get; set; }

        [JsonProperty("current_departname")]
        public string CurrentDepartName { get; set; }

        [JsonProperty("checked")]
        public bool Checked { get; set; }

        [JsonProperty("created")]
        public string Created { get; set; }

        [JsonProperty("updated")]
        public string Updated { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; }
    }

}
