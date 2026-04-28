namespace ExamenSecurity.Api.Data;

public static class DemoIds
{
    public static readonly Guid AdminUserId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    public static readonly Guid AuditorUserId = Guid.Parse("10000000-0000-0000-0000-000000000002");
    public static readonly Guid StudentOneUserId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    public static readonly Guid StudentTwoUserId = Guid.Parse("20000000-0000-0000-0000-000000000002");
    public static readonly Guid DisabledStudentUserId = Guid.Parse("20000000-0000-0000-0000-000000000003");

    public static readonly Guid StudentOneRecordId = Guid.Parse("30000000-0000-0000-0000-000000000001");
    public static readonly Guid StudentTwoRecordId = Guid.Parse("30000000-0000-0000-0000-000000000002");
    public static readonly Guid DisabledStudentRecordId = Guid.Parse("30000000-0000-0000-0000-000000000003");
}
