using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BurhanNexus.HMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase2AppointmentsThermal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Data-safe: only new columns/tables. Existing settings row receives the documented defaults.
            migrationBuilder.AddColumn<int>(
                name: "AppointmentMaxDaysAhead",
                table: "RegistrationSettings",
                type: "integer",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.AddColumn<string>(
                name: "AppointmentSlipContact",
                table: "RegistrationSettings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AppointmentSlipMessage",
                table: "RegistrationSettings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "Please wait for your turn.");

            migrationBuilder.AddColumn<bool>(
                name: "AppointmentTimeEnabled",
                table: "RegistrationSettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ShowFeeOnAppointmentSlip",
                table: "RegistrationSettings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "Appointments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PatientId = table.Column<long>(type: "bigint", nullable: false),
                    DoctorId = table.Column<int>(type: "integer", nullable: false),
                    EncounterId = table.Column<long>(type: "bigint", nullable: false),
                    AppointmentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AppointmentTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    TokenNumber = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Complaint = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ServiceItemId = table.Column<int>(type: "integer", nullable: true),
                    ConsultationFee = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    BookedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    BookedByUserId = table.Column<int>(type: "integer", nullable: true),
                    BookedByUsername = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Workstation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CancelledUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledByUserId = table.Column<int>(type: "integer", nullable: true),
                    CancelledByUsername = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CancelReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SeenUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Appointments", x => x.Id);
                    table.CheckConstraint("CK_Appointments_Token_Positive", "\"TokenNumber\" > 0");
                    table.ForeignKey(
                        name: "FK_Appointments_Doctors_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "Doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Appointments_PatientEncounters_EncounterId",
                        column: x => x.EncounterId,
                        principalTable: "PatientEncounters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Appointments_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Appointments_ServiceItems_ServiceItemId",
                        column: x => x.ServiceItemId,
                        principalTable: "ServiceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PrinterProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    WorkstationName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PaperWidthMm = table.Column<int>(type: "integer", nullable: false),
                    WindowsPrinterName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    LanHost = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LanPort = table.Column<int>(type: "integer", nullable: false),
                    AgentUrl = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Alignment = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    FontSize = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ShowLogo = table.Column<bool>(type: "boolean", nullable: false),
                    BoldText = table.Column<bool>(type: "boolean", nullable: false),
                    TokenSize = table.Column<int>(type: "integer", nullable: false),
                    AutoCut = table.Column<bool>(type: "boolean", nullable: false),
                    Copies = table.Column<int>(type: "integer", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedByUserId = table.Column<int>(type: "integer", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrinterProfiles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_AppointmentDate_DoctorId_Status",
                table: "Appointments",
                columns: new[] { "AppointmentDate", "DoctorId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_EncounterId",
                table: "Appointments",
                column: "EncounterId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_PatientId",
                table: "Appointments",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_ServiceItemId",
                table: "Appointments",
                column: "ServiceItemId");

            migrationBuilder.CreateIndex(
                name: "UX_Appointments_Doctor_Date_Token",
                table: "Appointments",
                columns: new[] { "DoctorId", "AppointmentDate", "TokenNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PrinterProfiles_WorkstationName",
                table: "PrinterProfiles",
                column: "WorkstationName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Appointments");

            migrationBuilder.DropTable(
                name: "PrinterProfiles");

            migrationBuilder.DropColumn(
                name: "AppointmentMaxDaysAhead",
                table: "RegistrationSettings");

            migrationBuilder.DropColumn(
                name: "AppointmentSlipContact",
                table: "RegistrationSettings");

            migrationBuilder.DropColumn(
                name: "AppointmentSlipMessage",
                table: "RegistrationSettings");

            migrationBuilder.DropColumn(
                name: "AppointmentTimeEnabled",
                table: "RegistrationSettings");

            migrationBuilder.DropColumn(
                name: "ShowFeeOnAppointmentSlip",
                table: "RegistrationSettings");
        }
    }
}
