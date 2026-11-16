using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BurhanNexus.HMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Update10Ipd : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "patient_advance_seq");

            migrationBuilder.AddColumn<TimeOnly>(
                name: "BedChargeCutoff",
                table: "IpdSettings",
                type: "time without time zone",
                nullable: false,
                defaultValue: new TimeOnly(12, 0, 0));

            migrationBuilder.AddColumn<string>(
                name: "BedChargePolicy",
                table: "IpdSettings",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "PerNight");

            migrationBuilder.AddColumn<int>(
                name: "HalfDayHours",
                table: "IpdSettings",
                type: "integer",
                nullable: false,
                defaultValue: 6);

            migrationBuilder.AddColumn<decimal>(
                name: "Quantity",
                table: "IpdBedCharges",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 1m);

            migrationBuilder.AddColumn<long>(
                name: "AdmissionFeeChargeId",
                table: "IpdAdmissions",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Quantity",
                table: "Charges",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: false,
                defaultValue: 1m);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitPrice",
                table: "Charges",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "AdvanceId",
                table: "CashierLedger",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DischargeClearanceOverrides",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AdmissionId = table.Column<long>(type: "bigint", nullable: false),
                    Outstanding = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: true),
                    Username = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DischargeClearanceOverrides", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DischargeClearanceOverrides_IpdAdmissions_AdmissionId",
                        column: x => x.AdmissionId,
                        principalTable: "IpdAdmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NicuEquipment",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ServiceItemId = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NicuEquipment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NicuEquipment_ServiceItems_ServiceItemId",
                        column: x => x.ServiceItemId,
                        principalTable: "ServiceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PatientAdvances",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<long>(type: "bigint", nullable: false),
                    AdmissionId = table.Column<long>(type: "bigint", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    AppliedAmount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    RefundedAmount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    PaymentMethodId = table.Column<int>(type: "integer", nullable: false),
                    CounterId = table.Column<int>(type: "integer", nullable: false),
                    CashierUserId = table.Column<int>(type: "integer", nullable: false),
                    CashierUsername = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientAdvances", x => x.Id);
                    table.CheckConstraint("CK_PatientAdvances_Balance", "\"Amount\" > 0 AND \"AppliedAmount\" >= 0 AND \"RefundedAmount\" >= 0 AND \"AppliedAmount\" + \"RefundedAmount\" <= \"Amount\"");
                    table.ForeignKey(
                        name: "FK_PatientAdvances_Counters_CounterId",
                        column: x => x.CounterId,
                        principalTable: "Counters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PatientAdvances_IpdAdmissions_AdmissionId",
                        column: x => x.AdmissionId,
                        principalTable: "IpdAdmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PatientAdvances_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PatientAdvances_PaymentMethods_PaymentMethodId",
                        column: x => x.PaymentMethodId,
                        principalTable: "PaymentMethods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ServicePackages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ServiceItemId = table.Column<int>(type: "integer", nullable: false),
                    Excluded = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServicePackages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServicePackages_ServiceItems_ServiceItemId",
                        column: x => x.ServiceItemId,
                        principalTable: "ServiceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EquipmentUsages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                    AdmissionId = table.Column<long>(type: "bigint", nullable: false),
                    PatientId = table.Column<long>(type: "bigint", nullable: false),
                    EquipmentId = table.Column<int>(type: "integer", nullable: false),
                    EquipmentName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Identifier = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    StartUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ChargeUnit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    ChargeId = table.Column<long>(type: "bigint", nullable: true),
                    CoveredByAdmissionPackageId = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    EndReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    StartedByUserId = table.Column<int>(type: "integer", nullable: true),
                    StartedByUsername = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    EndedByUsername = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentUsages", x => x.Id);
                    table.CheckConstraint("CK_EquipmentUsages_Period", "\"EndUtc\" IS NULL OR \"EndUtc\" >= \"StartUtc\"");
                    table.ForeignKey(
                        name: "FK_EquipmentUsages_Charges_ChargeId",
                        column: x => x.ChargeId,
                        principalTable: "Charges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EquipmentUsages_IpdAdmissions_AdmissionId",
                        column: x => x.AdmissionId,
                        principalTable: "IpdAdmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EquipmentUsages_NicuEquipment_EquipmentId",
                        column: x => x.EquipmentId,
                        principalTable: "NicuEquipment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PatientAdvanceEntries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AdvanceId = table.Column<long>(type: "bigint", nullable: false),
                    EntryType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    ReceiptId = table.Column<long>(type: "bigint", nullable: true),
                    Reference = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Note = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: true),
                    Username = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientAdvanceEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatientAdvanceEntries_PatientAdvances_AdvanceId",
                        column: x => x.AdvanceId,
                        principalTable: "PatientAdvances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AdmissionPackages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AdmissionId = table.Column<long>(type: "bigint", nullable: false),
                    PackageId = table.Column<int>(type: "integer", nullable: false),
                    ChargeId = table.Column<long>(type: "bigint", nullable: true),
                    AppliedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AppliedBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    RemovedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RemovedBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    RemovedReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdmissionPackages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdmissionPackages_Charges_ChargeId",
                        column: x => x.ChargeId,
                        principalTable: "Charges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AdmissionPackages_IpdAdmissions_AdmissionId",
                        column: x => x.AdmissionId,
                        principalTable: "IpdAdmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AdmissionPackages_ServicePackages_PackageId",
                        column: x => x.PackageId,
                        principalTable: "ServicePackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ServicePackageItems",
                columns: table => new
                {
                    PackageId = table.Column<int>(type: "integer", nullable: false),
                    ServiceItemId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServicePackageItems", x => new { x.PackageId, x.ServiceItemId });
                    table.ForeignKey(
                        name: "FK_ServicePackageItems_ServiceItems_ServiceItemId",
                        column: x => x.ServiceItemId,
                        principalTable: "ServiceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ServicePackageItems_ServicePackages_PackageId",
                        column: x => x.PackageId,
                        principalTable: "ServicePackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdmissionPackages_ChargeId",
                table: "AdmissionPackages",
                column: "ChargeId");

            migrationBuilder.CreateIndex(
                name: "IX_AdmissionPackages_PackageId",
                table: "AdmissionPackages",
                column: "PackageId");

            migrationBuilder.CreateIndex(
                name: "UX_AdmissionPackages_Active",
                table: "AdmissionPackages",
                columns: new[] { "AdmissionId", "PackageId" },
                unique: true,
                filter: "\"IsActive\"");

            migrationBuilder.CreateIndex(
                name: "IX_DischargeClearanceOverrides_AdmissionId",
                table: "DischargeClearanceOverrides",
                column: "AdmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentUsages_AdmissionId",
                table: "EquipmentUsages",
                column: "AdmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentUsages_ChargeId",
                table: "EquipmentUsages",
                column: "ChargeId",
                unique: true,
                filter: "\"ChargeId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentUsages_IdempotencyKey",
                table: "EquipmentUsages",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_EquipmentUsages_ActiveDevice",
                table: "EquipmentUsages",
                columns: new[] { "EquipmentId", "Identifier" },
                unique: true,
                filter: "\"Status\" = 'Active' AND \"Identifier\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_EquipmentUsages_ActivePerAdmission",
                table: "EquipmentUsages",
                columns: new[] { "AdmissionId", "EquipmentId" },
                unique: true,
                filter: "\"Status\" = 'Active'");

            migrationBuilder.CreateIndex(
                name: "IX_NicuEquipment_Code",
                table: "NicuEquipment",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NicuEquipment_ServiceItemId",
                table: "NicuEquipment",
                column: "ServiceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientAdvanceEntries_AdvanceId",
                table: "PatientAdvanceEntries",
                column: "AdvanceId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientAdvanceEntries_ReceiptId",
                table: "PatientAdvanceEntries",
                column: "ReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientAdvances_AdmissionId",
                table: "PatientAdvances",
                column: "AdmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientAdvances_CounterId",
                table: "PatientAdvances",
                column: "CounterId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientAdvances_Number",
                table: "PatientAdvances",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PatientAdvances_PatientId_AdmissionId",
                table: "PatientAdvances",
                columns: new[] { "PatientId", "AdmissionId" });

            migrationBuilder.CreateIndex(
                name: "IX_PatientAdvances_PaymentMethodId",
                table: "PatientAdvances",
                column: "PaymentMethodId");

            migrationBuilder.CreateIndex(
                name: "UX_PatientAdvances_Idempotency",
                table: "PatientAdvances",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServicePackageItems_ServiceItemId",
                table: "ServicePackageItems",
                column: "ServiceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ServicePackages_Code",
                table: "ServicePackages",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServicePackages_ServiceItemId",
                table: "ServicePackages",
                column: "ServiceItemId");
            // Update 10: existing hospitals also require a cleared bill (or an authorised override) before discharge – IPD settings can switch it off.
            migrationBuilder.Sql("UPDATE \"IpdSettings\" SET \"RequireBillClearanceForDischarge\" = TRUE;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdmissionPackages");

            migrationBuilder.DropTable(
                name: "DischargeClearanceOverrides");

            migrationBuilder.DropTable(
                name: "EquipmentUsages");

            migrationBuilder.DropTable(
                name: "PatientAdvanceEntries");

            migrationBuilder.DropTable(
                name: "ServicePackageItems");

            migrationBuilder.DropTable(
                name: "NicuEquipment");

            migrationBuilder.DropTable(
                name: "PatientAdvances");

            migrationBuilder.DropTable(
                name: "ServicePackages");

            migrationBuilder.DropColumn(
                name: "BedChargeCutoff",
                table: "IpdSettings");

            migrationBuilder.DropColumn(
                name: "BedChargePolicy",
                table: "IpdSettings");

            migrationBuilder.DropColumn(
                name: "HalfDayHours",
                table: "IpdSettings");

            migrationBuilder.DropColumn(
                name: "Quantity",
                table: "IpdBedCharges");

            migrationBuilder.DropColumn(
                name: "AdmissionFeeChargeId",
                table: "IpdAdmissions");

            migrationBuilder.DropColumn(
                name: "Quantity",
                table: "Charges");

            migrationBuilder.DropColumn(
                name: "UnitPrice",
                table: "Charges");

            migrationBuilder.DropColumn(
                name: "AdvanceId",
                table: "CashierLedger");

            migrationBuilder.DropSequence(
                name: "patient_advance_seq");
        }
    }
}
