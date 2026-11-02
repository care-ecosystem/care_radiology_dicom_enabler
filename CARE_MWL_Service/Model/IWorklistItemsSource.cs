// Copyright (c) 2012-2022 fo-dicom contributors.
// Licensed under the Microsoft Public License (MS-PL).

using System.Collections.Generic;
using System.Threading.Tasks;

namespace Worklist_SCP.Model
{
    public interface IWorklistItemsSource
    {

        /// <summary>
        /// this method queries some source like database or webservice to get a list of all scheduled worklist items.
        /// This method is called periodically.
        /// </summary>
        List<WorklistItem> GetAllCurrentWorklistItems();
        List<WorklistItem> GetAllCurrentWorklistItemsFromDB();

        List<WorklistItem> GetAllCurrentWorklistItemsFromPellucidAsync();

        /// <summary>
        /// Reads the scheduled worklist for a single facility from care_worklist. Does not call the
        /// CARE API. A modality given (the one the device queried with) replaces the Configuration
        /// tab modality.
        /// </summary>
        List<WorklistItem> GetCareWorklistItemsFromDB(string facilityId, string modality = null);

        /// <summary>
        /// Calls the CARE worklist API for a single facility and saves the response to care_worklist.
        /// The Facility ID is mandatory - with none given nothing is fetched and false is returned.
        /// A modality given (the one the device queried with) replaces the Configuration tab modality.
        /// </summary>
        bool RefreshCareWorklistFromApi(string facilityId, string modality = null);

    }
}
