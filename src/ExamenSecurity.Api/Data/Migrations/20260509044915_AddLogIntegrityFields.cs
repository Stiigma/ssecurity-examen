using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExamenSecurity.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLogIntegrityFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApiClients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    OwnerTeam = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DisabledAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiClients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    FullName = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    Role = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    Department = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastLoginAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConfigurationChanges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SettingKey = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    PreviousValue = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    NewValue = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ChangedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfigurationChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConfigurationChanges_Users_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SecurityAlerts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AlertType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Severity = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    RelatedUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RelatedUsername = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    RelatedIpAddress = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    EventCount = table.Column<int>(type: "integer", nullable: false),
                    IsAcknowledged = table.Column<bool>(type: "boolean", nullable: false),
                    AcknowledgedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcknowledgedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SecurityAlerts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SecurityAlerts_Users_AcknowledgedByUserId",
                        column: x => x.AcknowledgedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SecurityAlerts_Users_RelatedUserId",
                        column: x => x.RelatedUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SecurityEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Severity = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Username = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Role = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    HttpMethod = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    Path = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    StatusCode = table.Column<int>(type: "integer", nullable: true),
                    ResourceType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ResourceId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Outcome = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    MetadataJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EventHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    PreviousHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SecurityEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SecurityEvents_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "StudentRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    EnrollmentNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Career = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Semester = table.Column<int>(type: "integer", nullable: false),
                    GradePointAverage = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    AcademicStatus = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ScholarshipStatus = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    AdvisorPrivateNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentRecords_Users_StudentUserId",
                        column: x => x.StudentUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SupportTickets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Subject = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "character varying(1500)", maxLength: 1500, nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    IsSecurityRelevant = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ClosedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupportTickets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupportTickets_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AccountLockouts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    TargetValue = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    LockedUntilUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    AlertId = table.Column<Guid>(type: "uuid", nullable: true),
                    UnlockedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UnlockedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountLockouts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountLockouts_SecurityAlerts_AlertId",
                        column: x => x.AlertId,
                        principalTable: "SecurityAlerts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AccountLockouts_Users_UnlockedByUserId",
                        column: x => x.UnlockedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountLockouts_AlertId",
                table: "AccountLockouts",
                column: "AlertId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountLockouts_IsActive",
                table: "AccountLockouts",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_AccountLockouts_LockedUntilUtc",
                table: "AccountLockouts",
                column: "LockedUntilUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AccountLockouts_TargetValue",
                table: "AccountLockouts",
                column: "TargetValue");

            migrationBuilder.CreateIndex(
                name: "IX_AccountLockouts_UnlockedByUserId",
                table: "AccountLockouts",
                column: "UnlockedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ConfigurationChanges_ChangedByUserId",
                table: "ConfigurationChanges",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SecurityAlerts_AcknowledgedByUserId",
                table: "SecurityAlerts",
                column: "AcknowledgedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SecurityAlerts_AlertType",
                table: "SecurityAlerts",
                column: "AlertType");

            migrationBuilder.CreateIndex(
                name: "IX_SecurityAlerts_CreatedAtUtc",
                table: "SecurityAlerts",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_SecurityAlerts_IsAcknowledged",
                table: "SecurityAlerts",
                column: "IsAcknowledged");

            migrationBuilder.CreateIndex(
                name: "IX_SecurityAlerts_RelatedUserId",
                table: "SecurityAlerts",
                column: "RelatedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SecurityEvents_CreatedAtUtc",
                table: "SecurityEvents",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_SecurityEvents_EventType",
                table: "SecurityEvents",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_SecurityEvents_IpAddress",
                table: "SecurityEvents",
                column: "IpAddress");

            migrationBuilder.CreateIndex(
                name: "IX_SecurityEvents_Severity",
                table: "SecurityEvents",
                column: "Severity");

            migrationBuilder.CreateIndex(
                name: "IX_SecurityEvents_UserId",
                table: "SecurityEvents",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_SecurityEvents_Username",
                table: "SecurityEvents",
                column: "Username");

            migrationBuilder.CreateIndex(
                name: "IX_StudentRecords_StudentUserId",
                table: "StudentRecords",
                column: "StudentUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_UserId",
                table: "SupportTickets",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountLockouts");

            migrationBuilder.DropTable(
                name: "ApiClients");

            migrationBuilder.DropTable(
                name: "ConfigurationChanges");

            migrationBuilder.DropTable(
                name: "SecurityEvents");

            migrationBuilder.DropTable(
                name: "StudentRecords");

            migrationBuilder.DropTable(
                name: "SupportTickets");

            migrationBuilder.DropTable(
                name: "SecurityAlerts");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
