using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BurhanNexus.HMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase8Ot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "cssd_batch_seq");

            migrationBuilder.CreateSequence(
                name: "surgery_seq");

            migrationBuilder.AddColumn<long>(
                name: "SurgeryId",
                table: "ClinicalRecords",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "SurgeryId",
                table: "Charges",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CssdBatches",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Method = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Steriliser = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    CycleNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    StartUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Parameters = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FailReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ResponsibleStaff = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ResultUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResultBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ReleasedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReleasedBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CssdBatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CssdInstruments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Category = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CssdInstruments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OtChecklistItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Stage = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsMandatory = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OtChecklistItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OtConsentTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TemplateText = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OtConsentTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OtProcedures",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    DepartmentId = table.Column<int>(type: "integer", nullable: true),
                    Specialty = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Category = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    AnesthesiaTypes = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DefaultOtType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ServiceItemId = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OtProcedures", x => x.Id);
                    table.CheckConstraint("CK_OtProcedures_Duration", "\"DurationMinutes\" BETWEEN 5 AND 1440");
                    table.ForeignKey(
                        name: "FK_OtProcedures_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OtProcedures_ServiceItems_ServiceItemId",
                        column: x => x.ServiceItemId,
                        principalTable: "ServiceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OtSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    SurgeonFeeItemId = table.Column<int>(type: "integer", nullable: true),
                    AssistantFeeItemId = table.Column<int>(type: "integer", nullable: true),
                    AnesthesiaFeeItemId = table.Column<int>(type: "integer", nullable: true),
                    AnesthesiaSuppliesItemId = table.Column<int>(type: "integer", nullable: true),
                    RecoveryItemId = table.Column<int>(type: "integer", nullable: true),
                    EquipmentItemId = table.Column<int>(type: "integer", nullable: true),
                    ConsumablesItemId = table.Column<int>(type: "integer", nullable: true),
                    DefaultCleaningMinutes = table.Column<int>(type: "integer", nullable: false),
                    PrintHeader = table.Column<bool>(type: "boolean", nullable: false),
                    LetterheadTopMarginMm = table.Column<int>(type: "integer", nullable: false),
                    FormFooter = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    MaxUploadMb = table.Column<int>(type: "integer", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OtSettings", x => x.Id);
                    table.CheckConstraint("CK_OtSettings_Single", "\"Id\" = 1");
                });

            migrationBuilder.CreateTable(
                name: "OtTheatres",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Location = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Equipment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    OpensAt = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    ClosesAt = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    CleaningMinutes = table.Column<int>(type: "integer", nullable: false),
                    IsAvailable = table.Column<bool>(type: "boolean", nullable: false),
                    UnavailableReason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    StockLocationId = table.Column<int>(type: "integer", nullable: true),
                    ChargeServiceItemId = table.Column<int>(type: "integer", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OtTheatres", x => x.Id);
                    table.CheckConstraint("CK_OtTheatres_Cleaning", "\"CleaningMinutes\" BETWEEN 0 AND 480");
                    table.ForeignKey(
                        name: "FK_OtTheatres_ServiceItems_ChargeServiceItemId",
                        column: x => x.ChargeServiceItemId,
                        principalTable: "ServiceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OtTheatres_StockLocations_StockLocationId",
                        column: x => x.StockLocationId,
                        principalTable: "StockLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CssdSets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TheatreId = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SterileUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    LastBatchId = table.Column<long>(type: "bigint", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CssdSets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CssdSets_OtTheatres_TheatreId",
                        column: x => x.TheatreId,
                        principalTable: "OtTheatres",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OtEquipment",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    TheatreId = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    LastMaintenance = table.Column<DateOnly>(type: "date", nullable: true),
                    NextMaintenance = table.Column<DateOnly>(type: "date", nullable: true),
                    ServiceNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OtEquipment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OtEquipment_OtTheatres_TheatreId",
                        column: x => x.TheatreId,
                        principalTable: "OtTheatres",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Surgeries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<long>(type: "bigint", nullable: false),
                    IpdAdmissionId = table.Column<long>(type: "bigint", nullable: true),
                    DepartmentId = table.Column<int>(type: "integer", nullable: true),
                    ProcedureId = table.Column<int>(type: "integer", nullable: true),
                    ProcedureName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Diagnosis = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Site = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    TheatreId = table.Column<int>(type: "integer", nullable: false),
                    PlannedStartUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    PlannedEndUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CleaningMinutes = table.Column<int>(type: "integer", nullable: false),
                    PlannedDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AnesthesiaType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    AnesthesiaDetail = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    EmergencyReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ClinicalNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StatusReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ActualStartUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActualEndUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AnesthesiaStartUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AnesthesiaEndUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AnesthesiaProvider = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: true),
                    CreatedByUsername = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Surgeries", x => x.Id);
                    table.CheckConstraint("CK_Surgeries_Period", "\"PlannedEndUtc\" > \"PlannedStartUtc\" AND \"DurationMinutes\" > 0 AND \"CleaningMinutes\" >= 0");
                    table.ForeignKey(
                        name: "FK_Surgeries_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Surgeries_IpdAdmissions_IpdAdmissionId",
                        column: x => x.IpdAdmissionId,
                        principalTable: "IpdAdmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Surgeries_OtProcedures_ProcedureId",
                        column: x => x.ProcedureId,
                        principalTable: "OtProcedures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Surgeries_OtTheatres_TheatreId",
                        column: x => x.TheatreId,
                        principalTable: "OtTheatres",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Surgeries_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CssdBatchSets",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BatchId = table.Column<long>(type: "bigint", nullable: false),
                    SetId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CssdBatchSets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CssdBatchSets_CssdBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "CssdBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CssdBatchSets_CssdSets_SetId",
                        column: x => x.SetId,
                        principalTable: "CssdSets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CssdSetItems",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SetId = table.Column<int>(type: "integer", nullable: false),
                    InstrumentId = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CssdSetItems", x => x.Id);
                    table.CheckConstraint("CK_CssdSetItems_Quantity", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_CssdSetItems_CssdInstruments_InstrumentId",
                        column: x => x.InstrumentId,
                        principalTable: "CssdInstruments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CssdSetItems_CssdSets_SetId",
                        column: x => x.SetId,
                        principalTable: "CssdSets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OtEquipmentLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EquipmentId = table.Column<int>(type: "integer", nullable: false),
                    Event = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    FromValue = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    ToValue = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Username = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OtEquipmentLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OtEquipmentLogs_OtEquipment_EquipmentId",
                        column: x => x.EquipmentId,
                        principalTable: "OtEquipment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CssdIssues",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SetId = table.Column<int>(type: "integer", nullable: false),
                    SurgeryId = table.Column<long>(type: "bigint", nullable: true),
                    BatchId = table.Column<long>(type: "bigint", nullable: true),
                    IssuedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IssuedBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    IssuedTo = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    ReturnedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReturnedBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ReturnNote = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CssdIssues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CssdIssues_CssdBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "CssdBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CssdIssues_CssdSets_SetId",
                        column: x => x.SetId,
                        principalTable: "CssdSets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CssdIssues_Surgeries_SurgeryId",
                        column: x => x.SurgeryId,
                        principalTable: "Surgeries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OperativeNotes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SurgeryId = table.Column<long>(type: "bigint", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    AmendsNoteId = table.Column<long>(type: "bigint", nullable: true),
                    AmendReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Positioning = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Site = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    ProcedurePerformed = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    OperativeFindings = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Technique = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    BloodLossMl = table.Column<int>(type: "integer", nullable: true),
                    IvFluids = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DrugsAdministered = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Specimens = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Implants = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Complications = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    AdditionalProcedures = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Observations = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    PreOpDiagnosis = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PostOpDiagnosis = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PostOpInstructions = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SurgeonNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SurgeonName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    FinalizedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FinalizedBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperativeNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OperativeNotes_OperativeNotes_AmendsNoteId",
                        column: x => x.AmendsNoteId,
                        principalTable: "OperativeNotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperativeNotes_Surgeries_SurgeryId",
                        column: x => x.SurgeryId,
                        principalTable: "Surgeries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OtConsumptions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SurgeryId = table.Column<long>(type: "bigint", nullable: false),
                    PatientId = table.Column<long>(type: "bigint", nullable: false),
                    LocationId = table.Column<int>(type: "integer", nullable: false),
                    MedicineId = table.Column<int>(type: "integer", nullable: false),
                    MedicineName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BatchId = table.Column<long>(type: "bigint", nullable: false),
                    BatchNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ReturnOfId = table.Column<long>(type: "bigint", nullable: true),
                    Note = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: true),
                    Username = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OtConsumptions", x => x.Id);
                    table.CheckConstraint("CK_OtConsumptions_Quantity", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_OtConsumptions_MedicineBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "MedicineBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OtConsumptions_Medicines_MedicineId",
                        column: x => x.MedicineId,
                        principalTable: "Medicines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OtConsumptions_OtConsumptions_ReturnOfId",
                        column: x => x.ReturnOfId,
                        principalTable: "OtConsumptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OtConsumptions_StockLocations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "StockLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OtConsumptions_Surgeries_SurgeryId",
                        column: x => x.SurgeryId,
                        principalTable: "Surgeries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OtDrugEntries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SurgeryId = table.Column<long>(type: "bigint", nullable: false),
                    Phase = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Kind = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Dose = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Route = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    GivenUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GivenBy = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Username = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OtDrugEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OtDrugEntries_Surgeries_SurgeryId",
                        column: x => x.SurgeryId,
                        principalTable: "Surgeries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RecoveryEpisodes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SurgeryId = table.Column<long>(type: "bigint", nullable: false),
                    PatientId = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ArrivalUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Nurse = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    AnesthesiaSummary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Destination = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    HandoverStaff = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    TransferUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    HandoverNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DecisionBy = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecoveryEpisodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecoveryEpisodes_Surgeries_SurgeryId",
                        column: x => x.SurgeryId,
                        principalTable: "Surgeries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SurgeryCharges",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SurgeryId = table.Column<long>(type: "bigint", nullable: false),
                    Kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ChargeId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SurgeryCharges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SurgeryCharges_Charges_ChargeId",
                        column: x => x.ChargeId,
                        principalTable: "Charges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SurgeryCharges_Surgeries_SurgeryId",
                        column: x => x.SurgeryId,
                        principalTable: "Surgeries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SurgeryChecklistEntries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SurgeryId = table.Column<long>(type: "bigint", nullable: false),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    Stage = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ItemLabel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Answer = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Remarks = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    RecordedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: true),
                    Username = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SurgeryChecklistEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SurgeryChecklistEntries_OtChecklistItems_ItemId",
                        column: x => x.ItemId,
                        principalTable: "OtChecklistItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SurgeryChecklistEntries_Surgeries_SurgeryId",
                        column: x => x.SurgeryId,
                        principalTable: "Surgeries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SurgeryChecklistStages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SurgeryId = table.Column<long>(type: "bigint", nullable: false),
                    Stage = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CompletedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: true),
                    Username = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Remarks = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    IsException = table.Column<bool>(type: "boolean", nullable: false),
                    ExceptionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PendingItems = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SurgeryChecklistStages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SurgeryChecklistStages_Surgeries_SurgeryId",
                        column: x => x.SurgeryId,
                        principalTable: "Surgeries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SurgeryConsents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SurgeryId = table.Column<long>(type: "bigint", nullable: false),
                    ConsentTypeId = table.Column<int>(type: "integer", nullable: false),
                    ConsentTypeName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    GivenBy = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Relation = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    ConsentUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ResponsibleStaff = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Witness = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SurgeryConsents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SurgeryConsents_OtConsentTypes_ConsentTypeId",
                        column: x => x.ConsentTypeId,
                        principalTable: "OtConsentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SurgeryConsents_Surgeries_SurgeryId",
                        column: x => x.SurgeryId,
                        principalTable: "Surgeries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SurgeryEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SurgeryId = table.Column<long>(type: "bigint", nullable: false),
                    Category = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Event = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    FromValue = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ToValue = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: true),
                    Username = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SurgeryEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SurgeryEvents_Surgeries_SurgeryId",
                        column: x => x.SurgeryId,
                        principalTable: "Surgeries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SurgerySpecimens",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SurgeryId = table.Column<long>(type: "bigint", nullable: false),
                    PatientId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Type = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    CollectedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CollectedBy = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Destination = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    LabOrderId = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DispatchedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DispatchedBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ReceivedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReceivedBy = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Remarks = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SurgerySpecimens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SurgerySpecimens_LabOrders_LabOrderId",
                        column: x => x.LabOrderId,
                        principalTable: "LabOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SurgerySpecimens_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SurgerySpecimens_Surgeries_SurgeryId",
                        column: x => x.SurgeryId,
                        principalTable: "Surgeries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SurgeryTeamMembers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SurgeryId = table.Column<long>(type: "bigint", nullable: false),
                    Role = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DoctorId = table.Column<int>(type: "integer", nullable: true),
                    UserId = table.Column<int>(type: "integer", nullable: true),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    StartUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AddedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AddedBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    RemovedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RemovedBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    RemovedReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SurgeryTeamMembers", x => x.Id);
                    table.CheckConstraint("CK_SurgeryTeamMembers_Person", "\"DoctorId\" IS NOT NULL OR \"UserId\" IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_SurgeryTeamMembers_Doctors_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "Doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SurgeryTeamMembers_Surgeries_SurgeryId",
                        column: x => x.SurgeryId,
                        principalTable: "Surgeries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SurgeryTeamMembers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OtDocuments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SurgeryId = table.Column<long>(type: "bigint", nullable: false),
                    ConsentId = table.Column<long>(type: "bigint", nullable: true),
                    Kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Title = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    StoredPath = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OriginalName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UploadedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UploadedBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OtDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OtDocuments_Surgeries_SurgeryId",
                        column: x => x.SurgeryId,
                        principalTable: "Surgeries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OtDocuments_SurgeryConsents_ConsentId",
                        column: x => x.ConsentId,
                        principalTable: "SurgeryConsents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalRecords_SurgeryId_Kind",
                table: "ClinicalRecords",
                columns: new[] { "SurgeryId", "Kind" });

            migrationBuilder.CreateIndex(
                name: "IX_Charges_SurgeryId",
                table: "Charges",
                column: "SurgeryId");

            migrationBuilder.CreateIndex(
                name: "IX_CssdBatches_Number",
                table: "CssdBatches",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CssdBatchSets_BatchId_SetId",
                table: "CssdBatchSets",
                columns: new[] { "BatchId", "SetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CssdBatchSets_SetId",
                table: "CssdBatchSets",
                column: "SetId");

            migrationBuilder.CreateIndex(
                name: "IX_CssdInstruments_Code",
                table: "CssdInstruments",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CssdIssues_BatchId",
                table: "CssdIssues",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_CssdIssues_SurgeryId",
                table: "CssdIssues",
                column: "SurgeryId");

            migrationBuilder.CreateIndex(
                name: "UX_CssdIssues_OpenSet",
                table: "CssdIssues",
                column: "SetId",
                unique: true,
                filter: "\"ReturnedUtc\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CssdSetItems_InstrumentId",
                table: "CssdSetItems",
                column: "InstrumentId");

            migrationBuilder.CreateIndex(
                name: "IX_CssdSetItems_SetId_InstrumentId",
                table: "CssdSetItems",
                columns: new[] { "SetId", "InstrumentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CssdSets_Code",
                table: "CssdSets",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CssdSets_TheatreId",
                table: "CssdSets",
                column: "TheatreId");

            migrationBuilder.CreateIndex(
                name: "IX_OperativeNotes_AmendsNoteId",
                table: "OperativeNotes",
                column: "AmendsNoteId",
                unique: true,
                filter: "\"AmendsNoteId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OperativeNotes_SurgeryId_Version",
                table: "OperativeNotes",
                columns: new[] { "SurgeryId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_OperativeNotes_OneDraft",
                table: "OperativeNotes",
                column: "SurgeryId",
                unique: true,
                filter: "\"Status\" = 'Draft'");

            migrationBuilder.CreateIndex(
                name: "IX_OtChecklistItems_Stage_Label",
                table: "OtChecklistItems",
                columns: new[] { "Stage", "Label" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OtConsentTypes_Name",
                table: "OtConsentTypes",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OtConsumptions_BatchId",
                table: "OtConsumptions",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_OtConsumptions_LocationId",
                table: "OtConsumptions",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_OtConsumptions_MedicineId",
                table: "OtConsumptions",
                column: "MedicineId");

            migrationBuilder.CreateIndex(
                name: "IX_OtConsumptions_ReturnOfId",
                table: "OtConsumptions",
                column: "ReturnOfId");

            migrationBuilder.CreateIndex(
                name: "IX_OtConsumptions_SurgeryId",
                table: "OtConsumptions",
                column: "SurgeryId");

            migrationBuilder.CreateIndex(
                name: "IX_OtDocuments_ConsentId",
                table: "OtDocuments",
                column: "ConsentId");

            migrationBuilder.CreateIndex(
                name: "IX_OtDocuments_StoredPath",
                table: "OtDocuments",
                column: "StoredPath",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OtDocuments_SurgeryId",
                table: "OtDocuments",
                column: "SurgeryId");

            migrationBuilder.CreateIndex(
                name: "IX_OtDrugEntries_SurgeryId_GivenUtc",
                table: "OtDrugEntries",
                columns: new[] { "SurgeryId", "GivenUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OtEquipment_Code",
                table: "OtEquipment",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OtEquipment_TheatreId",
                table: "OtEquipment",
                column: "TheatreId");

            migrationBuilder.CreateIndex(
                name: "IX_OtEquipmentLogs_EquipmentId",
                table: "OtEquipmentLogs",
                column: "EquipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_OtProcedures_Code",
                table: "OtProcedures",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OtProcedures_DepartmentId",
                table: "OtProcedures",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_OtProcedures_ServiceItemId",
                table: "OtProcedures",
                column: "ServiceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_OtTheatres_ChargeServiceItemId",
                table: "OtTheatres",
                column: "ChargeServiceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_OtTheatres_Code",
                table: "OtTheatres",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OtTheatres_StockLocationId",
                table: "OtTheatres",
                column: "StockLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_RecoveryEpisodes_Status",
                table: "RecoveryEpisodes",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_RecoveryEpisodes_SurgeryId",
                table: "RecoveryEpisodes",
                column: "SurgeryId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Surgeries_DepartmentId",
                table: "Surgeries",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Surgeries_IpdAdmissionId",
                table: "Surgeries",
                column: "IpdAdmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_Surgeries_Number",
                table: "Surgeries",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Surgeries_PatientId",
                table: "Surgeries",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_Surgeries_PlannedDate_Status",
                table: "Surgeries",
                columns: new[] { "PlannedDate", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Surgeries_ProcedureId",
                table: "Surgeries",
                column: "ProcedureId");

            migrationBuilder.CreateIndex(
                name: "IX_Surgeries_TheatreId_PlannedStartUtc",
                table: "Surgeries",
                columns: new[] { "TheatreId", "PlannedStartUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_Surgeries_Idempotency",
                table: "Surgeries",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SurgeryCharges_ChargeId",
                table: "SurgeryCharges",
                column: "ChargeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_SurgeryCharges_Standard",
                table: "SurgeryCharges",
                columns: new[] { "SurgeryId", "Kind" },
                unique: true,
                filter: "\"Kind\" <> 'Other'");

            migrationBuilder.CreateIndex(
                name: "IX_SurgeryChecklistEntries_ItemId",
                table: "SurgeryChecklistEntries",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_SurgeryChecklistEntries_SurgeryId_ItemId_Id",
                table: "SurgeryChecklistEntries",
                columns: new[] { "SurgeryId", "ItemId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_SurgeryChecklistStages_SurgeryId_Stage",
                table: "SurgeryChecklistStages",
                columns: new[] { "SurgeryId", "Stage" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SurgeryConsents_ConsentTypeId",
                table: "SurgeryConsents",
                column: "ConsentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_SurgeryConsents_SurgeryId",
                table: "SurgeryConsents",
                column: "SurgeryId");

            migrationBuilder.CreateIndex(
                name: "IX_SurgeryEvents_SurgeryId_CreatedUtc",
                table: "SurgeryEvents",
                columns: new[] { "SurgeryId", "CreatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SurgerySpecimens_LabOrderId",
                table: "SurgerySpecimens",
                column: "LabOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_SurgerySpecimens_PatientId",
                table: "SurgerySpecimens",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_SurgerySpecimens_SurgeryId",
                table: "SurgerySpecimens",
                column: "SurgeryId");

            migrationBuilder.CreateIndex(
                name: "IX_SurgeryTeamMembers_DoctorId",
                table: "SurgeryTeamMembers",
                column: "DoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_SurgeryTeamMembers_SurgeryId",
                table: "SurgeryTeamMembers",
                column: "SurgeryId");

            migrationBuilder.CreateIndex(
                name: "IX_SurgeryTeamMembers_UserId",
                table: "SurgeryTeamMembers",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Charges_Surgeries_SurgeryId",
                table: "Charges",
                column: "SurgeryId",
                principalTable: "Surgeries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ClinicalRecords_Surgeries_SurgeryId",
                table: "ClinicalRecords",
                column: "SurgeryId",
                principalTable: "Surgeries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Charges_Surgeries_SurgeryId",
                table: "Charges");

            migrationBuilder.DropForeignKey(
                name: "FK_ClinicalRecords_Surgeries_SurgeryId",
                table: "ClinicalRecords");

            migrationBuilder.DropTable(
                name: "CssdBatchSets");

            migrationBuilder.DropTable(
                name: "CssdIssues");

            migrationBuilder.DropTable(
                name: "CssdSetItems");

            migrationBuilder.DropTable(
                name: "OperativeNotes");

            migrationBuilder.DropTable(
                name: "OtConsumptions");

            migrationBuilder.DropTable(
                name: "OtDocuments");

            migrationBuilder.DropTable(
                name: "OtDrugEntries");

            migrationBuilder.DropTable(
                name: "OtEquipmentLogs");

            migrationBuilder.DropTable(
                name: "OtSettings");

            migrationBuilder.DropTable(
                name: "RecoveryEpisodes");

            migrationBuilder.DropTable(
                name: "SurgeryCharges");

            migrationBuilder.DropTable(
                name: "SurgeryChecklistEntries");

            migrationBuilder.DropTable(
                name: "SurgeryChecklistStages");

            migrationBuilder.DropTable(
                name: "SurgeryEvents");

            migrationBuilder.DropTable(
                name: "SurgerySpecimens");

            migrationBuilder.DropTable(
                name: "SurgeryTeamMembers");

            migrationBuilder.DropTable(
                name: "CssdBatches");

            migrationBuilder.DropTable(
                name: "CssdInstruments");

            migrationBuilder.DropTable(
                name: "CssdSets");

            migrationBuilder.DropTable(
                name: "SurgeryConsents");

            migrationBuilder.DropTable(
                name: "OtEquipment");

            migrationBuilder.DropTable(
                name: "OtChecklistItems");

            migrationBuilder.DropTable(
                name: "OtConsentTypes");

            migrationBuilder.DropTable(
                name: "Surgeries");

            migrationBuilder.DropTable(
                name: "OtProcedures");

            migrationBuilder.DropTable(
                name: "OtTheatres");

            migrationBuilder.DropIndex(
                name: "IX_ClinicalRecords_SurgeryId_Kind",
                table: "ClinicalRecords");

            migrationBuilder.DropIndex(
                name: "IX_Charges_SurgeryId",
                table: "Charges");

            migrationBuilder.DropColumn(
                name: "SurgeryId",
                table: "ClinicalRecords");

            migrationBuilder.DropColumn(
                name: "SurgeryId",
                table: "Charges");

            migrationBuilder.DropSequence(
                name: "cssd_batch_seq");

            migrationBuilder.DropSequence(
                name: "surgery_seq");
        }
    }
}
