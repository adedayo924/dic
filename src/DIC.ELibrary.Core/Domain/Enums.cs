namespace DIC.ELibrary.Core.Domain;

public enum UserRole
{
    Student = 0,
    Librarian = 1,
    Administrator = 2,

    /// <summary>Teaching staff: same access as a student. Appended to keep persisted role values stable.</summary>
    Teacher = 3
}

public enum BookStatus
{
    Draft = 0,
    Published = 1,
    Archived = 2
}

public enum OcrStatus
{
    NotApplicable = 0,
    Pending = 1,
    Processing = 2,
    Completed = 3,
    Failed = 4
}

public enum LoanStatus
{
    Active = 0,
    Returned = 1,
    Overdue = 2
}

public enum AuditAction
{
    Login = 0,
    Logout = 1,
    LoginFailed = 2,
    UserCreated = 3,
    UserUpdated = 4,
    UserDeleted = 5,
    PasswordChanged = 6,
    BookCreated = 7,
    BookUpdated = 8,
    BookDeleted = 9,
    BookImported = 10,
    PackageBuilt = 11,
    PackageInstalled = 12,
    OcrRun = 13,
    LoanCreated = 14,
    LoanReturned = 15,
    SettingsChanged = 16,
    BackupCreated = 17,
    RestorePerformed = 18
}
