using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BurhanNexus.HMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Update13 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsFree",
                table: "ServiceItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Update 13: zero-fee services are no longer free by default. Existing hospitals keep their behaviour: a service
            // whose fee is 0 and that was already used (billed / booked) or deliberately set to 0 by a user is marked FREE.
            // Unused, unpriced standard-catalogue items (seeded by SYSTEM) stay "fee not set".
            migrationBuilder.Sql("""
                UPDATE "ServiceItems" s SET "IsFree" = TRUE
                WHERE s."DefaultFee" = 0 AND (
                    EXISTS (SELECT 1 FROM "Charges" c WHERE c."ServiceItemId" = s."Id")
                    OR EXISTS (SELECT 1 FROM "ServiceFees" f WHERE f."ServiceItemId" = s."Id" AND COALESCE(f."CreatedByUsername", '') <> 'SYSTEM')
                    OR (s."Kind" = 'Consultation' AND s."DoctorId" IS NOT NULL AND EXISTS (SELECT 1 FROM "Appointments" a WHERE a."DoctorId" = s."DoctorId"))
                    OR EXISTS (SELECT 1 FROM "LabTests" t JOIN "LabOrderItems" i ON i."LabTestId" = t."Id" WHERE t."ServiceItemId" = s."Id")
                    OR EXISTS (SELECT 1 FROM "RadiologyServices" r JOIN "RadiologyExams" e ON e."ServiceId" = r."Id" WHERE r."ServiceItemId" = s."Id"));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsFree",
                table: "ServiceItems");
        }
    }
}
