using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BurhanNexus.HMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Update18 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ConsultationAppointmentId",
                table: "Refunds",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "SettlementId",
                table: "CashierLedger",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "Appointments",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountOriginalFee",
                table: "Appointments",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscountReason",
                table: "Appointments",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "DiscountRefundId",
                table: "Appointments",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscountType",
                table: "Appointments",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountValue",
                table: "Appointments",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscountedByUsername",
                table: "Appointments",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DiscountedUtc",
                table: "Appointments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Diseases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUsername = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Diseases", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HospitalSettlements",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Username = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    OutstandingBefore = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    Notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: true),
                    CreatedByUsername = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HospitalSettlements", x => x.Id);
                    table.CheckConstraint("CK_HospitalSettlements_Positive", "\"Amount\" > 0");
                    table.ForeignKey(
                        name: "FK_HospitalSettlements_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PatientDiseases",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PatientId = table.Column<long>(type: "bigint", nullable: false),
                    DiseaseId = table.Column<int>(type: "integer", nullable: false),
                    EncounterId = table.Column<long>(type: "bigint", nullable: true),
                    AppointmentId = table.Column<long>(type: "bigint", nullable: true),
                    VisitDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AgeYears = table.Column<int>(type: "integer", nullable: true),
                    Gender = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: true),
                    CreatedByUsername = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientDiseases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatientDiseases_Appointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "Appointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PatientDiseases_Diseases_DiseaseId",
                        column: x => x.DiseaseId,
                        principalTable: "Diseases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PatientDiseases_PatientEncounters_EncounterId",
                        column: x => x.EncounterId,
                        principalTable: "PatientEncounters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PatientDiseases_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CashierLedger_SettlementId",
                table: "CashierLedger",
                column: "SettlementId");

            migrationBuilder.CreateIndex(
                name: "IX_Diseases_IsActive",
                table: "Diseases",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Diseases_NormalizedName",
                table: "Diseases",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HospitalSettlements_Number",
                table: "HospitalSettlements",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HospitalSettlements_UserId_BusinessDate",
                table: "HospitalSettlements",
                columns: new[] { "UserId", "BusinessDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PatientDiseases_AppointmentId",
                table: "PatientDiseases",
                column: "AppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientDiseases_DiseaseId_VisitDate",
                table: "PatientDiseases",
                columns: new[] { "DiseaseId", "VisitDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PatientDiseases_EncounterId",
                table: "PatientDiseases",
                column: "EncounterId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientDiseases_PatientId_VisitDate",
                table: "PatientDiseases",
                columns: new[] { "PatientId", "VisitDate" });

            migrationBuilder.AddForeignKey(
                name: "FK_CashierLedger_HospitalSettlements_SettlementId",
                table: "CashierLedger",
                column: "SettlementId",
                principalTable: "HospitalSettlements",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CashierLedger_HospitalSettlements_SettlementId",
                table: "CashierLedger");

            migrationBuilder.DropTable(
                name: "HospitalSettlements");

            migrationBuilder.DropTable(
                name: "PatientDiseases");

            migrationBuilder.DropTable(
                name: "Diseases");

            migrationBuilder.DropIndex(
                name: "IX_CashierLedger_SettlementId",
                table: "CashierLedger");

            migrationBuilder.DropColumn(
                name: "ConsultationAppointmentId",
                table: "Refunds");

            migrationBuilder.DropColumn(
                name: "SettlementId",
                table: "CashierLedger");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "DiscountOriginalFee",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "DiscountReason",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "DiscountRefundId",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "DiscountType",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "DiscountValue",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "DiscountedByUsername",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "DiscountedUtc",
                table: "Appointments");
        }
    }
}
