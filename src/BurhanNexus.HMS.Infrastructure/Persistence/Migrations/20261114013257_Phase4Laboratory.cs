using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BurhanNexus.HMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase4Laboratory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "lab_order_number_seq");

            migrationBuilder.CreateSequence(
                name: "lab_sample_number_seq");

            migrationBuilder.AddColumn<long>(
                name: "LabOrderId",
                table: "Charges",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LabCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LabInventoryItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Unit = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Supplier = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    ReorderLevel = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    TracksExpiry = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabInventoryItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LabOrders",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrderNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<long>(type: "bigint", nullable: false),
                    EncounterId = table.Column<long>(type: "bigint", nullable: true),
                    ReferralType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReferringDoctorId = table.Column<int>(type: "integer", nullable: true),
                    ExternalDoctorName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    ClinicalNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsUrgent = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_LabOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LabOrders_Doctors_ReferringDoctorId",
                        column: x => x.ReferringDoctorId,
                        principalTable: "Doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LabOrders_PatientEncounters_EncounterId",
                        column: x => x.EncounterId,
                        principalTable: "PatientEncounters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LabOrders_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LabSampleTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Container = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabSampleTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LabSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    LaboratoryName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    HospitalName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ContactNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    ReportHeader = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ReportFooter = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Layout = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SignatoryName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    SignatoryDesignation = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Logo = table.Column<byte[]>(type: "bytea", nullable: true),
                    LogoContentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Signature = table.Column<byte[]>(type: "bytea", nullable: true),
                    SignatureContentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ShowVerifiedBy = table.Column<bool>(type: "boolean", nullable: false),
                    AllowSelfVerification = table.Column<bool>(type: "boolean", nullable: false),
                    RequirePaymentBeforeRelease = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabSettings", x => x.Id);
                    table.CheckConstraint("CK_LabSettings_Singleton", "\"Id\" = 1");
                });

            migrationBuilder.CreateTable(
                name: "LabReportPrints",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LabOrderId = table.Column<long>(type: "bigint", nullable: false),
                    ItemIds = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CopyNumber = table.Column<int>(type: "integer", nullable: false),
                    Format = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PrintedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PrintedByUsername = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Workstation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabReportPrints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LabReportPrints_LabOrders_LabOrderId",
                        column: x => x.LabOrderId,
                        principalTable: "LabOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LabSamples",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SampleNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    LabOrderId = table.Column<long>(type: "bigint", nullable: false),
                    SampleTypeName = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CollectedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CollectedByUsername = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ReceivedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReceivedByUsername = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    RejectedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RejectedByUsername = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    RejectReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ReplacedBySampleId = table.Column<long>(type: "bigint", nullable: true),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabSamples", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LabSamples_LabOrders_LabOrderId",
                        column: x => x.LabOrderId,
                        principalTable: "LabOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LabTests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    CategoryId = table.Column<int>(type: "integer", nullable: false),
                    SampleTypeId = table.Column<int>(type: "integer", nullable: false),
                    ServiceItemId = table.Column<int>(type: "integer", nullable: false),
                    CollectionInstructions = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Method = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Interpretation = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    TurnaroundHours = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    DefinitionVersion = table.Column<int>(type: "integer", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabTests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LabTests_LabCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "LabCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LabTests_LabSampleTypes_SampleTypeId",
                        column: x => x.SampleTypeId,
                        principalTable: "LabSampleTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LabTests_ServiceItems_ServiceItemId",
                        column: x => x.ServiceItemId,
                        principalTable: "ServiceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LabOrderItems",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LabOrderId = table.Column<long>(type: "bigint", nullable: false),
                    LabTestId = table.Column<int>(type: "integer", nullable: false),
                    TestCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    TestName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    CategoryName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SampleTypeName = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Method = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DefinitionVersion = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SampleId = table.Column<long>(type: "bigint", nullable: true),
                    ChargeId = table.Column<long>(type: "bigint", nullable: true),
                    Comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ResultEnteredUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResultEnteredByUserId = table.Column<int>(type: "integer", nullable: true),
                    ResultEnteredByUsername = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
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
                    table.PrimaryKey("PK_LabOrderItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LabOrderItems_Charges_ChargeId",
                        column: x => x.ChargeId,
                        principalTable: "Charges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LabOrderItems_LabOrders_LabOrderId",
                        column: x => x.LabOrderId,
                        principalTable: "LabOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LabOrderItems_LabSamples_SampleId",
                        column: x => x.SampleId,
                        principalTable: "LabSamples",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LabOrderItems_LabTests_LabTestId",
                        column: x => x.LabTestId,
                        principalTable: "LabTests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LabTestParameters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LabTestId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Unit = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    ResultType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Options = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Decimals = table.Column<int>(type: "integer", nullable: false),
                    IsOptional = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    DefaultValue = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabTestParameters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LabTestParameters_LabTests_LabTestId",
                        column: x => x.LabTestId,
                        principalTable: "LabTests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LabItemEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrderItemId = table.Column<long>(type: "bigint", nullable: false),
                    EventType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: true),
                    Username = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabItemEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LabItemEvents_LabOrderItems_OrderItemId",
                        column: x => x.OrderItemId,
                        principalTable: "LabOrderItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LabResultRevisions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrderItemId = table.Column<long>(type: "bigint", nullable: false),
                    RevisionNo = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Snapshot = table.Column<string>(type: "jsonb", nullable: false),
                    PreviousStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUsername = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabResultRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LabResultRevisions_LabOrderItems_OrderItemId",
                        column: x => x.OrderItemId,
                        principalTable: "LabOrderItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LabResultValues",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrderItemId = table.Column<long>(type: "bigint", nullable: false),
                    ParameterId = table.Column<int>(type: "integer", nullable: true),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Unit = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    ResultType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Options = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Decimals = table.Column<int>(type: "integer", nullable: false),
                    IsOptional = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    RefLow = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: true),
                    RefHigh = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: true),
                    CriticalLow = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: true),
                    CriticalHigh = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: true),
                    RefText = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Value = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    NumericValue = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: true),
                    Flag = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabResultValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LabResultValues_LabOrderItems_OrderItemId",
                        column: x => x.OrderItemId,
                        principalTable: "LabOrderItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LabReferenceRanges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ParameterId = table.Column<int>(type: "integer", nullable: false),
                    Gender = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    MinAgeDays = table.Column<int>(type: "integer", nullable: true),
                    MaxAgeDays = table.Column<int>(type: "integer", nullable: true),
                    Low = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: true),
                    High = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: true),
                    CriticalLow = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: true),
                    CriticalHigh = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: true),
                    NormalText = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabReferenceRanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LabReferenceRanges_LabTestParameters_ParameterId",
                        column: x => x.ParameterId,
                        principalTable: "LabTestParameters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Charges_LabOrderId",
                table: "Charges",
                column: "LabOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_LabCategories_Name",
                table: "LabCategories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LabInventoryItems_Code",
                table: "LabInventoryItems",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LabItemEvents_EventType_BusinessDate",
                table: "LabItemEvents",
                columns: new[] { "EventType", "BusinessDate" });

            migrationBuilder.CreateIndex(
                name: "IX_LabItemEvents_OrderItemId",
                table: "LabItemEvents",
                column: "OrderItemId");

            migrationBuilder.CreateIndex(
                name: "IX_LabOrderItems_ChargeId",
                table: "LabOrderItems",
                column: "ChargeId");

            migrationBuilder.CreateIndex(
                name: "IX_LabOrderItems_LabOrderId",
                table: "LabOrderItems",
                column: "LabOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_LabOrderItems_LabTestId",
                table: "LabOrderItems",
                column: "LabTestId");

            migrationBuilder.CreateIndex(
                name: "IX_LabOrderItems_SampleId",
                table: "LabOrderItems",
                column: "SampleId");

            migrationBuilder.CreateIndex(
                name: "IX_LabOrderItems_Status_LabOrderId",
                table: "LabOrderItems",
                columns: new[] { "Status", "LabOrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_LabOrders_BusinessDate",
                table: "LabOrders",
                column: "BusinessDate");

            migrationBuilder.CreateIndex(
                name: "IX_LabOrders_EncounterId",
                table: "LabOrders",
                column: "EncounterId");

            migrationBuilder.CreateIndex(
                name: "IX_LabOrders_OrderNumber",
                table: "LabOrders",
                column: "OrderNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LabOrders_PatientId_BusinessDate",
                table: "LabOrders",
                columns: new[] { "PatientId", "BusinessDate" });

            migrationBuilder.CreateIndex(
                name: "IX_LabOrders_ReferringDoctorId",
                table: "LabOrders",
                column: "ReferringDoctorId");

            migrationBuilder.CreateIndex(
                name: "UX_LabOrders_Idempotency",
                table: "LabOrders",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LabReferenceRanges_ParameterId",
                table: "LabReferenceRanges",
                column: "ParameterId");

            migrationBuilder.CreateIndex(
                name: "IX_LabReportPrints_LabOrderId_CopyNumber",
                table: "LabReportPrints",
                columns: new[] { "LabOrderId", "CopyNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LabResultRevisions_OrderItemId_RevisionNo",
                table: "LabResultRevisions",
                columns: new[] { "OrderItemId", "RevisionNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LabResultValues_OrderItemId",
                table: "LabResultValues",
                column: "OrderItemId");

            migrationBuilder.CreateIndex(
                name: "IX_LabSamples_LabOrderId_Status",
                table: "LabSamples",
                columns: new[] { "LabOrderId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_LabSamples_SampleNumber",
                table: "LabSamples",
                column: "SampleNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LabSampleTypes_Code",
                table: "LabSampleTypes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LabSampleTypes_Name",
                table: "LabSampleTypes",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LabTestParameters_LabTestId_SortOrder",
                table: "LabTestParameters",
                columns: new[] { "LabTestId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_LabTests_CategoryId",
                table: "LabTests",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_LabTests_Code",
                table: "LabTests",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LabTests_SampleTypeId",
                table: "LabTests",
                column: "SampleTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_LabTests_ServiceItemId",
                table: "LabTests",
                column: "ServiceItemId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Charges_LabOrders_LabOrderId",
                table: "Charges",
                column: "LabOrderId",
                principalTable: "LabOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Charges_LabOrders_LabOrderId",
                table: "Charges");

            migrationBuilder.DropTable(
                name: "LabInventoryItems");

            migrationBuilder.DropTable(
                name: "LabItemEvents");

            migrationBuilder.DropTable(
                name: "LabReferenceRanges");

            migrationBuilder.DropTable(
                name: "LabReportPrints");

            migrationBuilder.DropTable(
                name: "LabResultRevisions");

            migrationBuilder.DropTable(
                name: "LabResultValues");

            migrationBuilder.DropTable(
                name: "LabSettings");

            migrationBuilder.DropTable(
                name: "LabTestParameters");

            migrationBuilder.DropTable(
                name: "LabOrderItems");

            migrationBuilder.DropTable(
                name: "LabSamples");

            migrationBuilder.DropTable(
                name: "LabTests");

            migrationBuilder.DropTable(
                name: "LabOrders");

            migrationBuilder.DropTable(
                name: "LabCategories");

            migrationBuilder.DropTable(
                name: "LabSampleTypes");

            migrationBuilder.DropIndex(
                name: "IX_Charges_LabOrderId",
                table: "Charges");

            migrationBuilder.DropColumn(
                name: "LabOrderId",
                table: "Charges");

            migrationBuilder.DropSequence(
                name: "lab_order_number_seq");

            migrationBuilder.DropSequence(
                name: "lab_sample_number_seq");
        }
    }
}
