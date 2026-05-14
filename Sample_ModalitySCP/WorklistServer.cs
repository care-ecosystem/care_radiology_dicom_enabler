// Copyright (c) 2012-2022 fo-dicom contributors.
// Licensed under the Microsoft Public License (MS-PL).

using FellowOakDicom.Network;
using System;
using System.Collections.Generic;
using System.Threading;

using Serilog.Sinks.File;
using Worklist_SCP.Model;
using Serilog;
using System.Windows.Forms;

namespace Worklist_SCP
{
    public class WorklistServer
    {

        private static IDicomServer _server;
        private static System.Threading.Timer _itemsLoaderTimer;


        protected WorklistServer()
        {
        }

        public static string AETitle { get; set; }


        public static IWorklistItemsSource CreateItemsSourceService => new WorklistItemsProvider();

        public static List<WorklistItem> CurrentWorklistItems { get; set; }

        public static void Start(int port, string aet)
        {
            AETitle = aet;
            _server = DicomServerFactory.Create<WorklistService>(port);
            // every 30 seconds the worklist source is queried and the current list of items is cached in _currentWorklistItems
            _itemsLoaderTimer = new System.Threading.Timer((state) =>
            {
                var newWorklistItems = CreateItemsSourceService.GetAllCurrentWorklistItems();
                CurrentWorklistItems = newWorklistItems;
            }, null, TimeSpan.Zero, TimeSpan.FromSeconds(60));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="port"></param>
        /// <param name="aet"></param>
        /// <param name="backend"> 0 - List , 1- MySQL</param>

        public static void Start(int port, string aet,int backend)
        {
            try
            {
                AETitle = aet;

                _server = DicomServerFactory.Create<WorklistService>(port);
                // every 30 seconds the worklist source is queried and the current list of items is cached in _currentWorklistItems
                _itemsLoaderTimer = new System.Threading.Timer((state) =>
                {
                    switch(backend)
                    {
                        case 0:
                            var newWorklistItems = CreateItemsSourceService.GetAllCurrentWorklistItems();
                            CurrentWorklistItems = newWorklistItems;
                            break;
                        case 1:
                            //var dbWorklistItems = CreateItemsSourceService.GetAllCurrentWorklistItemsFromDB();
                            //CurrentWorklistItems = dbWorklistItems;
                            break;
                        case 2:
                            //var dbWorklistItems = CreateItemsSourceService.GetAllCurrentWorklistItemsFromPellucidAsync();
                            var dbWorklistItems = CreateItemsSourceService.GetAllCurrentWorklistItemsFromCareAsync();
                            CurrentWorklistItems = dbWorklistItems;
                            break;
                    }
               
                }, null, TimeSpan.Zero, TimeSpan.FromSeconds(30));
            }
            catch(Exception ex)
            {
                MessageBox.Show(null, "Error Starting the Server" + ex.Message,
                                     "Error Starting the Server", MessageBoxButtons.OK,
                                     MessageBoxIcon.Error);
            }


        }

       


        public static void Stop()
        {
            _itemsLoaderTimer?.Dispose();
            _server?.Dispose();
        }


    }
}
