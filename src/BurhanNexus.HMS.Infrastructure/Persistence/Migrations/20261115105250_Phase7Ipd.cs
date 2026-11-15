using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BurhanNexus.HMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase7Ipd : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "delivery_seq");

            migrationBuilder.CreateSequence(
                name: "ipd_admission_seq");

            migrationBuilder.CreateSequence(
                name: "pregnancy_seq");

            migrationBuilder.AddColumn<long>(
                name: "IpdAdmissionId",
                table: "RadiologyOrders",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "IpdAdmissionId",
                table: "PatientMedicineIssues",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "IpdAdmissionId",
                table: "LabOrders",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "IpdAdmissionId",
                table: "Charges",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ClinicalFormFields",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Key = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Section = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Choices = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    AlertLow = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    AlertHigh = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    AlertSource = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicalFormFields", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IpdSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    AdmissionServiceItemId = table.Column<int>(type: "integer", nullable: true),
                    AllowDirectAdmission = table.Column<bool>(type: "boolean", nullable: false),
                    RequireBillClearanceForDischarge = table.Column<bool>(type: "boolean", nullable: false),
                    PrintHeader = table.Column<bool>(type: "boolean", nullable: false),
                    LetterheadTopMarginMm = table.Column<int>(type: "integer", nullable: false),
                    FormFooter = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    RequireMarConfirmations = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IpdSettings", x => x.Id);
                    table.CheckConstraint("CK_IpdSettings_Singleton", "\"Id\" = 1");
                    table.ForeignKey(
                        name: "FK_IpdSettings_ServiceItems_AdmissionServiceItemId",
                        column: x => x.AdmissionServiceItemId,
                        principalTable: "ServiceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IpdWards",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DepartmentId = table.Column<int>(type: "integer", nullable: true),
                    Location = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Capacity = table.Column<int>(type: "integer", nullable: false),
                    ResponsibleStaff = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    BedChargeServiceItemId = table.Column<int>(type: "integer", nullable: true),
                    StockLocationId = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IpdWards", x => x.Id);
                    table.CheckConstraint("CK_IpdWards_Capacity", "\"Capacity\" >= 0");
                    table.ForeignKey(
                        name: "FK_IpdWards_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IpdWards_ServiceItems_BedChargeServiceItemId",
                        column: x => x.BedChargeServiceItemId,
                        principalTable: "ServiceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IpdWards_StockLocations_StockLocationId",
                        column: x => x.StockLocationId,
                        principalTable: "StockLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Pregnancies",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PatientId = table.Column<long>(type: "bigint", nullable: false),
                    RegisteredDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Lmp = table.Column<DateOnly>(type: "date", nullable: true),
                    Edd = table.Column<DateOnly>(type: "date", nullable: true),
                    Gravida = table.Column<int>(type: "integer", nullable: true),
                    Para = table.Column<int>(type: "integer", nullable: true),
                    Abortions = table.Column<int>(type: "integer", nullable: true),
                    LivingChildren = table.Column<int>(type: "integer", nullable: true),
                    PreviousComplications = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    MedicalHistory = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    RiskFactors = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsHighRisk = table.Column<bool>(type: "boolean", nullable: false),
                    HighRiskReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    HighRiskAssessment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    HighRiskReferral = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    FollowUpPlan = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StatusNote = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pregnancies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pregnancies_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IpdRooms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    WardId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BedCapacity = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IpdRooms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IpdRooms_IpdWards_WardId",
                        column: x => x.WardId,
                        principalTable: "IpdWards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClinicalRecords",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PatientId = table.Column<long>(type: "bigint", nullable: false),
                    AdmissionId = table.Column<long>(type: "bigint", nullable: true),
                    PregnancyId = table.Column<long>(type: "bigint", nullable: true),
                    RecordedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EnteredUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: true),
                    Username = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ResponsibleName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Values = table.Column<string>(type: "jsonb", nullable: false),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CorrectsRecordId = table.Column<long>(type: "bigint", nullable: true),
                    CorrectionReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicalRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClinicalRecords_ClinicalRecords_CorrectsRecordId",
                        column: x => x.CorrectsRecordId,
                        principalTable: "ClinicalRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClinicalRecords_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClinicalRecords_Pregnancies_PregnancyId",
                        column: x => x.PregnancyId,
                        principalTable: "Pregnancies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DeliveryRecords",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                    AdmissionId = table.Column<long>(type: "bigint", nullable: false),
                    MotherPatientId = table.Column<long>(type: "bigint", nullable: false),
                    PregnancyId = table.Column<long>(type: "bigint", nullable: true),
                    LabourRecordId = table.Column<long>(type: "bigint", nullable: true),
                    DeliveredUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeliveryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    TypeDetail = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Outcome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AttendingStaff = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    DeliveringDoctor = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Complications = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Placenta = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    EstimatedBloodLossMl = table.Column<int>(type: "integer", nullable: true),
                    Perineum = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    MaternalCondition = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ImmediateCare = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    BabyCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryRecords", x => x.Id);
                    table.CheckConstraint("CK_DeliveryRecords_Babies", "\"BabyCount\" >= 1");
                    table.ForeignKey(
                        name: "FK_DeliveryRecords_Patients_MotherPatientId",
                        column: x => x.MotherPatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DeliveryRecords_Pregnancies_PregnancyId",
                        column: x => x.PregnancyId,
                        principalTable: "Pregnancies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IpdAdmissions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AdmissionNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<long>(type: "bigint", nullable: false),
                    Kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Source = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SourceEncounterId = table.Column<long>(type: "bigint", nullable: true),
                    MotherAdmissionId = table.Column<long>(type: "bigint", nullable: true),
                    PregnancyId = table.Column<long>(type: "bigint", nullable: true),
                    DepartmentId = table.Column<int>(type: "integer", nullable: true),
                    ReferringDoctor = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    AdmittingDoctorId = table.Column<int>(type: "integer", nullable: true),
                    AdmittingDoctorName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    AdmissionDiagnosis = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    InitialCondition = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    MedicalHistory = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Allergies = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AttendantName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    AttendantRelation = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    AttendantPhone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    AdmittedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AdmissionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CurrentWardId = table.Column<int>(type: "integer", nullable: true),
                    CurrentBedId = table.Column<int>(type: "integer", nullable: true),
                    NeedsAttention = table.Column<bool>(type: "boolean", nullable: false),
                    AttentionNote = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: true),
                    CreatedByUsername = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    DischargeType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    FinalDiagnosis = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    TreatmentSummary = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Procedures = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    InvestigationSummary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ConditionAtDischarge = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    FollowUp = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DischargeMedications = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ReferredTo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DischargeDoctor = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    DischargeRequestedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DischargeRequestedBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    DischargedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DischargeDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DischargedBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CancelReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IpdAdmissions", x => x.Id);
                    table.CheckConstraint("CK_IpdAdmissions_Mother", "\"MotherAdmissionId\" IS NULL OR \"MotherAdmissionId\" <> \"Id\"");
                    table.ForeignKey(
                        name: "FK_IpdAdmissions_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IpdAdmissions_Doctors_AdmittingDoctorId",
                        column: x => x.AdmittingDoctorId,
                        principalTable: "Doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IpdAdmissions_IpdAdmissions_MotherAdmissionId",
                        column: x => x.MotherAdmissionId,
                        principalTable: "IpdAdmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IpdAdmissions_IpdWards_CurrentWardId",
                        column: x => x.CurrentWardId,
                        principalTable: "IpdWards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IpdAdmissions_PatientEncounters_SourceEncounterId",
                        column: x => x.SourceEncounterId,
                        principalTable: "PatientEncounters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IpdAdmissions_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IpdAdmissions_Pregnancies_PregnancyId",
                        column: x => x.PregnancyId,
                        principalTable: "Pregnancies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IpdBedCharges",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AdmissionId = table.Column<long>(type: "bigint", nullable: false),
                    Night = table.Column<DateOnly>(type: "date", nullable: false),
                    WardId = table.Column<int>(type: "integer", nullable: false),
                    ChargeId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IpdBedCharges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IpdBedCharges_Charges_ChargeId",
                        column: x => x.ChargeId,
                        principalTable: "Charges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IpdBedCharges_IpdAdmissions_AdmissionId",
                        column: x => x.AdmissionId,
                        principalTable: "IpdAdmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IpdBeds",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    RoomId = table.Column<int>(type: "integer", nullable: false),
                    WardId = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    EquipmentId = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    StatusNote = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CurrentAdmissionId = table.Column<long>(type: "bigint", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IpdBeds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IpdBeds_IpdAdmissions_CurrentAdmissionId",
                        column: x => x.CurrentAdmissionId,
                        principalTable: "IpdAdmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IpdBeds_IpdRooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "IpdRooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IpdBeds_IpdWards_WardId",
                        column: x => x.WardId,
                        principalTable: "IpdWards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IpdEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AdmissionId = table.Column<long>(type: "bigint", nullable: true),
                    PregnancyId = table.Column<long>(type: "bigint", nullable: true),
                    Category = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Event = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    FromValue = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    ToValue = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: true),
                    Username = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IpdEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IpdEvents_IpdAdmissions_AdmissionId",
                        column: x => x.AdmissionId,
                        principalTable: "IpdAdmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IpdEvents_Pregnancies_PregnancyId",
                        column: x => x.PregnancyId,
                        principalTable: "Pregnancies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IpdTransfers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AdmissionId = table.Column<long>(type: "bigint", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FromWardId = table.Column<int>(type: "integer", nullable: true),
                    FromBedId = table.Column<int>(type: "integer", nullable: true),
                    FromDepartmentId = table.Column<int>(type: "integer", nullable: true),
                    ToWardId = table.Column<int>(type: "integer", nullable: true),
                    ToBedId = table.Column<int>(type: "integer", nullable: true),
                    ToDepartmentId = table.Column<int>(type: "integer", nullable: true),
                    FromText = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ToText = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ReceivingDepartment = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    TransferUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Username = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IpdTransfers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IpdTransfers_IpdAdmissions_AdmissionId",
                        column: x => x.AdmissionId,
                        principalTable: "IpdAdmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LabourRecords",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AdmissionId = table.Column<long>(type: "bigint", nullable: false),
                    PregnancyId = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    GestationalAge = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    PresentingComplaints = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    OnsetUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OnsetInfo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    MembraneStatus = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    MembranesRupturedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClinicalHistory = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    InitialAssessment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ResponsibleStaff = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabourRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LabourRecords_IpdAdmissions_AdmissionId",
                        column: x => x.AdmissionId,
                        principalTable: "IpdAdmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LabourRecords_Pregnancies_PregnancyId",
                        column: x => x.PregnancyId,
                        principalTable: "Pregnancies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MedicationOrders",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AdmissionId = table.Column<long>(type: "bigint", nullable: false),
                    PatientId = table.Column<long>(type: "bigint", nullable: false),
                    MedicineId = table.Column<int>(type: "integer", nullable: false),
                    MedicineName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Dose = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Route = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Frequency = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Times = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DurationDays = table.Column<int>(type: "integer", nullable: true),
                    StartUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StopUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Prescriber = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Source = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Instructions = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    StockUnitsPerDose = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EnteredBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    StopReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    StoppedBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicationOrders", x => x.Id);
                    table.CheckConstraint("CK_MedicationOrders_Units", "\"StockUnitsPerDose\" >= 0");
                    table.ForeignKey(
                        name: "FK_MedicationOrders_IpdAdmissions_AdmissionId",
                        column: x => x.AdmissionId,
                        principalTable: "IpdAdmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MedicationOrders_Medicines_MedicineId",
                        column: x => x.MedicineId,
                        principalTable: "Medicines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NewbornRecords",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DeliveryId = table.Column<long>(type: "bigint", nullable: false),
                    BirthOrder = table.Column<int>(type: "integer", nullable: false),
                    MotherPatientId = table.Column<long>(type: "bigint", nullable: false),
                    PregnancyId = table.Column<long>(type: "bigint", nullable: true),
                    NewbornPatientId = table.Column<long>(type: "bigint", nullable: true),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Sex = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    BirthUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    BirthWeightGrams = table.Column<int>(type: "integer", nullable: true),
                    GestationalAge = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Apgar1 = table.Column<int>(type: "integer", nullable: true),
                    Apgar5 = table.Column<int>(type: "integer", nullable: true),
                    Apgar10 = table.Column<int>(type: "integer", nullable: true),
                    Condition = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Resuscitation = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CongenitalObservations = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Examination = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ResponsibleStaff = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Placement = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NewbornAdmissionId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NewbornRecords", x => x.Id);
                    table.CheckConstraint("CK_NewbornRecords_NotMother", "\"NewbornPatientId\" IS NULL OR \"NewbornPatientId\" <> \"MotherPatientId\"");
                    table.ForeignKey(
                        name: "FK_NewbornRecords_DeliveryRecords_DeliveryId",
                        column: x => x.DeliveryId,
                        principalTable: "DeliveryRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NewbornRecords_IpdAdmissions_NewbornAdmissionId",
                        column: x => x.NewbornAdmissionId,
                        principalTable: "IpdAdmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NewbornRecords_Patients_MotherPatientId",
                        column: x => x.MotherPatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NewbornRecords_Patients_NewbornPatientId",
                        column: x => x.NewbornPatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NewbornRecords_Pregnancies_PregnancyId",
                        column: x => x.PregnancyId,
                        principalTable: "Pregnancies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NursingTasks",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AdmissionId = table.Column<long>(type: "bigint", nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    DueUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CompletedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Note = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NursingTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NursingTasks_IpdAdmissions_AdmissionId",
                        column: x => x.AdmissionId,
                        principalTable: "IpdAdmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ShiftHandovers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AdmissionId = table.Column<long>(type: "bigint", nullable: false),
                    WardId = table.Column<int>(type: "integer", nullable: true),
                    Shift = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    OutgoingUserId = table.Column<int>(type: "integer", nullable: true),
                    OutgoingUsername = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PatientCondition = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Diagnosis = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PendingInvestigations = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    MedicationDue = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IvFluids = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ImportantObservations = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    PendingProcedures = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SpecialCare = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OutstandingTasks = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AcknowledgedByUserId = table.Column<int>(type: "integer", nullable: true),
                    AcknowledgedByUsername = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    AcknowledgedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AcknowledgeNote = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShiftHandovers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShiftHandovers_IpdAdmissions_AdmissionId",
                        column: x => x.AdmissionId,
                        principalTable: "IpdAdmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IpdBedAllocations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AdmissionId = table.Column<long>(type: "bigint", nullable: false),
                    BedId = table.Column<int>(type: "integer", nullable: false),
                    WardId = table.Column<int>(type: "integer", nullable: false),
                    FromUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ToUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsShared = table.Column<bool>(type: "boolean", nullable: false),
                    SharedWithAdmissionId = table.Column<long>(type: "bigint", nullable: true),
                    Reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AllocatedBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ReleasedBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IpdBedAllocations", x => x.Id);
                    table.CheckConstraint("CK_IpdBedAllocations_Period", "\"ToUtc\" IS NULL OR \"ToUtc\" >= \"FromUtc\"");
                    table.ForeignKey(
                        name: "FK_IpdBedAllocations_IpdAdmissions_AdmissionId",
                        column: x => x.AdmissionId,
                        principalTable: "IpdAdmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IpdBedAllocations_IpdAdmissions_SharedWithAdmissionId",
                        column: x => x.SharedWithAdmissionId,
                        principalTable: "IpdAdmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IpdBedAllocations_IpdBeds_BedId",
                        column: x => x.BedId,
                        principalTable: "IpdBeds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IpdBedAllocations_IpdWards_WardId",
                        column: x => x.WardId,
                        principalTable: "IpdWards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IpdBedStatusChanges",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BedId = table.Column<int>(type: "integer", nullable: false),
                    From = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    To = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Username = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IpdBedStatusChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IpdBedStatusChanges_IpdBeds_BedId",
                        column: x => x.BedId,
                        principalTable: "IpdBeds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MedicationDoses",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrderId = table.Column<long>(type: "bigint", nullable: false),
                    AdmissionId = table.Column<long>(type: "bigint", nullable: false),
                    ScheduledUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    GivenUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DoseGiven = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    RouteGiven = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Nurse = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Remarks = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    RecordedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RecordedBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    StockTransactionId = table.Column<long>(type: "bigint", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicationDoses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MedicationDoses_InventoryTransactions_StockTransactionId",
                        column: x => x.StockTransactionId,
                        principalTable: "InventoryTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MedicationDoses_MedicationOrders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "MedicationOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NicuDetails",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AdmissionId = table.Column<long>(type: "bigint", nullable: false),
                    NewbornRecordId = table.Column<long>(type: "bigint", nullable: true),
                    MotherPatientId = table.Column<long>(type: "bigint", nullable: true),
                    GestationalAge = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    BirthWeightGrams = table.Column<int>(type: "integer", nullable: true),
                    AdmissionWeightGrams = table.Column<int>(type: "integer", nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ClinicalCondition = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    BirthHistory = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ReferralDetails = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ResponsibleClinician = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NicuDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NicuDetails_IpdAdmissions_AdmissionId",
                        column: x => x.AdmissionId,
                        principalTable: "IpdAdmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NicuDetails_NewbornRecords_NewbornRecordId",
                        column: x => x.NewbornRecordId,
                        principalTable: "NewbornRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RadiologyOrders_IpdAdmissionId",
                table: "RadiologyOrders",
                column: "IpdAdmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientMedicineIssues_IpdAdmissionId",
                table: "PatientMedicineIssues",
                column: "IpdAdmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_LabOrders_IpdAdmissionId",
                table: "LabOrders",
                column: "IpdAdmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_Charges_IpdAdmissionId",
                table: "Charges",
                column: "IpdAdmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalFormFields_Kind_Key",
                table: "ClinicalFormFields",
                columns: new[] { "Kind", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalRecords_AdmissionId_Kind_RecordedUtc",
                table: "ClinicalRecords",
                columns: new[] { "AdmissionId", "Kind", "RecordedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalRecords_CorrectsRecordId",
                table: "ClinicalRecords",
                column: "CorrectsRecordId",
                unique: true,
                filter: "\"CorrectsRecordId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalRecords_PatientId_Kind",
                table: "ClinicalRecords",
                columns: new[] { "PatientId", "Kind" });

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalRecords_PregnancyId",
                table: "ClinicalRecords",
                column: "PregnancyId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryRecords_AdmissionId",
                table: "DeliveryRecords",
                column: "AdmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryRecords_DeliveryDate",
                table: "DeliveryRecords",
                column: "DeliveryDate");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryRecords_LabourRecordId",
                table: "DeliveryRecords",
                column: "LabourRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryRecords_MotherPatientId",
                table: "DeliveryRecords",
                column: "MotherPatientId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryRecords_Number",
                table: "DeliveryRecords",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryRecords_PregnancyId",
                table: "DeliveryRecords",
                column: "PregnancyId");

            migrationBuilder.CreateIndex(
                name: "UX_DeliveryRecords_Idempotency",
                table: "DeliveryRecords",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IpdAdmissions_AdmissionDate",
                table: "IpdAdmissions",
                column: "AdmissionDate");

            migrationBuilder.CreateIndex(
                name: "IX_IpdAdmissions_AdmissionNumber",
                table: "IpdAdmissions",
                column: "AdmissionNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IpdAdmissions_AdmittingDoctorId",
                table: "IpdAdmissions",
                column: "AdmittingDoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_IpdAdmissions_CurrentBedId",
                table: "IpdAdmissions",
                column: "CurrentBedId");

            migrationBuilder.CreateIndex(
                name: "IX_IpdAdmissions_CurrentWardId",
                table: "IpdAdmissions",
                column: "CurrentWardId");

            migrationBuilder.CreateIndex(
                name: "IX_IpdAdmissions_DepartmentId",
                table: "IpdAdmissions",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_IpdAdmissions_MotherAdmissionId",
                table: "IpdAdmissions",
                column: "MotherAdmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_IpdAdmissions_PatientId_Status",
                table: "IpdAdmissions",
                columns: new[] { "PatientId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_IpdAdmissions_PregnancyId",
                table: "IpdAdmissions",
                column: "PregnancyId");

            migrationBuilder.CreateIndex(
                name: "IX_IpdAdmissions_SourceEncounterId",
                table: "IpdAdmissions",
                column: "SourceEncounterId");

            migrationBuilder.CreateIndex(
                name: "IX_IpdAdmissions_Status_Kind",
                table: "IpdAdmissions",
                columns: new[] { "Status", "Kind" });

            migrationBuilder.CreateIndex(
                name: "UX_IpdAdmissions_Idempotency",
                table: "IpdAdmissions",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_IpdAdmissions_OneActive",
                table: "IpdAdmissions",
                column: "PatientId",
                unique: true,
                filter: "\"Status\" IN ('Registered','Admitted','UnderTreatment','Transferred','DischargeRequested')");

            migrationBuilder.CreateIndex(
                name: "IX_IpdBedAllocations_BedId_FromUtc",
                table: "IpdBedAllocations",
                columns: new[] { "BedId", "FromUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_IpdBedAllocations_SharedWithAdmissionId",
                table: "IpdBedAllocations",
                column: "SharedWithAdmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_IpdBedAllocations_WardId",
                table: "IpdBedAllocations",
                column: "WardId");

            migrationBuilder.CreateIndex(
                name: "UX_IpdBedAllocations_OpenAdmission",
                table: "IpdBedAllocations",
                column: "AdmissionId",
                unique: true,
                filter: "\"ToUtc\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "UX_IpdBedAllocations_OpenBed",
                table: "IpdBedAllocations",
                column: "BedId",
                unique: true,
                filter: "\"ToUtc\" IS NULL AND NOT \"IsShared\"");

            migrationBuilder.CreateIndex(
                name: "IX_IpdBedCharges_AdmissionId_Night",
                table: "IpdBedCharges",
                columns: new[] { "AdmissionId", "Night" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IpdBedCharges_ChargeId",
                table: "IpdBedCharges",
                column: "ChargeId");

            migrationBuilder.CreateIndex(
                name: "IX_IpdBeds_Code",
                table: "IpdBeds",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IpdBeds_CurrentAdmissionId",
                table: "IpdBeds",
                column: "CurrentAdmissionId",
                unique: true,
                filter: "\"CurrentAdmissionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_IpdBeds_RoomId",
                table: "IpdBeds",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_IpdBeds_WardId",
                table: "IpdBeds",
                column: "WardId");

            migrationBuilder.CreateIndex(
                name: "IX_IpdBedStatusChanges_BedId",
                table: "IpdBedStatusChanges",
                column: "BedId");

            migrationBuilder.CreateIndex(
                name: "IX_IpdEvents_AdmissionId",
                table: "IpdEvents",
                column: "AdmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_IpdEvents_Category_CreatedUtc",
                table: "IpdEvents",
                columns: new[] { "Category", "CreatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_IpdEvents_PregnancyId",
                table: "IpdEvents",
                column: "PregnancyId");

            migrationBuilder.CreateIndex(
                name: "IX_IpdRooms_WardId_Name",
                table: "IpdRooms",
                columns: new[] { "WardId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IpdSettings_AdmissionServiceItemId",
                table: "IpdSettings",
                column: "AdmissionServiceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_IpdTransfers_AdmissionId",
                table: "IpdTransfers",
                column: "AdmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_IpdTransfers_TransferUtc",
                table: "IpdTransfers",
                column: "TransferUtc");

            migrationBuilder.CreateIndex(
                name: "IX_IpdWards_BedChargeServiceItemId",
                table: "IpdWards",
                column: "BedChargeServiceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_IpdWards_Code",
                table: "IpdWards",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IpdWards_DepartmentId",
                table: "IpdWards",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_IpdWards_StockLocationId",
                table: "IpdWards",
                column: "StockLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_LabourRecords_AdmissionId",
                table: "LabourRecords",
                column: "AdmissionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LabourRecords_PregnancyId",
                table: "LabourRecords",
                column: "PregnancyId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicationDoses_AdmissionId_ScheduledUtc",
                table: "MedicationDoses",
                columns: new[] { "AdmissionId", "ScheduledUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MedicationDoses_OrderId_ScheduledUtc",
                table: "MedicationDoses",
                columns: new[] { "OrderId", "ScheduledUtc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MedicationDoses_StockTransactionId",
                table: "MedicationDoses",
                column: "StockTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicationOrders_AdmissionId",
                table: "MedicationOrders",
                column: "AdmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicationOrders_MedicineId",
                table: "MedicationOrders",
                column: "MedicineId");

            migrationBuilder.CreateIndex(
                name: "IX_NewbornRecords_DeliveryId_BirthOrder",
                table: "NewbornRecords",
                columns: new[] { "DeliveryId", "BirthOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NewbornRecords_MotherPatientId",
                table: "NewbornRecords",
                column: "MotherPatientId");

            migrationBuilder.CreateIndex(
                name: "IX_NewbornRecords_NewbornAdmissionId",
                table: "NewbornRecords",
                column: "NewbornAdmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_NewbornRecords_NewbornPatientId",
                table: "NewbornRecords",
                column: "NewbornPatientId",
                unique: true,
                filter: "\"NewbornPatientId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_NewbornRecords_PregnancyId",
                table: "NewbornRecords",
                column: "PregnancyId");

            migrationBuilder.CreateIndex(
                name: "IX_NicuDetails_AdmissionId",
                table: "NicuDetails",
                column: "AdmissionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NicuDetails_NewbornRecordId",
                table: "NicuDetails",
                column: "NewbornRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_NursingTasks_AdmissionId_Status",
                table: "NursingTasks",
                columns: new[] { "AdmissionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Pregnancies_Number",
                table: "Pregnancies",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Pregnancies_OneActive",
                table: "Pregnancies",
                column: "PatientId",
                unique: true,
                filter: "\"Status\" = 'Active'");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftHandovers_AdmissionId",
                table: "ShiftHandovers",
                column: "AdmissionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Charges_IpdAdmissions_IpdAdmissionId",
                table: "Charges",
                column: "IpdAdmissionId",
                principalTable: "IpdAdmissions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LabOrders_IpdAdmissions_IpdAdmissionId",
                table: "LabOrders",
                column: "IpdAdmissionId",
                principalTable: "IpdAdmissions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PatientMedicineIssues_IpdAdmissions_IpdAdmissionId",
                table: "PatientMedicineIssues",
                column: "IpdAdmissionId",
                principalTable: "IpdAdmissions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RadiologyOrders_IpdAdmissions_IpdAdmissionId",
                table: "RadiologyOrders",
                column: "IpdAdmissionId",
                principalTable: "IpdAdmissions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ClinicalRecords_IpdAdmissions_AdmissionId",
                table: "ClinicalRecords",
                column: "AdmissionId",
                principalTable: "IpdAdmissions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DeliveryRecords_IpdAdmissions_AdmissionId",
                table: "DeliveryRecords",
                column: "AdmissionId",
                principalTable: "IpdAdmissions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DeliveryRecords_LabourRecords_LabourRecordId",
                table: "DeliveryRecords",
                column: "LabourRecordId",
                principalTable: "LabourRecords",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_IpdAdmissions_IpdBeds_CurrentBedId",
                table: "IpdAdmissions",
                column: "CurrentBedId",
                principalTable: "IpdBeds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Charges_IpdAdmissions_IpdAdmissionId",
                table: "Charges");

            migrationBuilder.DropForeignKey(
                name: "FK_LabOrders_IpdAdmissions_IpdAdmissionId",
                table: "LabOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_PatientMedicineIssues_IpdAdmissions_IpdAdmissionId",
                table: "PatientMedicineIssues");

            migrationBuilder.DropForeignKey(
                name: "FK_RadiologyOrders_IpdAdmissions_IpdAdmissionId",
                table: "RadiologyOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_IpdBeds_IpdAdmissions_CurrentAdmissionId",
                table: "IpdBeds");

            migrationBuilder.DropTable(
                name: "ClinicalFormFields");

            migrationBuilder.DropTable(
                name: "ClinicalRecords");

            migrationBuilder.DropTable(
                name: "IpdBedAllocations");

            migrationBuilder.DropTable(
                name: "IpdBedCharges");

            migrationBuilder.DropTable(
                name: "IpdBedStatusChanges");

            migrationBuilder.DropTable(
                name: "IpdEvents");

            migrationBuilder.DropTable(
                name: "IpdSettings");

            migrationBuilder.DropTable(
                name: "IpdTransfers");

            migrationBuilder.DropTable(
                name: "MedicationDoses");

            migrationBuilder.DropTable(
                name: "NicuDetails");

            migrationBuilder.DropTable(
                name: "NursingTasks");

            migrationBuilder.DropTable(
                name: "ShiftHandovers");

            migrationBuilder.DropTable(
                name: "MedicationOrders");

            migrationBuilder.DropTable(
                name: "NewbornRecords");

            migrationBuilder.DropTable(
                name: "DeliveryRecords");

            migrationBuilder.DropTable(
                name: "LabourRecords");

            migrationBuilder.DropTable(
                name: "IpdAdmissions");

            migrationBuilder.DropTable(
                name: "IpdBeds");

            migrationBuilder.DropTable(
                name: "Pregnancies");

            migrationBuilder.DropTable(
                name: "IpdRooms");

            migrationBuilder.DropTable(
                name: "IpdWards");

            migrationBuilder.DropIndex(
                name: "IX_RadiologyOrders_IpdAdmissionId",
                table: "RadiologyOrders");

            migrationBuilder.DropIndex(
                name: "IX_PatientMedicineIssues_IpdAdmissionId",
                table: "PatientMedicineIssues");

            migrationBuilder.DropIndex(
                name: "IX_LabOrders_IpdAdmissionId",
                table: "LabOrders");

            migrationBuilder.DropIndex(
                name: "IX_Charges_IpdAdmissionId",
                table: "Charges");

            migrationBuilder.DropColumn(
                name: "IpdAdmissionId",
                table: "RadiologyOrders");

            migrationBuilder.DropColumn(
                name: "IpdAdmissionId",
                table: "PatientMedicineIssues");

            migrationBuilder.DropColumn(
                name: "IpdAdmissionId",
                table: "LabOrders");

            migrationBuilder.DropColumn(
                name: "IpdAdmissionId",
                table: "Charges");

            migrationBuilder.DropSequence(
                name: "delivery_seq");

            migrationBuilder.DropSequence(
                name: "ipd_admission_seq");

            migrationBuilder.DropSequence(
                name: "pregnancy_seq");
        }
    }
}
