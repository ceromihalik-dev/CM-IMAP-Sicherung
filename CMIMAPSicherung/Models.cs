using System;
using System.Collections.Generic;

namespace CMIMAPSicherung
{
    [Serializable]
    public class AppConfig
    {
        public string BackupRoot { get; set; }
        public List<AccountConfig> Accounts { get; set; }
        public int LogRetentionDays { get; set; }
        public ScheduleConfig Schedule { get; set; }

        public bool RetryEnabled { get; set; }
        public int RetryCount { get; set; }
        public int RetryDelayMinutes { get; set; }
        public bool TrayEnabled { get; set; }
        public bool StartMinimizedToTray { get; set; }
        public List<GlobalHistoryEntry> GlobalHistory { get; set; }

        public bool VerifyAfterBackup { get; set; }
        public bool ZipAfterBackup { get; set; }
        public int ZipKeepCount { get; set; }
        public bool StorageWarningEnabled { get; set; }
        public int StorageWarningGB { get; set; }

        // Optimierungen fuer grosse Postfaecher / Datenmengen
        public bool LargeMailboxMode { get; set; }
        public int ImapTimeoutSeconds { get; set; }
        public bool PreventSleepDuringLongOperations { get; set; }
        public int LargeFileWarningGB { get; set; }
        public int LargeMailboxMinFreeGB { get; set; }
        public int ZipMaxStorageGB { get; set; }

        public AppConfig()
        {
            BackupRoot = @"D:\IMAP-Backup";
            Accounts = new List<AccountConfig>();
            LogRetentionDays = 30;
            Schedule = new ScheduleConfig();
            RetryEnabled = true;
            RetryCount = 2;
            RetryDelayMinutes = 1;
            TrayEnabled = true;
            StartMinimizedToTray = false;
            GlobalHistory = new List<GlobalHistoryEntry>();
            VerifyAfterBackup = true;
            ZipAfterBackup = false;
            ZipKeepCount = 3;
            StorageWarningEnabled = true;
            StorageWarningGB = 10;
            LargeMailboxMode = true;
            ImapTimeoutSeconds = 600;
            PreventSleepDuringLongOperations = true;
            LargeFileWarningGB = 5;
            LargeMailboxMinFreeGB = 40;
            ZipMaxStorageGB = 50;
        }
    }

    [Serializable]
    public class ScheduleConfig
    {
        public bool Enabled { get; set; }
        public string Frequency { get; set; }
        public string Time { get; set; }
        public DayOfWeek Weekday { get; set; }
        public string LastConfigured { get; set; }

        public ScheduleConfig()
        {
            Enabled = false;
            Frequency = "Daily";
            Time = "20:00";
            Weekday = DayOfWeek.Monday;
            LastConfigured = "";
        }
    }

    public class TaskStatusInfo
    {
        public bool Exists;
        public string State;
        public DateTime? NextRun;
        public DateTime? LastRun;
        public int LastResult;
        public string Details;
    }

    [Serializable]
    public class AccountConfig
    {
        public bool Enabled { get; set; }
        public string Name { get; set; }
        public string Host { get; set; }
        public int Port { get; set; }
        public string User { get; set; }
        public string Id { get; set; }
        public string LastBackup { get; set; }
        public List<HistoryEntry> History { get; set; }

        public AccountConfig()
        {
            Enabled = true;
            Port = 993;
            LastBackup = "";
            History = new List<HistoryEntry>();
        }
    }

    [Serializable]
    public class HistoryEntry
    {
        public string Timestamp { get; set; }
        public string Mode { get; set; }
        public string Result { get; set; }
        public string Details { get; set; }
        public string Duration { get; set; }
    }

    [Serializable]
    public class GlobalHistoryEntry
    {
        public string Timestamp { get; set; }
        public string Source { get; set; }
        public string Mode { get; set; }
        public int Total { get; set; }
        public int OkCount { get; set; }
        public int ErrorCount { get; set; }
        public int CancelledCount { get; set; }
        public string Duration { get; set; }
        public string LogPath { get; set; }
    }

    internal class PythonCommand
    {
        public string FileName;
        public List<string> Prefix = new List<string>();
    }

    internal enum OperationMode
    {
        Backup,
        Test
    }

    internal class OperationRequest
    {
        public OperationMode Mode;
        public List<AccountConfig> Accounts;
        public PythonCommand Python;
        public string Source;
    }

    internal class OperationSummary
    {
        public OperationMode Mode;
        public int Total;
        public int OkCount;
        public int ErrorCount;
        public int CancelledCount;
        public string LogPath;
        public string Source;
        public TimeSpan Duration;
    }

    internal class ProgressInfo
    {
        public string AccountId;
        public string Status;
        public string Message;
        public int Completed;
        public int Total;
        public bool AppendLog;
    }
}
