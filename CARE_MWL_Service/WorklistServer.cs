// Copyright (c) 2012-2022 fo-dicom contributors.
// Licensed under the Microsoft Public License (MS-PL).

using FellowOakDicom;
using FellowOakDicom.Log;
using FellowOakDicom.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using Worklist_SCP.Model;


namespace Worklist_SCP
{
    public class WorklistServer
    {

        //private static IDicomServer _server;
        //private static Timer _itemsLoaderTimer;
        private static List<IDicomServer> _servers = new List<IDicomServer>();
        private static Timer _itemsLoaderTimer;

        protected WorklistServer()
        {
        }

        public static string AETitle { get; set; }


        public static IWorklistItemsSource CreateItemsSourceService => new WorklistItemsProvider();

        public static List<WorklistItem> CurrentWorklistItems { get; set; }

        public static Dictionary<string, List<WorklistItem>> WorklistsByModality { get; set; }
        private static Dictionary<string, string> _emulatorModalities = new Dictionary<string, string>();
        public static void Start(int port, string aet)
        {
            AETitle = aet;
            var server = DicomServerFactory.Create<WorklistService>(port);
            //_server = DicomServerFactory.Create<WorklistService>(port);
            _servers.Add(server);
            // every 30 seconds the worklist source is queried and the current list of items is cached in _currentWorklistItems
            _itemsLoaderTimer = new Timer((state) =>
            {
                var newWorklistItems = CreateItemsSourceService.GetAllCurrentWorklistItems();
                CurrentWorklistItems = newWorklistItems;
            }, null, TimeSpan.Zero, TimeSpan.FromSeconds(30));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="port"></param>
        /// <param name="aet"></param>
        /// <param name="backend"> 0 - List , 1- MySQL</param>

        public static void Start(int port, string aet,int backend, string modality = "CR")
        {
            try
            {
                AETitle = aet;

                new DicomSetupBuilder()
                    .RegisterServices(s => s.AddFellowOakDicom().AddLogManager<ConsoleLogManager>())
                    .Build();
                //_server = DicomServerFactory.Create<WorklistService>(port);
                // CHANGED: Create server and add to list
                var server = DicomServerFactory.Create<WorklistService>(port);
                _servers.Add(server);
                // every 30 seconds the worklist source is queried and the current list of items is cached in _currentWorklistItems
                //_itemsLoaderTimer = new System.Threading.Timer((state) =>
                //{
                //    switch(backend)
                //    {
                //        case 0:

                //            var newWorklistItems = CreateItemsSourceService.GetAllCurrentWorklistItems();
                //            WorklistServer.CurrentWorklistItems = newWorklistItems;
                //            break;
                //        case 1:
                //            var dbWorklistItems = CreateItemsSourceService.GetAllCurrentWorklistItemsFromDB();
                //            WorklistServer.CurrentWorklistItems = dbWorklistItems;
                //            break;
                //        case 2:
                //            var pellucidWorklistItems = CreateItemsSourceService.GetAllCurrentWorklistItemsFromCareAsync();
                //            WorklistServer.CurrentWorklistItems = pellucidWorklistItems;
                //            break;

                //    }

                //}, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));

                // Initialize dictionary once
                if (WorklistsByModality == null)
                {
                    WorklistsByModality = new Dictionary<string, List<WorklistItem>>();
                }

                // Store modality for this emulator
                if (!_emulatorModalities.ContainsKey(aet))
                {
                    _emulatorModalities[aet] = modality;  // Store: MODALITYSCP_CR -> "CR"
                }



                // CHANGED: Only start timer ONCE (not on every Start() call)
                if (_itemsLoaderTimer == null)
                {
                    _itemsLoaderTimer = new System.Threading.Timer((state) =>
                    {
                        // Fetch data for EACH emulator's modality
                        foreach (var emulatorEntry in _emulatorModalities)
                        {
                            string aeTitle = emulatorEntry.Key;
                            string currentModality = emulatorEntry.Value;

                            var worklistItems = CreateItemsSourceService.GetAllCurrentWorklistItemsFromCareAsync(currentModality);
                            // Store only for this modality
                            WorklistsByModality[currentModality] = worklistItems;
                        }

                    }, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
                }
            }
            catch(Exception ex)
            {
                throw new Exception("WorklistServer.Start failed on port " + port + ": " + ex.Message, ex);
            }


        }




        //public static void Stop()
        //{
        //    _itemsLoaderTimer?.Dispose();
        //    _server?.Dispose();
        //}
        public static void Stop()
        {
            _itemsLoaderTimer?.Dispose();

            // CHANGED: Dispose ALL servers
            foreach (var server in _servers)
            {
                server?.Dispose();
            }
            _servers.Clear();
        }

        /// <summary>
        /// Split worklist items by modality and store in dictionary
        /// </summary>
        private static void SplitAndStoreWorklistByModality(List<WorklistItem> allWorklistItems)
        {
            try
            {
                WorklistsByModality.Clear();

                var groupedByModality = allWorklistItems.GroupBy(w => w.Modality ?? "CR");

                foreach (var group in groupedByModality)
                {
                    WorklistsByModality[group.Key] = group.ToList();
                }
            }
            catch (Exception ex)
            {
                // Fallback: store all as CR
                WorklistsByModality["CR"] = allWorklistItems;
            }
        }
    }

}
