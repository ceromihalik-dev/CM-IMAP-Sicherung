using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Windows.Forms;

namespace CMIMAPSicherung
{
    internal static class TaskSchedulerManager
    {
        public const string TaskName = "CM IMAP Sicherung - Automatisch";

        public static bool CreateOrUpdate(ScheduleConfig schedule, out string details)
        {
            details = "";
            if (schedule == null || !schedule.Enabled) return Remove(out details);

            string exe = Application.ExecutablePath;
            if (String.IsNullOrWhiteSpace(exe) || !File.Exists(exe))
            {
                details = "Die Programmdatei wurde nicht gefunden: " + exe;
                return false;
            }

            var env = new Dictionary<string, string>();
            env["CM_TASK"] = TaskName;
            env["CM_EXE"] = exe;
            env["CM_FREQ"] = schedule.Frequency ?? "Daily";
            env["CM_TIME"] = String.IsNullOrWhiteSpace(schedule.Time) ? "20:00" : schedule.Time;
            env["CM_DAY"] = schedule.Weekday.ToString();
            env["CM_USER"] = WindowsIdentity.GetCurrent().Name;

            string script =
                "$ErrorActionPreference='Stop'; " +
                "Import-Module ScheduledTasks; " +
                "$action=New-ScheduledTaskAction -Execute $env:CM_EXE -Argument '/scheduled'; " +
                "$settings=New-ScheduledTaskSettingsSet -StartWhenAvailable -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries; " +
                "$trigger=$null; " +
                "if($env:CM_FREQ -eq 'Daily'){ $at=[datetime]::Today.Add([timespan]::Parse($env:CM_TIME)); $trigger=New-ScheduledTaskTrigger -Daily -At $at } " +
                "elseif($env:CM_FREQ -eq 'Weekly'){ $at=[datetime]::Today.Add([timespan]::Parse($env:CM_TIME)); $trigger=New-ScheduledTaskTrigger -Weekly -DaysOfWeek $env:CM_DAY -At $at } " +
                "elseif($env:CM_FREQ -eq 'Logon'){ $trigger=New-ScheduledTaskTrigger -AtLogOn -User $env:CM_USER } " +
                "else { throw 'Unbekannter Zeitplantyp' }; " +
                "Register-ScheduledTask -TaskName $env:CM_TASK -Action $action -Trigger $trigger -Settings $settings -Description 'Automatische Sicherung durch CM IMAP Sicherung' -Force | Out-Null; " +
                "Write-Output 'OK'";

            int rc = RunPowerShell(script, env, out details);
            return rc == 0;
        }

        public static bool Remove(out string details)
        {
            var env = new Dictionary<string, string>();
            env["CM_TASK"] = TaskName;
            string script =
                "$ErrorActionPreference='Stop'; Import-Module ScheduledTasks; " +
                "$t=Get-ScheduledTask -TaskName $env:CM_TASK -ErrorAction SilentlyContinue; " +
                "if($null -ne $t){ Unregister-ScheduledTask -TaskName $env:CM_TASK -Confirm:$false }; Write-Output 'OK'";
            return RunPowerShell(script, env, out details) == 0;
        }

        public static TaskStatusInfo QueryStatus()
        {
            var result = new TaskStatusInfo { Exists = false, State = "Nicht eingerichtet", LastResult = 0 };
            try
            {
                var env = new Dictionary<string, string>();
                env["CM_TASK"] = TaskName;
                string script =
                    "$ErrorActionPreference='Stop'; Import-Module ScheduledTasks; " +
                    "$t=Get-ScheduledTask -TaskName $env:CM_TASK -ErrorAction SilentlyContinue; " +
                    "if($null -eq $t){ Write-Output 'MISSING'; exit 0 }; " +
                    "$i=Get-ScheduledTaskInfo -TaskName $env:CM_TASK; " +
                    "$next=if($i.NextRunTime -gt [datetime]::MinValue){$i.NextRunTime.ToString('o')}else{''}; " +
                    "$last=if($i.LastRunTime -gt [datetime]::MinValue){$i.LastRunTime.ToString('o')}else{''}; " +
                    "Write-Output ('OK|'+$t.State+'|'+$next+'|'+$last+'|'+$i.LastTaskResult)";
                string output;
                int rc = RunPowerShell(script, env, out output);
                if (rc != 0) { result.Details = output; return result; }
                string line = (output ?? "").Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
                if (String.Equals(line, "MISSING", StringComparison.OrdinalIgnoreCase)) return result;
                if (line != null && line.StartsWith("OK|", StringComparison.OrdinalIgnoreCase))
                {
                    string[] parts = line.Split('|');
                    result.Exists = true;
                    if (parts.Length > 1) result.State = parts[1];
                    DateTime dt;
                    if (parts.Length > 2 && DateTime.TryParse(parts[2], null, DateTimeStyles.RoundtripKind, out dt)) result.NextRun = dt;
                    if (parts.Length > 3 && DateTime.TryParse(parts[3], null, DateTimeStyles.RoundtripKind, out dt)) result.LastRun = dt;
                    int code;
                    if (parts.Length > 4 && Int32.TryParse(parts[4], out code)) result.LastResult = code;
                }
            }
            catch (Exception ex) { result.Details = ex.Message; }
            return result;
        }

        private static int RunPowerShell(string script, Dictionary<string, string> env, out string output)
        {
            string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
            var psi = new ProcessStartInfo();
            psi.FileName = "powershell.exe";
            psi.Arguments = "-NoProfile -ExecutionPolicy Bypass -EncodedCommand " + encoded;
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            foreach (var kv in env) psi.EnvironmentVariables[kv.Key] = kv.Value ?? "";
            using (var p = Process.Start(psi))
            {
                string stdout = p.StandardOutput.ReadToEnd();
                string stderr = p.StandardError.ReadToEnd();
                p.WaitForExit();
                output = (stdout + Environment.NewLine + stderr).Trim();
                return p.ExitCode;
            }
        }
    }
}
