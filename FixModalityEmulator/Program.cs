using FellowOakDicom;
using System;
using System.IO;

class Program
{
    static void Main()
    {
        string[] dirsToFix = new[]
        {
            @"C:\Program Files (x86)\DVTk\Modality Emulator\Data\AcquisitionModality\Default",
            @"C:\Program Files (x86)\DVTk\Modality Emulator\Data\Worklist\WLM RQ"
        };
        
        Console.WriteLine("=== Fixing Modality Emulator Templates & Worklists ===\n");
        
        int totalModified = 0;
        
        foreach (string templateDir in dirsToFix)
        {
            Console.WriteLine($"\nProcessing: {templateDir}\n");
            
            if (!Directory.Exists(templateDir))
            {
                Console.WriteLine($"  ERROR: Directory not found!\n");
                continue;
            }
            
            int modifiedCount = 0;
            string[] dicomFiles = Directory.GetFiles(templateDir, "*.dcm");
            
            foreach (string filePath in dicomFiles)
            {
                string fileName = Path.GetFileName(filePath);
                
                try
                {
                    DicomFile dicomFile = DicomFile.Open(filePath);
                    
                    string currentStudyUid = dicomFile.Dataset.GetString(DicomTag.StudyInstanceUID);
                    
                    if (string.IsNullOrWhiteSpace(currentStudyUid))
                    {
                        string newStudyUid = "1.2.826.0.1.3680043.2.1545.1.2.1.7.TEMPLATE";
                        dicomFile.Dataset.AddOrUpdate(DicomTag.StudyInstanceUID, newStudyUid);
                        dicomFile.Save(filePath);
                        
                        Console.WriteLine($"  ✓ {fileName} - Added StudyInstanceUID");
                        modifiedCount++;
                        totalModified++;
                    }
                    else if (currentStudyUid == "1.2.826.0.1.3680043.2.1545.6.906.3")
                    {
                        // Already has a valid UID
                        Console.WriteLine($"  ✓ {fileName} - Already has StudyInstanceUID");
                    }
                    else
                    {
                        Console.WriteLine($"  ✓ {fileName} - StudyInstanceUID: {currentStudyUid}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  ✗ {fileName} - ERROR: {ex.Message}");
                }
            }
            
            Console.WriteLine($"\n  Modified: {modifiedCount} files");
        }
        
        Console.WriteLine($"\n=== TOTAL MODIFIED: {totalModified} ===");
        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }
}
