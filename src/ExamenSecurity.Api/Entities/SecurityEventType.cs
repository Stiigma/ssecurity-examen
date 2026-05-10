namespace ExamenSecurity.Api.Entities;

public enum SecurityEventType
{
    LoginSucceeded = 1,
    LoginFailed = 2,
    DisabledAccountLoginAttempt = 3,
    PasswordResetRequested = 4,
    TokenAuthenticationFailed = 5,
    UnauthorizedRequest = 6,
    AccessDenied = 7,
    StudentRecordAccessed = 8,
    StudentRecordAccessDenied = 9,
    AdminUserCreated = 10,
    AdminUserDisabled = 11,
    ApiClientDisabled = 12,
    ConfigurationChanged = 13,
    SecurityTicketCreated = 14,
    AlertAcknowledged = 15,
    UnhandledException = 16,
    RateLimitExceeded = 17,
    AccountLocked = 18,
    AccountUnlocked = 19,
    LockoutAttemptDuringLock = 20,
    IntegrityCheckPerformed = 21,
    HoneytokenTriggered = 22,
    ImpossibleTravelDetected = 23
}
