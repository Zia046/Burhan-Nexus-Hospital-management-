using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BurhanNexus.HMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Update9LabNursing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "medicine_request_seq");

            migrationBuilder.AddColumn<bool>(
                name: "RequiresReview",
                table: "LabTests",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "StandardCatalogueVersion",
                table: "LabSettings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsPregnancy",
                table: "LabReferenceRanges",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PatientPregnant",
                table: "LabOrders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "LabCultureResults",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ResultValueId = table.Column<long>(type: "bigint", nullable: false),
                    Organism = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Antibiotic = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Interpretation = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Mic = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabCultureResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LabCultureResults_LabResultValues_ResultValueId",
                        column: x => x.ResultValueId,
                        principalTable: "LabResultValues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MedicineRequests",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                    AdmissionId = table.Column<long>(type: "bigint", nullable: false),
                    PatientId = table.Column<long>(type: "bigint", nullable: false),
                    WardId = table.Column<int>(type: "integer", nullable: true),
                    WardName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BedCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RequestedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RequestedByUserId = table.Column<int>(type: "integer", nullable: true),
                    RequestedByUsername = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    RequestedByName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicineRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MedicineRequests_IpdAdmissions_AdmissionId",
                        column: x => x.AdmissionId,
                        principalTable: "IpdAdmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MedicineRequests_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MedicineRequestEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RequestId = table.Column<long>(type: "bigint", nullable: false),
                    LineId = table.Column<long>(type: "bigint", nullable: true),
                    Event = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: true),
                    PharmacyIssueId = table.Column<long>(type: "bigint", nullable: true),
                    PharmacyIssueNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Note = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: true),
                    Username = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicineRequestEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MedicineRequestEvents_MedicineRequests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "MedicineRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MedicineRequestLines",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RequestId = table.Column<long>(type: "bigint", nullable: false),
                    MedicineId = table.Column<int>(type: "integer", nullable: false),
                    MedicineName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    IssuedQuantity = table.Column<int>(type: "integer", nullable: false),
                    Instructions = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DecisionNote = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    DecidedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DecidedBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicineRequestLines", x => x.Id);
                    table.CheckConstraint("CK_MedicineRequestLines_Qty", "\"Quantity\" > 0 AND \"IssuedQuantity\" >= 0 AND \"IssuedQuantity\" <= \"Quantity\"");
                    table.ForeignKey(
                        name: "FK_MedicineRequestLines_MedicineRequests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "MedicineRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MedicineRequestLines_Medicines_MedicineId",
                        column: x => x.MedicineId,
                        principalTable: "Medicines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LabCultureResults_ResultValueId",
                table: "LabCultureResults",
                column: "ResultValueId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicineRequestEvents_RequestId",
                table: "MedicineRequestEvents",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicineRequestLines_MedicineId",
                table: "MedicineRequestLines",
                column: "MedicineId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicineRequestLines_RequestId",
                table: "MedicineRequestLines",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicineRequests_AdmissionId",
                table: "MedicineRequests",
                column: "AdmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicineRequests_Number",
                table: "MedicineRequests",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MedicineRequests_PatientId",
                table: "MedicineRequests",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicineRequests_Status_RequestedUtc",
                table: "MedicineRequests",
                columns: new[] { "Status", "RequestedUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_MedicineRequests_Idempotency",
                table: "MedicineRequests",
                column: "IdempotencyKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LabCultureResults");

            migrationBuilder.DropTable(
                name: "MedicineRequestEvents");

            migrationBuilder.DropTable(
                name: "MedicineRequestLines");

            migrationBuilder.DropTable(
                name: "MedicineRequests");

            migrationBuilder.DropColumn(
                name: "RequiresReview",
                table: "LabTests");

            migrationBuilder.DropColumn(
                name: "StandardCatalogueVersion",
                table: "LabSettings");

            migrationBuilder.DropColumn(
                name: "IsPregnancy",
                table: "LabReferenceRanges");

            migrationBuilder.DropColumn(
                name: "PatientPregnant",
                table: "LabOrders");

            migrationBuilder.DropSequence(
                name: "medicine_request_seq");
        }
    }
}
