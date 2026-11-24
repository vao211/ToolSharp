using System.Diagnostics;

namespace AlwaysOnTop;

internal static class Program
{
   private const string ShutdownEventName = @"Local\AlwaysOnTopTool_Shutdown";

   [STAThread]
   private static void Main()
   {
      CloseExistingInstances();

      using var shutdownEvent = new EventWaitHandle(false, EventResetMode.ManualReset, ShutdownEventName);
      shutdownEvent.Reset();

      Application.EnableVisualStyles();
      Application.SetCompatibleTextRenderingDefault(false);

      using var form = new HiddenForm(shutdownEvent);
      if (!form.IsInitialized)
      {
         return;
      }

      Application.Run(form);
   }

   private static void CloseExistingInstances()
   {
      var signaled = false;
      try
      {
         using var existingEvent = EventWaitHandle.OpenExisting(ShutdownEventName);
         existingEvent.Set();
         signaled = true;
      }
      catch (WaitHandleCannotBeOpenedException)
      {
         // No instance listening to the shutdown event (e.g. older version or no running instance).
      }

      using var currentProcess = Process.GetCurrentProcess();
      var existingProcesses = Process.GetProcessesByName(currentProcess.ProcessName)
         .Where(p => p.Id != currentProcess.Id)
         .ToList();

      foreach (var proc in existingProcesses)
      {
         using (proc)
         {
            try
            {
               if (signaled && proc.WaitForExit(1000))
               {
                  continue;
               }

               proc.Kill();
               proc.WaitForExit(1000);
            }
            catch
            {
               // Ignore if the process already exited or cannot be terminated.
            }
         }
      }
   }
}