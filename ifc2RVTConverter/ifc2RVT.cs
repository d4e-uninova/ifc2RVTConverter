using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace ifc2RVTConverter
{
    public class ifc2RVT
    {
        private static Logger logger = new Logger("ifc2RVT");

        [DllImport("USER32.DLL")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("USER32.DLL", SetLastError = true)]
        static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
        
        [DllImport("user32")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool EnumChildWindows(IntPtr window, EnumWindowProc callback, IntPtr i);

        private delegate bool EnumWindowProc(IntPtr hWnd, IntPtr parameter);

        private string revitPath;

        private const int HIDE = 0x5;

        public ifc2RVT(string revitPath)
        {
            this.revitPath = revitPath;
        }

        private void tryToKill(Process process)
        {
            try
            {
                logger.log("Closing process '" + process.ProcessName + "' with id " + process.Id);
                process.Kill();
                process.WaitForExit();
            }
            catch (Exception ex)
            {
                logger.log(ex.Message, Logger.LogType.ERROR);
            }
        }

        private bool Convert_(string journal,string inFile, string outFile)
        {
            bool isDirectory = Directory.Exists(outFile);
            if (isDirectory)
            {
                outFile = outFile + "\\" + Path.ChangeExtension(Path.GetFileName(inFile), "rvt");
            }

            string appPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string tmpIfcFile = appPath + @"\ifc2rvt\in.ifc";
            string tmpRvtFile = appPath + @"\ifc2rvt\out.rvt";
            Process process;
            if (!Directory.Exists(Path.GetDirectoryName(tmpIfcFile)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(tmpIfcFile));
            }
            if (File.Exists(tmpIfcFile))
            {
                File.Delete(tmpIfcFile);
            }
            if (File.Exists(tmpRvtFile))
            {
                File.Delete(tmpRvtFile);
            }
            if (File.Exists(outFile))
            {
                File.Delete(outFile);
            }
            File.Copy(inFile, tmpIfcFile);
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = this.revitPath + @"\Revit.exe";
            startInfo.Arguments = journal + " /nosplash";
            process = Process.Start(startInfo);
            logger.log("Process '" + process.ProcessName + "' with id " + process.Id + " started");
            do
            {
                Thread.Sleep(1);
            } while (process.MainWindowHandle == IntPtr.Zero);
            do
            {
                Thread.Sleep(1);
                ShowWindow(process.MainWindowHandle, HIDE);
                if (FindWindow(null, "Journal Error") != IntPtr.Zero)
                {
                    logger.log("Unknown error", Logger.LogType.ERROR);
                    tryToKill(process);
                    return false;
                }
            } while (GetChildWindows(process.MainWindowHandle).Count() > 10);
            if (!process.HasExited)
            {
                tryToKill(process);
            }
            File.Copy(tmpRvtFile, outFile);
            return true;
        }

        /// <summary>
        /// Callback method to be used when enumerating windows.
        /// </summary>
        /// <param name="handle">Handle of the next window</param>
        /// <param name="pointer">Pointer to a GCHandle that holds a reference to the list to fill</param>
        /// <returns>True to continue the enumeration, false to bail</returns>
        private static bool EnumWindow(IntPtr handle, IntPtr pointer)
        {
            GCHandle gch = GCHandle.FromIntPtr(pointer);
            List<IntPtr> list = gch.Target as List<IntPtr>;
            if (list == null)
            {
                throw new InvalidCastException("GCHandle Target could not be cast as List<IntPtr>");
            }
            list.Add(handle);
            //  You can modify this to check to see if you want to cancel the operation, then return a null here
            return true;
        }

        private static List<IntPtr> GetChildWindows(IntPtr parent)
        {
            List<IntPtr> result = new List<IntPtr>();
            GCHandle listHandle = GCHandle.Alloc(result);
            try
            {
                EnumWindowProc childProc = new EnumWindowProc(EnumWindow);
                EnumChildWindows(parent, childProc, GCHandle.ToIntPtr(listHandle));
            }
            finally
            {
                if (listHandle.IsAllocated)
                    listHandle.Free();
            }
            return result;
        }

        public bool Convert(string inFile,string outFile)
        {
            Process[] processes;
            try
            {
                string[] journals = new string[] { "ifc2rvt.txt", "ifc2rvtNoJoin.txt", "ifc2rvtNoPopUp.txt" };

                processes = Process.GetProcessesByName("Revit");
                for (var i = 0; i < processes.Length; i++)
                {
                    tryToKill(processes[i]);
                }

                for (var i = 0; i < journals.Length; i++)
                {
                    logger.log("Launching Revit with journal file " + journals[i]);
                    if (Convert_(journals[i], inFile, outFile))
                    {
                        return true;
                    }
                }
                return false;
            }
            catch (Exception e)
            {
                logger.log(e.Message, Logger.LogType.ERROR);
                processes = Process.GetProcessesByName("Revit");
                for (var i = 0; i < processes.Length; i++)
                {
                    tryToKill(processes[i]);
                }
                return false;
            }
        }

    }
}
