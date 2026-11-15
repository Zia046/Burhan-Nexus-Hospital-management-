using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BurhanNexus.HMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase6Radiology : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "radiology_order_seq");

            migrationBuilder.CreateSequence(
                name: "radiology_service_code_seq");

            migrationBuilder.AddColumn<long>(
                name: "RadiologyOrderId",
                table: "Charges",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RadiologyOrders",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrderNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<long>(type: "bigint", nullable: false),
                    EncounterId = table.Column<long>(type: "bigint", nullable: true),
                    ReferringDepartmentId = table.Column<int>(type: "integer", nullable: true),
                    ReferralType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReferringDoctorId = table.Column<int>(type: "integer", nullable: true),
                    ExternalDoctorName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    ClinicalIndication = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    BookedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    BookedByUserId = table.Column<int>(type: "integer", nullable: true),
                    BookedByUsername = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Workstation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RadiologyOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RadiologyOrders_Departments_ReferringDepartmentId",
                        column: x => x.ReferringDepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RadiologyOrders_Doctors_ReferringDoctorId",
                        column: x => x.ReferringDoctorId,
                        principalTable: "Doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RadiologyOrders_PatientEncounters_EncounterId",
                        column: x => x.EncounterId,
                        principalTable: "PatientEncounters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RadiologyOrders_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RadiologySettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    DepartmentName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    HospitalName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ContactNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    ReportHeader = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ReportFooter = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PrintHeader = table.Column<bool>(type: "boolean", nullable: false),
                    LetterheadTopMarginMm = table.Column<int>(type: "integer", nullable: false),
                    DefaultReportingDoctor = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    DefaultDesignation = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Logo = table.Column<byte[]>(type: "bytea", nullable: true),
                    LogoContentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Signature = table.Column<byte[]>(type: "bytea", nullable: true),
                    SignatureContentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ShowVerifiedBy = table.Column<bool>(type: "boolean", nullable: false),
                    AllowSelfVerification = table.Column<bool>(type: "boolean", nullable: false),
                    RequirePaymentBeforeRelease = table.Column<bool>(type: "boolean", nullable: false),
                    AllowBookingWithoutVisit = table.Column<bool>(type: "boolean", nullable: false),
                    VisitValidDays = table.Column<int>(type: "integer", nullable: false),
                    MaxUploadMb = table.Column<int>(type: "integer", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RadiologySettings", x => x.Id);
                    table.CheckConstraint("CK_RadiologySettings_Singleton", "\"Id\" = 1");
                });

            migrationBuilder.CreateTable(
                name: "RadiologyTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Modality = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Technique = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Findings = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    Impression = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Recommendations = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedByUsername = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RadiologyTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RadiologyServices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Modality = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BodyPart = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PreparationInstructions = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: true),
                    ReportTitle = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    DefaultTemplateId = table.Column<int>(type: "integer", nullable: true),
                    ServiceItemId = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RadiologyServices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RadiologyServices_RadiologyTemplates_DefaultTemplateId",
                        column: x => x.DefaultTemplateId,
                        principalTable: "RadiologyTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RadiologyServices_ServiceItems_ServiceItemId",
                        column: x => x.ServiceItemId,
                        principalTable: "ServiceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RadiologyExams",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrderId = table.Column<long>(type: "bigint", nullable: false),
                    ServiceId = table.Column<int>(type: "integer", nullable: false),
                    ServiceCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ServiceName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Modality = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BodyPart = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReportTitle = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    ScheduledUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ScheduledDate = table.Column<DateOnly>(type: "date", nullable: false),
                    TokenNumber = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ChargeId = table.Column<long>(type: "bigint", nullable: true),
                    ArrivedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StartedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PerformedByUserId = table.Column<int>(type: "integer", nullable: true),
                    PerformedByUsername = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    PerformedByName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    TechnicalNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ClinicalIndication = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Technique = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Findings = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    Impression = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Recommendations = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ReportingDoctorName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    ReportingDoctorDesignation = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    TemplateName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ReportSavedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReportedByUserId = table.Column<int>(type: "integer", nullable: true),
                    ReportedByUsername = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SubmittedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VerifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VerifiedByUserId = table.Column<int>(type: "integer", nullable: true),
                    VerifiedByUsername = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    VerifiedByName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    CorrectionReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ReleasedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevisionNo = table.Column<int>(type: "integer", nullable: false),
                    CancelledUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledByUsername = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CancelReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RadiologyExams", x => x.Id);
                    table.CheckConstraint("CK_RadiologyExams_Token", "\"TokenNumber\" > 0");
                    table.ForeignKey(
                        name: "FK_RadiologyExams_Charges_ChargeId",
                        column: x => x.ChargeId,
                        principalTable: "Charges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RadiologyExams_RadiologyOrders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "RadiologyOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RadiologyExams_RadiologyServices_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "RadiologyServices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RadiologyAttachments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ExamId = table.Column<long>(type: "bigint", nullable: false),
                    PatientId = table.Column<long>(type: "bigint", nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StoredPath = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Kind = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    UploadedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UploadedByUsername = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    IsRemoved = table.Column<bool>(type: "boolean", nullable: false),
                    RemovedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RemovedByUsername = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    RemoveReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RadiologyAttachments", x => x.Id);
                    table.CheckConstraint("CK_RadiologyAttachments_Size", "\"SizeBytes\" > 0");
                    table.ForeignKey(
                        name: "FK_RadiologyAttachments_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RadiologyAttachments_RadiologyExams_ExamId",
                        column: x => x.ExamId,
                        principalTable: "RadiologyExams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RadiologyExamEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ExamId = table.Column<long>(type: "bigint", nullable: false),
                    EventType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: true),
                    Username = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RadiologyExamEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RadiologyExamEvents_RadiologyExams_ExamId",
                        column: x => x.ExamId,
                        principalTable: "RadiologyExams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RadiologyReportPrints",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ExamId = table.Column<long>(type: "bigint", nullable: false),
                    CopyNumber = table.Column<int>(type: "integer", nullable: false),
                    Format = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PrintedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PrintedByUsername = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Workstation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RadiologyReportPrints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RadiologyReportPrints_RadiologyExams_ExamId",
                        column: x => x.ExamId,
                        principalTable: "RadiologyExams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RadiologyReportRevisions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ExamId = table.Column<long>(type: "bigint", nullable: false),
                    RevisionNo = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Snapshot = table.Column<string>(type: "jsonb", nullable: false),
                    PreviousStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUsername = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RadiologyReportRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RadiologyReportRevisions_RadiologyExams_ExamId",
                        column: x => x.ExamId,
                        principalTable: "RadiologyExams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Charges_RadiologyOrderId",
                table: "Charges",
                column: "RadiologyOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_RadiologyAttachments_ExamId",
                table: "RadiologyAttachments",
                column: "ExamId");

            migrationBuilder.CreateIndex(
                name: "IX_RadiologyAttachments_PatientId",
                table: "RadiologyAttachments",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_RadiologyAttachments_StoredPath",
                table: "RadiologyAttachments",
                column: "StoredPath",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RadiologyExamEvents_EventType_BusinessDate",
                table: "RadiologyExamEvents",
                columns: new[] { "EventType", "BusinessDate" });

            migrationBuilder.CreateIndex(
                name: "IX_RadiologyExamEvents_ExamId",
                table: "RadiologyExamEvents",
                column: "ExamId");

            migrationBuilder.CreateIndex(
                name: "IX_RadiologyExams_ChargeId",
                table: "RadiologyExams",
                column: "ChargeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RadiologyExams_Modality_ScheduledDate_TokenNumber",
                table: "RadiologyExams",
                columns: new[] { "Modality", "ScheduledDate", "TokenNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_RadiologyExams_OrderId",
                table: "RadiologyExams",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_RadiologyExams_ServiceId",
                table: "RadiologyExams",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_RadiologyExams_Status_Modality_ScheduledDate",
                table: "RadiologyExams",
                columns: new[] { "Status", "Modality", "ScheduledDate" });

            migrationBuilder.CreateIndex(
                name: "IX_RadiologyOrders_BusinessDate",
                table: "RadiologyOrders",
                column: "BusinessDate");

            migrationBuilder.CreateIndex(
                name: "IX_RadiologyOrders_EncounterId",
                table: "RadiologyOrders",
                column: "EncounterId");

            migrationBuilder.CreateIndex(
                name: "IX_RadiologyOrders_OrderNumber",
                table: "RadiologyOrders",
                column: "OrderNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RadiologyOrders_PatientId_BusinessDate",
                table: "RadiologyOrders",
                columns: new[] { "PatientId", "BusinessDate" });

            migrationBuilder.CreateIndex(
                name: "IX_RadiologyOrders_ReferringDepartmentId",
                table: "RadiologyOrders",
                column: "ReferringDepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_RadiologyOrders_ReferringDoctorId",
                table: "RadiologyOrders",
                column: "ReferringDoctorId");

            migrationBuilder.CreateIndex(
                name: "UX_RadiologyOrders_Idempotency",
                table: "RadiologyOrders",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RadiologyReportPrints_ExamId_CopyNumber",
                table: "RadiologyReportPrints",
                columns: new[] { "ExamId", "CopyNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RadiologyReportRevisions_ExamId_RevisionNo",
                table: "RadiologyReportRevisions",
                columns: new[] { "ExamId", "RevisionNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RadiologyServices_Code",
                table: "RadiologyServices",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RadiologyServices_DefaultTemplateId",
                table: "RadiologyServices",
                column: "DefaultTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_RadiologyServices_ServiceItemId",
                table: "RadiologyServices",
                column: "ServiceItemId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RadiologyTemplates_Modality_Name",
                table: "RadiologyTemplates",
                columns: new[] { "Modality", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Charges_RadiologyOrders_RadiologyOrderId",
                table: "Charges",
                column: "RadiologyOrderId",
                principalTable: "RadiologyOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Charges_RadiologyOrders_RadiologyOrderId",
                table: "Charges");

            migrationBuilder.DropTable(
                name: "RadiologyAttachments");

            migrationBuilder.DropTable(
                name: "RadiologyExamEvents");

            migrationBuilder.DropTable(
                name: "RadiologyReportPrints");

            migrationBuilder.DropTable(
                name: "RadiologyReportRevisions");

            migrationBuilder.DropTable(
                name: "RadiologySettings");

            migrationBuilder.DropTable(
                name: "RadiologyExams");

            migrationBuilder.DropTable(
                name: "RadiologyOrders");

            migrationBuilder.DropTable(
                name: "RadiologyServices");

            migrationBuilder.DropTable(
                name: "RadiologyTemplates");

            migrationBuilder.DropIndex(
                name: "IX_Charges_RadiologyOrderId",
                table: "Charges");

            migrationBuilder.DropColumn(
                name: "RadiologyOrderId",
                table: "Charges");

            migrationBuilder.DropSequence(
                name: "radiology_order_seq");

            migrationBuilder.DropSequence(
                name: "radiology_service_code_seq");
        }
    }
}
