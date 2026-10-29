using System.Collections.Generic;
using FellowOakDicom;
using FellowOakDicom.Network;

namespace Plexus_MWL_Service.logs
{
    /// <summary>
    /// Describes a modality device for the logs, so they show which device fetched the worklist, sent
    /// MPPS or sent an image. Shared by the MWL, Store SCP and Store SCU services.
    /// </summary>
    public static class RemoteDeviceInfo
    {
        // Tags a modality puts in its images and MPPS messages that identify the device
        private static readonly DicomTag[] DeviceTags =
        {
            DicomTag.Modality,
            DicomTag.StationName,
            DicomTag.Manufacturer,
            DicomTag.ManufacturerModelName,
            DicomTag.DeviceSerialNumber,
            DicomTag.SoftwareVersions,
            DicomTag.InstitutionName,
            DicomTag.InstitutionalDepartmentName,
            DicomTag.InstitutionAddress,
            DicomTag.PerformedStationAETitle,
            DicomTag.PerformedStationName,
            DicomTag.PerformedLocation,
        };

        /// <summary>
        /// The calling device as negotiated on the association: AE title, IP and port, the AE title it
        /// called, and the DICOM implementation (toolkit / software) it identified itself with.
        /// </summary>
        public static string Describe(DicomAssociation association)
        {
            if (association == null)
                return "unknown device (no association)";
            return $"AE={association.CallingAE}, IP={association.RemoteHost}, Port={association.RemotePort}, CalledAE={association.CalledAE}, " +
                $"ImplementationClassUID={association.RemoteImplementationClassUID?.UID ?? "N/A"}, " +
                $"ImplementationVersion={(string.IsNullOrWhiteSpace(association.RemoteImplementationVersion) ? "N/A" : association.RemoteImplementationVersion)}, " +
                $"MaxPDULength={association.MaximumPDULength}";
        }

        /// <summary>
        /// The device on a connection, for errors: its AE title once the association has been requested,
        /// and its IP and port from the network connection, so a device that fails before or while
        /// negotiating the association is still identified.
        /// </summary>
        public static string DescribeConnection(DicomAssociation association, string remoteHost, int remotePort)
        {
            return $"AE={association?.CallingAE ?? "unknown (association not requested yet)"}, " +
                $"IP={association?.RemoteHost ?? remoteHost ?? "N/A"}, Port={(association != null ? association.RemotePort : remotePort)}";
        }

        /// <summary>
        /// The device details the modality put in an image or MPPS dataset (station, manufacturer, model,
        /// serial number, software, institution...), and for a saved DICOM file, the AE title and DICOM
        /// implementation of the device that sent it, from its file meta header.
        /// </summary>
        public static string Describe(DicomDataset dataset, DicomFileMetaInformation fileMeta = null)
        {
            var parts = new List<string>();
            if (fileMeta != null)
            {
                AddValue(parts, "SourceAE", fileMeta, DicomTag.SourceApplicationEntityTitle);
                AddValue(parts, "ImplementationClassUID", fileMeta, DicomTag.ImplementationClassUID);
                AddValue(parts, "ImplementationVersion", fileMeta, DicomTag.ImplementationVersionName);
            }
            if (dataset != null)
            {
                foreach (DicomTag tag in DeviceTags)
                    AddValue(parts, tag.DictionaryEntry.Keyword, dataset, tag);
            }
            return parts.Count > 0 ? string.Join(", ", parts) : "no device details in the dataset";
        }

        /// <summary>
        /// The remote IP and port of a new connection, before any DICOM message is read. Returns
        /// "N/A" / 0 when the connection is already gone.
        /// </summary>
        public static void GetRemoteEndpoint(INetworkStream stream, out string remoteHost, out int remotePort)
        {
            remoteHost = "N/A";
            remotePort = 0;
            try
            {
                remoteHost = stream?.RemoteHost ?? "N/A";
                remotePort = stream?.RemotePort ?? 0;
            }
            catch
            {
            }
        }

        /// <summary>
        /// The keys a C-FIND query was sent with that have a value, including those inside sequences
        /// (e.g. ScheduledProcedureStepSequence.ScheduledStationAETitle), to show what the device asked for.
        /// </summary>
        public static string DescribeQuery(DicomDataset query)
        {
            var parts = new List<string>();
            if (query != null)
                AddQueryKeys(parts, query, string.Empty);
            return parts.Count > 0 ? string.Join(", ", parts) : "no matching keys (all items requested)";
        }

        private static void AddQueryKeys(List<string> parts, DicomDataset dataset, string prefix)
        {
            foreach (DicomItem item in dataset)
            {
                string keyword = item.Tag.DictionaryEntry.Keyword;
                string name = prefix + (string.IsNullOrEmpty(keyword) ? item.Tag.ToString() : keyword);
                if (item is DicomSequence sequence)
                {
                    foreach (DicomDataset sequenceItem in sequence.Items)
                        AddQueryKeys(parts, sequenceItem, name + ".");
                }
                else
                {
                    AddValue(parts, name, dataset, item.Tag);
                }
            }
        }

        // Adds "name=value" when the tag has a non-empty value. A value that cannot be read as a string
        // is left out rather than failing the DICOM operation being logged.
        private static void AddValue(List<string> parts, string name, DicomDataset dataset, DicomTag tag)
        {
            try
            {
                if (dataset.TryGetString(tag, out string value) && !string.IsNullOrWhiteSpace(value))
                    parts.Add($"{name}={value.Trim()}");
            }
            catch
            {
            }
        }
    }
}
