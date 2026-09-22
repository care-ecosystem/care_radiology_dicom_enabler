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
        /// Fetches the worklist from the CARE server for a single facility. The Facility ID is
        /// mandatory - with none resolved the implementation returns an empty list rather than
        /// querying every facility.
        /// </summary>
        List<WorklistItem> GetAllCurrentWorklistItemsFromCareAsync(string facilityId);

    }
}
