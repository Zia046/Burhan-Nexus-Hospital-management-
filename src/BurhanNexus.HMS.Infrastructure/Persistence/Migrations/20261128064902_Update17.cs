using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BurhanNexus.HMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Update17 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PatientMedicineIssues_Government",
                table: "PatientMedicineIssues");

            migrationBuilder.AddColumn<bool>(
                name: "AllowChangeIssueMode",
                table: "PharmacySettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DefaultIssueMode",
                table: "PharmacySettings",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DiscountEnabled",
                table: "PharmacySettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "FreeIssueApprovalRequired",
                table: "PharmacySettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "FreeIssueEnabled",
                table: "PharmacySettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PaidSaleEnabled",
                table: "PharmacySettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "StockClassificationEnabled",
                table: "PharmacySettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SubsidyEnabled",
                table: "PharmacySettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ApprovalNote",
                table: "PatientMedicineIssues",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovalStatus",
                table: "PatientMedicineIssues",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ApprovedByUserId",
                table: "PatientMedicineIssues",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovedByUsername",
                table: "PatientMedicineIssues",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedUtc",
                table: "PatientMedicineIssues",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FreeReason",
                table: "PatientMedicineIssues",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IssueMode",
                table: "PatientMedicineIssues",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PatientAmount",
                table: "PatientMedicineIssues",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SubsidyAmount",
                table: "PatientMedicineIssues",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalValue",
                table: "PatientMedicineIssues",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StockClass",
                table: "MedicineBatches",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactNumber",
                table: "Doctors",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            // Update 17: existing hospitals keep their pharmacy behaviour. The Medicine Issue Configuration starts from the stored
            // hospital type (Government → Free Issue; Private → Paid Sale with discounts); the administrator changes it later.
            migrationBuilder.Sql("""
                UPDATE "PharmacySettings" p SET
                    "FreeIssueEnabled" = (h."HospitalType" = 'Government'),
                    "PaidSaleEnabled" = (h."HospitalType" <> 'Government'),
                    "DiscountEnabled" = (h."HospitalType" <> 'Government'),
                    "SubsidyEnabled" = FALSE, "FreeIssueApprovalRequired" = FALSE, "AllowChangeIssueMode" = TRUE, "StockClassificationEnabled" = FALSE,
                    "DefaultIssueMode" = CASE WHEN h."HospitalType" = 'Government' THEN 'Free' ELSE 'Paid' END
                FROM "HospitalConfiguration" h;
                UPDATE "PharmacySettings" SET "PaidSaleEnabled" = TRUE, "DiscountEnabled" = TRUE, "AllowChangeIssueMode" = TRUE, "DefaultIssueMode" = 'Paid'
                WHERE "DefaultIssueMode" IS NULL;
                -- Old documents get their issue mode (free = Government mode; paid / discounted = Private mode). Amounts are not changed.
                UPDATE "PatientMedicineIssues" SET
                    "IssueMode" = CASE WHEN "Mode" = 'Government' THEN 'Free' WHEN COALESCE("DiscountAmount", 0) > 0 THEN 'Discounted' ELSE 'Paid' END,
                    "TotalValue" = CASE WHEN "Mode" = 'Government' THEN NULL ELSE "GrossAmount" END,
                    "PatientAmount" = CASE WHEN "Mode" = 'Government' THEN 0 ELSE "NetAmount" END
                WHERE "IssueMode" IS NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_PatientMedicineIssues_ApprovalStatus",
                table: "PatientMedicineIssues",
                column: "ApprovalStatus");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PatientMedicineIssues_Government",
                table: "PatientMedicineIssues",
                sql: "\"Mode\" = 'Private' OR (\"GrossAmount\" IS NULL AND \"NetAmount\" IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PatientMedicineIssues_ApprovalStatus",
                table: "PatientMedicineIssues");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PatientMedicineIssues_Government",
                table: "PatientMedicineIssues");

            migrationBuilder.DropColumn(
                name: "AllowChangeIssueMode",
                table: "PharmacySettings");

            migrationBuilder.DropColumn(
                name: "DefaultIssueMode",
                table: "PharmacySettings");

            migrationBuilder.DropColumn(
                name: "DiscountEnabled",
                table: "PharmacySettings");

            migrationBuilder.DropColumn(
                name: "FreeIssueApprovalRequired",
                table: "PharmacySettings");

            migrationBuilder.DropColumn(
                name: "FreeIssueEnabled",
                table: "PharmacySettings");

            migrationBuilder.DropColumn(
                name: "PaidSaleEnabled",
                table: "PharmacySettings");

            migrationBuilder.DropColumn(
                name: "StockClassificationEnabled",
                table: "PharmacySettings");

            migrationBuilder.DropColumn(
                name: "SubsidyEnabled",
                table: "PharmacySettings");

            migrationBuilder.DropColumn(
                name: "ApprovalNote",
                table: "PatientMedicineIssues");

            migrationBuilder.DropColumn(
                name: "ApprovalStatus",
                table: "PatientMedicineIssues");

            migrationBuilder.DropColumn(
                name: "ApprovedByUserId",
                table: "PatientMedicineIssues");

            migrationBuilder.DropColumn(
                name: "ApprovedByUsername",
                table: "PatientMedicineIssues");

            migrationBuilder.DropColumn(
                name: "ApprovedUtc",
                table: "PatientMedicineIssues");

            migrationBuilder.DropColumn(
                name: "FreeReason",
                table: "PatientMedicineIssues");

            migrationBuilder.DropColumn(
                name: "IssueMode",
                table: "PatientMedicineIssues");

            migrationBuilder.DropColumn(
                name: "PatientAmount",
                table: "PatientMedicineIssues");

            migrationBuilder.DropColumn(
                name: "SubsidyAmount",
                table: "PatientMedicineIssues");

            migrationBuilder.DropColumn(
                name: "TotalValue",
                table: "PatientMedicineIssues");

            migrationBuilder.DropColumn(
                name: "StockClass",
                table: "MedicineBatches");

            migrationBuilder.DropColumn(
                name: "ContactNumber",
                table: "Doctors");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PatientMedicineIssues_Government",
                table: "PatientMedicineIssues",
                sql: "\"Mode\" = 'Private' OR (\"EncounterId\" IS NOT NULL AND \"GrossAmount\" IS NULL AND \"NetAmount\" IS NULL)");
        }
    }
}
