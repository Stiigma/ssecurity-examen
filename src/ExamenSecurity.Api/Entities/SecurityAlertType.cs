namespace ExamenSecurity.Api.Entities;

public enum SecurityAlertType
{
    MultipleFailedLogins = 1,
    DisabledAccountTargeted = 2,
    AdminEndpointProbing = 3,
    StudentRecordProbing = 4,
    SensitiveAdminChange = 5,
    SecurityTicketRequiresReview = 6,
    RepeatedUnhandledErrors = 7,
    HoneytokenAccessed = 8,
    ImpossibleTravel = 9
}
