using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BurhanNexus.HMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase2PatientsOpdEmergency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.CreateSequence(
                name: "emergency_number_seq");

            migrationBuilder.CreateSequence(
                name: "opd_number_seq");

            migrationBuilder.CreateSequence(
                name: "patient_number_seq");

            migrationBuilder.CreateTable(
                name: "Doctors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Qualification = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Specialty = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RegistrationNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DepartmentId = table.Column<int>(type: "integer", nullable: true),
                    UserId = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Doctors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Doctors_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmergencyCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmergencyCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Patients",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PatientNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SequenceNumber = table.Column<long>(type: "bigint", nullable: false),
                    FullName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    GuardianRelation = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: true),
                    GuardianName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Gender = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: true),
                    DateOfBirthEstimated = table.Column<bool>(type: "boolean", nullable: false),
                    AgeAtRegistration = table.Column<int>(type: "integer", nullable: true),
                    AgeUnitAtRegistration = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Cnic = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    CnicDigits = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: true),
                    MobileNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    MobileDigits = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    AlternateContact = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CityOrVillage = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BloodGroup = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsUnidentified = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ArchivedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StatusReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    MergedIntoPatientId = table.Column<long>(type: "bigint", nullable: true),
                    RegisteredUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RegisteredByUserId = table.Column<int>(type: "integer", nullable: true),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedByUserId = table.Column<int>(type: "integer", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Patients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Patients_Patients_MergedIntoPatientId",
                        column: x => x.MergedIntoPatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RegistrationSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    PatientNumberPrefix = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    PatientNumberDigits = table.Column<int>(type: "integer", nullable: false),
                    OpdNumberPrefix = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    EmergencyNumberPrefix = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    EncounterNumberIncludesYear = table.Column<bool>(type: "boolean", nullable: false),
                    EncounterNumberDigits = table.Column<int>(type: "integer", nullable: false),
                    OpdTokenEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    OpdTokenScope = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DefaultOpdDepartmentId = table.Column<int>(type: "integer", nullable: true),
                    RequireDoctorForPrivateOpd = table.Column<bool>(type: "boolean", nullable: false),
                    OpdSlipPaper = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    EmergencySlipPaper = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    AutoPrintAfterRegistration = table.Column<bool>(type: "boolean", nullable: false),
                    DefaultOpdServiceItemId = table.Column<int>(type: "integer", nullable: true),
                    DefaultEmergencyServiceItemId = table.Column<int>(type: "integer", nullable: true),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistrationSettings", x => x.Id);
                    table.CheckConstraint("CK_RegistrationSettings_Singleton", "\"Id\" = 1");
                });

            migrationBuilder.CreateTable(
                name: "ServiceItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Category = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DefaultFee = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    DoctorId = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceItems_Doctors_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "Doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PatientIdentifiers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PatientId = table.Column<long>(type: "bigint", nullable: false),
                    Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Value = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Note = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientIdentifiers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatientIdentifiers_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PatientEncounters",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EncounterNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SequenceNumber = table.Column<long>(type: "bigint", nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PatientId = table.Column<long>(type: "bigint", nullable: false),
                    OriginalPatientId = table.Column<long>(type: "bigint", nullable: true),
                    EncounterUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EncounterDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DepartmentId = table.Column<int>(type: "integer", nullable: true),
                    DoctorId = table.Column<int>(type: "integer", nullable: true),
                    TokenNumber = table.Column<int>(type: "integer", nullable: true),
                    TokenScope = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    HospitalTypeAtVisit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ServiceItemId = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: true),
                    CreatedByUsername = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Workstation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientEncounters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatientEncounters_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PatientEncounters_Doctors_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "Doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PatientEncounters_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PatientEncounters_ServiceItems_ServiceItemId",
                        column: x => x.ServiceItemId,
                        principalTable: "ServiceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmergencyVisits",
                columns: table => new
                {
                    EncounterId = table.Column<long>(type: "bigint", nullable: false),
                    CategoryId = table.Column<int>(type: "integer", nullable: true),
                    BriefComplaint = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    BroughtBy = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    AttendantName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    AttendantContact = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ReferredBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsMedicoLegal = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StatusChangedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmergencyVisits", x => x.EncounterId);
                    table.ForeignKey(
                        name: "FK_EmergencyVisits_EmergencyCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "EmergencyCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmergencyVisits_PatientEncounters_EncounterId",
                        column: x => x.EncounterId,
                        principalTable: "PatientEncounters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OpdVisits",
                columns: table => new
                {
                    EncounterId = table.Column<long>(type: "bigint", nullable: false),
                    IsRepeatVisit = table.Column<bool>(type: "boolean", nullable: false),
                    ChiefComplaint = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpdVisits", x => x.EncounterId);
                    table.ForeignKey(
                        name: "FK_OpdVisits_PatientEncounters_EncounterId",
                        column: x => x.EncounterId,
                        principalTable: "PatientEncounters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PrintedDocuments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EncounterId = table.Column<long>(type: "bigint", nullable: false),
                    DocumentType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PaperSize = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    PrintNumber = table.Column<int>(type: "integer", nullable: false),
                    PrintedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PrintedByUserId = table.Column<int>(type: "integer", nullable: true),
                    PrintedByUsername = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Workstation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrintedDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrintedDocuments_PatientEncounters_EncounterId",
                        column: x => x.EncounterId,
                        principalTable: "PatientEncounters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmergencyStatusHistory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EncounterId = table.Column<long>(type: "bigint", nullable: false),
                    FromStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ToStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ChangedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ChangedByUserId = table.Column<int>(type: "integer", nullable: true),
                    ChangedByUsername = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmergencyStatusHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmergencyStatusHistory_EmergencyVisits_EncounterId",
                        column: x => x.EncounterId,
                        principalTable: "EmergencyVisits",
                        principalColumn: "EncounterId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Doctors_DepartmentId",
                table: "Doctors",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Doctors_Name",
                table: "Doctors",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_EmergencyCategories_Name",
                table: "EmergencyCategories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmergencyStatusHistory_EncounterId_ChangedUtc",
                table: "EmergencyStatusHistory",
                columns: new[] { "EncounterId", "ChangedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_EmergencyVisits_CategoryId",
                table: "EmergencyVisits",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_EmergencyVisits_Status",
                table: "EmergencyVisits",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PatientEncounters_DepartmentId",
                table: "PatientEncounters",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientEncounters_DoctorId",
                table: "PatientEncounters",
                column: "DoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientEncounters_EncounterNumber",
                table: "PatientEncounters",
                column: "EncounterNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PatientEncounters_PatientId_EncounterUtc",
                table: "PatientEncounters",
                columns: new[] { "PatientId", "EncounterUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PatientEncounters_ServiceItemId",
                table: "PatientEncounters",
                column: "ServiceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientEncounters_TokenScope_EncounterDate_TokenNumber",
                table: "PatientEncounters",
                columns: new[] { "TokenScope", "EncounterDate", "TokenNumber" },
                unique: true,
                filter: "\"TokenNumber\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PatientEncounters_Type_EncounterDate",
                table: "PatientEncounters",
                columns: new[] { "Type", "EncounterDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PatientEncounters_Type_SequenceNumber",
                table: "PatientEncounters",
                columns: new[] { "Type", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PatientIdentifiers_PatientId",
                table: "PatientIdentifiers",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientIdentifiers_Type_Value",
                table: "PatientIdentifiers",
                columns: new[] { "Type", "Value" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PatientIdentifiers_Value",
                table: "PatientIdentifiers",
                column: "Value");

            migrationBuilder.CreateIndex(
                name: "IX_Patients_CnicDigits",
                table: "Patients",
                column: "CnicDigits");

            migrationBuilder.CreateIndex(
                name: "IX_Patients_FullName_trgm",
                table: "Patients",
                column: "FullName")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_Patients_GuardianName_trgm",
                table: "Patients",
                column: "GuardianName")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_Patients_MergedIntoPatientId",
                table: "Patients",
                column: "MergedIntoPatientId");

            migrationBuilder.CreateIndex(
                name: "IX_Patients_MobileDigits",
                table: "Patients",
                column: "MobileDigits");

            migrationBuilder.CreateIndex(
                name: "IX_Patients_PatientNumber",
                table: "Patients",
                column: "PatientNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Patients_RegisteredUtc",
                table: "Patients",
                column: "RegisteredUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Patients_SequenceNumber",
                table: "Patients",
                column: "SequenceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Patients_Status",
                table: "Patients",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PrintedDocuments_EncounterId_PrintedUtc",
                table: "PrintedDocuments",
                columns: new[] { "EncounterId", "PrintedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceItems_Code",
                table: "ServiceItems",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceItems_DoctorId",
                table: "ServiceItems",
                column: "DoctorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmergencyStatusHistory");

            migrationBuilder.DropTable(
                name: "OpdVisits");

            migrationBuilder.DropTable(
                name: "PatientIdentifiers");

            migrationBuilder.DropTable(
                name: "PrintedDocuments");

            migrationBuilder.DropTable(
                name: "RegistrationSettings");

            migrationBuilder.DropTable(
                name: "EmergencyVisits");

            migrationBuilder.DropTable(
                name: "EmergencyCategories");

            migrationBuilder.DropTable(
                name: "PatientEncounters");

            migrationBuilder.DropTable(
                name: "Patients");

            migrationBuilder.DropTable(
                name: "ServiceItems");

            migrationBuilder.DropTable(
                name: "Doctors");

            migrationBuilder.DropSequence(
                name: "emergency_number_seq");

            migrationBuilder.DropSequence(
                name: "opd_number_seq");

            migrationBuilder.DropSequence(
                name: "patient_number_seq");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:pg_trgm", ",,");
        }
    }
}
