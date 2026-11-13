namespace BurhanNexus.HMS.Core.Domain;

public enum HospitalType
{
    Government = 1,
    Private = 2
}

public enum LicenseType
{
    Trial = 1,
    Annual = 2,
    Lifetime = 3
}

public enum BackupStatus
{
    InProgress = 1,
    Completed = 2,
    Failed = 3
}

public enum BackupKind
{
    Manual = 1,
    PreRestoreSafety = 2,
    Scheduled = 3,
    Imported = 4
}
