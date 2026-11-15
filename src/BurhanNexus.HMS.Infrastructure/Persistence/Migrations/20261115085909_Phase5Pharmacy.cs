using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BurhanNexus.HMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase5Pharmacy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "medicine_code_seq");

            migrationBuilder.CreateSequence(
                name: "pharm_adjustment_seq");

            migrationBuilder.CreateSequence(
                name: "pharm_issue_seq");

            migrationBuilder.CreateSequence(
                name: "pharm_receipt_seq");

            migrationBuilder.CreateSequence(
                name: "pharm_return_seq");

            migrationBuilder.CreateSequence(
                name: "pharm_sale_seq");

            migrationBuilder.CreateSequence(
                name: "pharm_supplier_payment_seq");

            migrationBuilder.CreateSequence(
                name: "pharm_supplier_return_seq");

            migrationBuilder.CreateSequence(
                name: "pharm_transfer_seq");

            migrationBuilder.CreateSequence(
                name: "supplier_code_seq");

            migrationBuilder.AddColumn<long>(
                name: "PharmacyIssueId",
                table: "Charges",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PharmacySaleId",
                table: "Charges",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PharmacyLookups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PharmacyLookups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PharmacySettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    PharmacyName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    NearExpiryDays = table.Column<int>(type: "integer", nullable: false),
                    EncounterValidDays = table.Column<int>(type: "integer", nullable: false),
                    MedicineServiceItemId = table.Column<int>(type: "integer", nullable: true),
                    GovernmentIssueServiceItemId = table.Column<int>(type: "integer", nullable: true),
                    SlipFooter = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PharmacySettings", x => x.Id);
                    table.CheckConstraint("CK_PharmacySettings_Singleton", "\"Id\" = 1");
                    table.ForeignKey(
                        name: "FK_PharmacySettings_ServiceItems_GovernmentIssueServiceItemId",
                        column: x => x.GovernmentIssueServiceItemId,
                        principalTable: "ServiceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PharmacySettings_ServiceItems_MedicineServiceItemId",
                        column: x => x.MedicineServiceItemId,
                        principalTable: "ServiceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockLocations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DepartmentId = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockLocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockLocations_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Suppliers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ContactPerson = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    City = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Suppliers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Medicines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    GenericName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    BrandName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    ManufacturerId = table.Column<int>(type: "integer", nullable: true),
                    CategoryId = table.Column<int>(type: "integer", nullable: true),
                    DosageFormId = table.Column<int>(type: "integer", nullable: true),
                    Strength = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PackSize = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Unit = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    UnitsPerPack = table.Column<int>(type: "integer", nullable: false),
                    MinimumStock = table.Column<int>(type: "integer", nullable: false),
                    StorageInstructions = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    PurchasePrice = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: true),
                    SalePrice = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    AllowDiscount = table.Column<bool>(type: "boolean", nullable: false),
                    MaxDiscountPercent = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUsername = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Medicines", x => x.Id);
                    table.CheckConstraint("CK_Medicines_Values", "\"MinimumStock\" >= 0 AND \"UnitsPerPack\" >= 1 AND \"MaxDiscountPercent\" BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "FK_Medicines_PharmacyLookups_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "PharmacyLookups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Medicines_PharmacyLookups_DosageFormId",
                        column: x => x.DosageFormId,
                        principalTable: "PharmacyLookups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Medicines_PharmacyLookups_ManufacturerId",
                        column: x => x.ManufacturerId,
                        principalTable: "PharmacyLookups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PatientMedicineIssues",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                    Mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PatientId = table.Column<long>(type: "bigint", nullable: false),
                    EncounterId = table.Column<long>(type: "bigint", nullable: true),
                    LocationId = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    GrossAmount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    DiscountAmount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    NetAmount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: true),
                    CreatedByUsername = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Workstation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientMedicineIssues", x => x.Id);
                    table.CheckConstraint("CK_PatientMedicineIssues_Government", "\"Mode\" = 'Private' OR (\"EncounterId\" IS NOT NULL AND \"GrossAmount\" IS NULL AND \"NetAmount\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_PatientMedicineIssues_PatientEncounters_EncounterId",
                        column: x => x.EncounterId,
                        principalTable: "PatientEncounters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PatientMedicineIssues_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PatientMedicineIssues_StockLocations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "StockLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PharmacySales",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<int>(type: "integer", nullable: false),
                    CustomerName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    CustomerPhone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    GrossAmount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    NetAmount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    ReceiptId = table.Column<long>(type: "bigint", nullable: true),
                    CounterId = table.Column<int>(type: "integer", nullable: false),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: true),
                    CreatedByUsername = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PharmacySales", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PharmacySales_Counters_CounterId",
                        column: x => x.CounterId,
                        principalTable: "Counters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PharmacySales_Receipts_ReceiptId",
                        column: x => x.ReceiptId,
                        principalTable: "Receipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PharmacySales_StockLocations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "StockLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockTransfers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                    FromLocationId = table.Column<int>(type: "integer", nullable: false),
                    ToLocationId = table.Column<int>(type: "integer", nullable: false),
                    Reference = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: true),
                    CreatedByUsername = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockTransfers", x => x.Id);
                    table.CheckConstraint("CK_StockTransfers_Locations", "\"FromLocationId\" <> \"ToLocationId\"");
                    table.ForeignKey(
                        name: "FK_StockTransfers_StockLocations_FromLocationId",
                        column: x => x.FromLocationId,
                        principalTable: "StockLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockTransfers_StockLocations_ToLocationId",
                        column: x => x.ToLocationId,
                        principalTable: "StockLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockReceipts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                    Mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    LocationId = table.Column<int>(type: "integer", nullable: false),
                    SupplierId = table.Column<int>(type: "integer", nullable: true),
                    SourceName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    ReferenceNumber = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    InvoiceDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ReceivedDate = table.Column<DateOnly>(type: "date", nullable: false),
                    GrossAmount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    DiscountAmount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    AdditionalCharges = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    NetAmount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: true),
                    CreatedByUsername = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockReceipts", x => x.Id);
                    table.CheckConstraint("CK_StockReceipts_GovernmentNoPrices", "\"Mode\" = 'Private' OR (\"GrossAmount\" IS NULL AND \"NetAmount\" IS NULL AND \"DiscountAmount\" IS NULL AND \"AdditionalCharges\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_StockReceipts_StockLocations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "StockLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockReceipts_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupplierPayments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    PaymentMethodId = table.Column<int>(type: "integer", nullable: false),
                    PaymentReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PaidUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RecordedByUserId = table.Column<int>(type: "integer", nullable: true),
                    RecordedByUsername = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierPayments", x => x.Id);
                    table.CheckConstraint("CK_SupplierPayments_Amount", "\"Amount\" > 0");
                    table.ForeignKey(
                        name: "FK_SupplierPayments_PaymentMethods_PaymentMethodId",
                        column: x => x.PaymentMethodId,
                        principalTable: "PaymentMethods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierPayments_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupplierReturns",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                    Mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SupplierId = table.Column<int>(type: "integer", nullable: true),
                    SourceName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    LocationId = table.Column<int>(type: "integer", nullable: false),
                    ReferenceInvoice = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: true),
                    CreatedByUsername = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierReturns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierReturns_StockLocations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "StockLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierReturns_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MedicineBatches",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MedicineId = table.Column<int>(type: "integer", nullable: false),
                    BatchNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ManufactureDate = table.Column<DateOnly>(type: "date", nullable: true),
                    SupplierId = table.Column<int>(type: "integer", nullable: true),
                    UnitCost = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: true),
                    SalePrice = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUsername = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicineBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MedicineBatches_Medicines_MedicineId",
                        column: x => x.MedicineId,
                        principalTable: "Medicines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MedicineBatches_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MedicinePriceChanges",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MedicineId = table.Column<int>(type: "integer", nullable: false),
                    OldPurchasePrice = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    NewPurchasePrice = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    OldSalePrice = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    NewSalePrice = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    BatchId = table.Column<long>(type: "bigint", nullable: true),
                    Note = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ChangedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ChangedByUserId = table.Column<int>(type: "integer", nullable: true),
                    ChangedByUsername = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicinePriceChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MedicinePriceChanges_Medicines_MedicineId",
                        column: x => x.MedicineId,
                        principalTable: "Medicines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MedicineReturns",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SaleId = table.Column<long>(type: "bigint", nullable: true),
                    IssueId = table.Column<long>(type: "bigint", nullable: true),
                    LocationId = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    RefundId = table.Column<long>(type: "bigint", nullable: true),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: true),
                    CreatedByUsername = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicineReturns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MedicineReturns_PatientMedicineIssues_IssueId",
                        column: x => x.IssueId,
                        principalTable: "PatientMedicineIssues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MedicineReturns_PharmacySales_SaleId",
                        column: x => x.SaleId,
                        principalTable: "PharmacySales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MedicineReturns_Refunds_RefundId",
                        column: x => x.RefundId,
                        principalTable: "Refunds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MedicineReturns_StockLocations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "StockLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupplierLedger",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SupplierId = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    StockReceiptId = table.Column<long>(type: "bigint", nullable: true),
                    SupplierPaymentId = table.Column<long>(type: "bigint", nullable: true),
                    SupplierReturnId = table.Column<long>(type: "bigint", nullable: true),
                    Reference = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: true),
                    CreatedByUsername = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierLedger", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierLedger_StockReceipts_StockReceiptId",
                        column: x => x.StockReceiptId,
                        principalTable: "StockReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierLedger_SupplierPayments_SupplierPaymentId",
                        column: x => x.SupplierPaymentId,
                        principalTable: "SupplierPayments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierLedger_SupplierReturns_SupplierReturnId",
                        column: x => x.SupplierReturnId,
                        principalTable: "SupplierReturns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierLedger_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryTransactions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    LocationId = table.Column<int>(type: "integer", nullable: false),
                    MedicineId = table.Column<int>(type: "integer", nullable: false),
                    BatchId = table.Column<long>(type: "bigint", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    BalanceAfter = table.Column<int>(type: "integer", nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: true),
                    UnitPrice = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    DocumentType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    DocumentId = table.Column<long>(type: "bigint", nullable: false),
                    DocumentNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CounterpartLocationId = table.Column<int>(type: "integer", nullable: true),
                    PatientId = table.Column<long>(type: "bigint", nullable: true),
                    Note = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: true),
                    Username = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryTransactions", x => x.Id);
                    table.CheckConstraint("CK_InventoryTransactions_Values", "\"Quantity\" <> 0 AND \"BalanceAfter\" >= 0");
                    table.ForeignKey(
                        name: "FK_InventoryTransactions_MedicineBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "MedicineBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTransactions_Medicines_MedicineId",
                        column: x => x.MedicineId,
                        principalTable: "Medicines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTransactions_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTransactions_StockLocations_CounterpartLocationId",
                        column: x => x.CounterpartLocationId,
                        principalTable: "StockLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTransactions_StockLocations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "StockLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PatientMedicineIssueItems",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IssueId = table.Column<long>(type: "bigint", nullable: false),
                    MedicineId = table.Column<int>(type: "integer", nullable: false),
                    BatchId = table.Column<long>(type: "bigint", nullable: false),
                    MedicineName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BatchNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    ReturnedQuantity = table.Column<int>(type: "integer", nullable: false),
                    Instructions = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    FefoOverridden = table.Column<bool>(type: "boolean", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    UnitCost = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: true),
                    Discount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    LineTotal = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    ChargeId = table.Column<long>(type: "bigint", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientMedicineIssueItems", x => x.Id);
                    table.CheckConstraint("CK_PatientMedicineIssueItems_Quantity", "\"Quantity\" > 0 AND \"ReturnedQuantity\" BETWEEN 0 AND \"Quantity\"");
                    table.ForeignKey(
                        name: "FK_PatientMedicineIssueItems_Charges_ChargeId",
                        column: x => x.ChargeId,
                        principalTable: "Charges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PatientMedicineIssueItems_MedicineBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "MedicineBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PatientMedicineIssueItems_Medicines_MedicineId",
                        column: x => x.MedicineId,
                        principalTable: "Medicines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PatientMedicineIssueItems_PatientMedicineIssues_IssueId",
                        column: x => x.IssueId,
                        principalTable: "PatientMedicineIssues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PharmacySaleItems",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SaleId = table.Column<long>(type: "bigint", nullable: false),
                    MedicineId = table.Column<int>(type: "integer", nullable: false),
                    BatchId = table.Column<long>(type: "bigint", nullable: false),
                    MedicineName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BatchNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    ReturnedQuantity = table.Column<int>(type: "integer", nullable: false),
                    FefoOverridden = table.Column<bool>(type: "boolean", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: true),
                    Discount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    LineTotal = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    ChargeId = table.Column<long>(type: "bigint", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PharmacySaleItems", x => x.Id);
                    table.CheckConstraint("CK_PharmacySaleItems_Quantity", "\"Quantity\" > 0 AND \"ReturnedQuantity\" BETWEEN 0 AND \"Quantity\"");
                    table.ForeignKey(
                        name: "FK_PharmacySaleItems_Charges_ChargeId",
                        column: x => x.ChargeId,
                        principalTable: "Charges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PharmacySaleItems_MedicineBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "MedicineBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PharmacySaleItems_Medicines_MedicineId",
                        column: x => x.MedicineId,
                        principalTable: "Medicines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PharmacySaleItems_PharmacySales_SaleId",
                        column: x => x.SaleId,
                        principalTable: "PharmacySales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockAdjustments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<int>(type: "integer", nullable: false),
                    MedicineId = table.Column<int>(type: "integer", nullable: false),
                    BatchId = table.Column<long>(type: "bigint", nullable: false),
                    Increase = table.Column<bool>(type: "boolean", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    QuantityBefore = table.Column<int>(type: "integer", nullable: false),
                    QuantityAfter = table.Column<int>(type: "integer", nullable: true),
                    Reason = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Note = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    RequestedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RequestedByUserId = table.Column<int>(type: "integer", nullable: true),
                    RequestedByUsername = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    DecidedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DecidedByUserId = table.Column<int>(type: "integer", nullable: true),
                    DecidedByUsername = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    DecisionNote = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockAdjustments", x => x.Id);
                    table.CheckConstraint("CK_StockAdjustments_Quantity", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_StockAdjustments_MedicineBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "MedicineBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockAdjustments_Medicines_MedicineId",
                        column: x => x.MedicineId,
                        principalTable: "Medicines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockAdjustments_StockLocations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "StockLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockBalances",
                columns: table => new
                {
                    LocationId = table.Column<int>(type: "integer", nullable: false),
                    BatchId = table.Column<long>(type: "bigint", nullable: false),
                    MedicineId = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockBalances", x => new { x.LocationId, x.BatchId });
                    table.CheckConstraint("CK_StockBalances_NotNegative", "\"Quantity\" >= 0");
                    table.ForeignKey(
                        name: "FK_StockBalances_MedicineBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "MedicineBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockBalances_Medicines_MedicineId",
                        column: x => x.MedicineId,
                        principalTable: "Medicines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockBalances_StockLocations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "StockLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockReceiptItems",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StockReceiptId = table.Column<long>(type: "bigint", nullable: false),
                    MedicineId = table.Column<int>(type: "integer", nullable: false),
                    BatchId = table.Column<long>(type: "bigint", nullable: false),
                    MedicineName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BatchNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ManufactureDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: true),
                    SalePrice = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    Discount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    LineTotal = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockReceiptItems", x => x.Id);
                    table.CheckConstraint("CK_StockReceiptItems_Quantity", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_StockReceiptItems_MedicineBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "MedicineBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockReceiptItems_Medicines_MedicineId",
                        column: x => x.MedicineId,
                        principalTable: "Medicines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockReceiptItems_StockReceipts_StockReceiptId",
                        column: x => x.StockReceiptId,
                        principalTable: "StockReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockTransferItems",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TransferId = table.Column<long>(type: "bigint", nullable: false),
                    MedicineId = table.Column<int>(type: "integer", nullable: false),
                    BatchId = table.Column<long>(type: "bigint", nullable: false),
                    MedicineName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BatchNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockTransferItems", x => x.Id);
                    table.CheckConstraint("CK_StockTransferItems_Quantity", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_StockTransferItems_MedicineBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "MedicineBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockTransferItems_Medicines_MedicineId",
                        column: x => x.MedicineId,
                        principalTable: "Medicines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockTransferItems_StockTransfers_TransferId",
                        column: x => x.TransferId,
                        principalTable: "StockTransfers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupplierReturnItems",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SupplierReturnId = table.Column<long>(type: "bigint", nullable: false),
                    MedicineId = table.Column<int>(type: "integer", nullable: false),
                    BatchId = table.Column<long>(type: "bigint", nullable: false),
                    MedicineName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BatchNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierReturnItems", x => x.Id);
                    table.CheckConstraint("CK_SupplierReturnItems_Quantity", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_SupplierReturnItems_MedicineBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "MedicineBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierReturnItems_Medicines_MedicineId",
                        column: x => x.MedicineId,
                        principalTable: "Medicines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierReturnItems_SupplierReturns_SupplierReturnId",
                        column: x => x.SupplierReturnId,
                        principalTable: "SupplierReturns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MedicineReturnItems",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReturnId = table.Column<long>(type: "bigint", nullable: false),
                    SaleItemId = table.Column<long>(type: "bigint", nullable: true),
                    IssueItemId = table.Column<long>(type: "bigint", nullable: true),
                    MedicineId = table.Column<int>(type: "integer", nullable: false),
                    BatchId = table.Column<long>(type: "bigint", nullable: false),
                    MedicineName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BatchNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    Restocked = table.Column<bool>(type: "boolean", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicineReturnItems", x => x.Id);
                    table.CheckConstraint("CK_MedicineReturnItems_Quantity", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_MedicineReturnItems_MedicineBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "MedicineBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MedicineReturnItems_MedicineReturns_ReturnId",
                        column: x => x.ReturnId,
                        principalTable: "MedicineReturns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MedicineReturnItems_Medicines_MedicineId",
                        column: x => x.MedicineId,
                        principalTable: "Medicines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MedicineReturnItems_PatientMedicineIssueItems_IssueItemId",
                        column: x => x.IssueItemId,
                        principalTable: "PatientMedicineIssueItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MedicineReturnItems_PharmacySaleItems_SaleItemId",
                        column: x => x.SaleItemId,
                        principalTable: "PharmacySaleItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Charges_PharmacyIssueId",
                table: "Charges",
                column: "PharmacyIssueId");

            migrationBuilder.CreateIndex(
                name: "IX_Charges_PharmacySaleId",
                table: "Charges",
                column: "PharmacySaleId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_BatchId_LocationId",
                table: "InventoryTransactions",
                columns: new[] { "BatchId", "LocationId" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_CounterpartLocationId",
                table: "InventoryTransactions",
                column: "CounterpartLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_DocumentType_DocumentId",
                table: "InventoryTransactions",
                columns: new[] { "DocumentType", "DocumentId" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_LocationId",
                table: "InventoryTransactions",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_MedicineId_LocationId_Id",
                table: "InventoryTransactions",
                columns: new[] { "MedicineId", "LocationId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_PatientId",
                table: "InventoryTransactions",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_Type_BusinessDate",
                table: "InventoryTransactions",
                columns: new[] { "Type", "BusinessDate" });

            migrationBuilder.CreateIndex(
                name: "IX_MedicineBatches_ExpiryDate",
                table: "MedicineBatches",
                column: "ExpiryDate");

            migrationBuilder.CreateIndex(
                name: "IX_MedicineBatches_SupplierId",
                table: "MedicineBatches",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "UX_MedicineBatches_Batch",
                table: "MedicineBatches",
                columns: new[] { "MedicineId", "BatchNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MedicinePriceChanges_MedicineId",
                table: "MedicinePriceChanges",
                column: "MedicineId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicineReturnItems_BatchId",
                table: "MedicineReturnItems",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicineReturnItems_IssueItemId",
                table: "MedicineReturnItems",
                column: "IssueItemId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicineReturnItems_MedicineId",
                table: "MedicineReturnItems",
                column: "MedicineId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicineReturnItems_ReturnId",
                table: "MedicineReturnItems",
                column: "ReturnId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicineReturnItems_SaleItemId",
                table: "MedicineReturnItems",
                column: "SaleItemId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicineReturns_BusinessDate",
                table: "MedicineReturns",
                column: "BusinessDate");

            migrationBuilder.CreateIndex(
                name: "IX_MedicineReturns_IssueId",
                table: "MedicineReturns",
                column: "IssueId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicineReturns_LocationId",
                table: "MedicineReturns",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicineReturns_Number",
                table: "MedicineReturns",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MedicineReturns_RefundId",
                table: "MedicineReturns",
                column: "RefundId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicineReturns_SaleId",
                table: "MedicineReturns",
                column: "SaleId");

            migrationBuilder.CreateIndex(
                name: "UX_MedicineReturns_Idempotency",
                table: "MedicineReturns",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Medicines_CategoryId",
                table: "Medicines",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Medicines_Code",
                table: "Medicines",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Medicines_DosageFormId",
                table: "Medicines",
                column: "DosageFormId");

            migrationBuilder.CreateIndex(
                name: "IX_Medicines_GenericName",
                table: "Medicines",
                column: "GenericName");

            migrationBuilder.CreateIndex(
                name: "IX_Medicines_ManufacturerId",
                table: "Medicines",
                column: "ManufacturerId");

            migrationBuilder.CreateIndex(
                name: "IX_Medicines_Name",
                table: "Medicines",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_PatientMedicineIssueItems_BatchId",
                table: "PatientMedicineIssueItems",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientMedicineIssueItems_ChargeId",
                table: "PatientMedicineIssueItems",
                column: "ChargeId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientMedicineIssueItems_IssueId",
                table: "PatientMedicineIssueItems",
                column: "IssueId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientMedicineIssueItems_MedicineId",
                table: "PatientMedicineIssueItems",
                column: "MedicineId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientMedicineIssues_BusinessDate",
                table: "PatientMedicineIssues",
                column: "BusinessDate");

            migrationBuilder.CreateIndex(
                name: "IX_PatientMedicineIssues_EncounterId",
                table: "PatientMedicineIssues",
                column: "EncounterId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientMedicineIssues_LocationId",
                table: "PatientMedicineIssues",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientMedicineIssues_Number",
                table: "PatientMedicineIssues",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PatientMedicineIssues_PatientId_BusinessDate",
                table: "PatientMedicineIssues",
                columns: new[] { "PatientId", "BusinessDate" });

            migrationBuilder.CreateIndex(
                name: "UX_PatientMedicineIssues_Idempotency",
                table: "PatientMedicineIssues",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PharmacyLookups_Kind_Name",
                table: "PharmacyLookups",
                columns: new[] { "Kind", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PharmacySaleItems_BatchId",
                table: "PharmacySaleItems",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_PharmacySaleItems_ChargeId",
                table: "PharmacySaleItems",
                column: "ChargeId");

            migrationBuilder.CreateIndex(
                name: "IX_PharmacySaleItems_MedicineId",
                table: "PharmacySaleItems",
                column: "MedicineId");

            migrationBuilder.CreateIndex(
                name: "IX_PharmacySaleItems_SaleId",
                table: "PharmacySaleItems",
                column: "SaleId");

            migrationBuilder.CreateIndex(
                name: "IX_PharmacySales_BusinessDate",
                table: "PharmacySales",
                column: "BusinessDate");

            migrationBuilder.CreateIndex(
                name: "IX_PharmacySales_CounterId",
                table: "PharmacySales",
                column: "CounterId");

            migrationBuilder.CreateIndex(
                name: "IX_PharmacySales_LocationId",
                table: "PharmacySales",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_PharmacySales_Number",
                table: "PharmacySales",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PharmacySales_ReceiptId",
                table: "PharmacySales",
                column: "ReceiptId");

            migrationBuilder.CreateIndex(
                name: "UX_PharmacySales_Idempotency",
                table: "PharmacySales",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PharmacySettings_GovernmentIssueServiceItemId",
                table: "PharmacySettings",
                column: "GovernmentIssueServiceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PharmacySettings_MedicineServiceItemId",
                table: "PharmacySettings",
                column: "MedicineServiceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustments_BatchId",
                table: "StockAdjustments",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustments_LocationId",
                table: "StockAdjustments",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustments_MedicineId",
                table: "StockAdjustments",
                column: "MedicineId");

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustments_Number",
                table: "StockAdjustments",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustments_Status_BusinessDate",
                table: "StockAdjustments",
                columns: new[] { "Status", "BusinessDate" });

            migrationBuilder.CreateIndex(
                name: "UX_StockAdjustments_Idempotency",
                table: "StockAdjustments",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockBalances_BatchId",
                table: "StockBalances",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_StockBalances_MedicineId_LocationId",
                table: "StockBalances",
                columns: new[] { "MedicineId", "LocationId" });

            migrationBuilder.CreateIndex(
                name: "IX_StockLocations_Code",
                table: "StockLocations",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockLocations_DepartmentId",
                table: "StockLocations",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_StockLocations_Name",
                table: "StockLocations",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockReceiptItems_BatchId",
                table: "StockReceiptItems",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_StockReceiptItems_MedicineId",
                table: "StockReceiptItems",
                column: "MedicineId");

            migrationBuilder.CreateIndex(
                name: "IX_StockReceiptItems_StockReceiptId",
                table: "StockReceiptItems",
                column: "StockReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_StockReceipts_BusinessDate",
                table: "StockReceipts",
                column: "BusinessDate");

            migrationBuilder.CreateIndex(
                name: "IX_StockReceipts_LocationId",
                table: "StockReceipts",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_StockReceipts_Number",
                table: "StockReceipts",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockReceipts_SupplierId",
                table: "StockReceipts",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "UX_StockReceipts_Idempotency",
                table: "StockReceipts",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockTransferItems_BatchId",
                table: "StockTransferItems",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransferItems_MedicineId",
                table: "StockTransferItems",
                column: "MedicineId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransferItems_TransferId",
                table: "StockTransferItems",
                column: "TransferId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfers_BusinessDate",
                table: "StockTransfers",
                column: "BusinessDate");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfers_FromLocationId",
                table: "StockTransfers",
                column: "FromLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfers_Number",
                table: "StockTransfers",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfers_ToLocationId",
                table: "StockTransfers",
                column: "ToLocationId");

            migrationBuilder.CreateIndex(
                name: "UX_StockTransfers_Idempotency",
                table: "StockTransfers",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierLedger_StockReceiptId",
                table: "SupplierLedger",
                column: "StockReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierLedger_SupplierId_BusinessDate",
                table: "SupplierLedger",
                columns: new[] { "SupplierId", "BusinessDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierLedger_SupplierPaymentId",
                table: "SupplierLedger",
                column: "SupplierPaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierLedger_SupplierReturnId",
                table: "SupplierLedger",
                column: "SupplierReturnId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPayments_Number",
                table: "SupplierPayments",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPayments_PaymentMethodId",
                table: "SupplierPayments",
                column: "PaymentMethodId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPayments_SupplierId",
                table: "SupplierPayments",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "UX_SupplierPayments_Idempotency",
                table: "SupplierPayments",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierReturnItems_BatchId",
                table: "SupplierReturnItems",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierReturnItems_MedicineId",
                table: "SupplierReturnItems",
                column: "MedicineId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierReturnItems_SupplierReturnId",
                table: "SupplierReturnItems",
                column: "SupplierReturnId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierReturns_LocationId",
                table: "SupplierReturns",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierReturns_Number",
                table: "SupplierReturns",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierReturns_SupplierId",
                table: "SupplierReturns",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "UX_SupplierReturns_Idempotency",
                table: "SupplierReturns",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_Code",
                table: "Suppliers",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_Name",
                table: "Suppliers",
                column: "Name");

            migrationBuilder.AddForeignKey(
                name: "FK_Charges_PatientMedicineIssues_PharmacyIssueId",
                table: "Charges",
                column: "PharmacyIssueId",
                principalTable: "PatientMedicineIssues",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Charges_PharmacySales_PharmacySaleId",
                table: "Charges",
                column: "PharmacySaleId",
                principalTable: "PharmacySales",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Charges_PatientMedicineIssues_PharmacyIssueId",
                table: "Charges");

            migrationBuilder.DropForeignKey(
                name: "FK_Charges_PharmacySales_PharmacySaleId",
                table: "Charges");

            migrationBuilder.DropTable(
                name: "InventoryTransactions");

            migrationBuilder.DropTable(
                name: "MedicinePriceChanges");

            migrationBuilder.DropTable(
                name: "MedicineReturnItems");

            migrationBuilder.DropTable(
                name: "PharmacySettings");

            migrationBuilder.DropTable(
                name: "StockAdjustments");

            migrationBuilder.DropTable(
                name: "StockBalances");

            migrationBuilder.DropTable(
                name: "StockReceiptItems");

            migrationBuilder.DropTable(
                name: "StockTransferItems");

            migrationBuilder.DropTable(
                name: "SupplierLedger");

            migrationBuilder.DropTable(
                name: "SupplierReturnItems");

            migrationBuilder.DropTable(
                name: "MedicineReturns");

            migrationBuilder.DropTable(
                name: "PatientMedicineIssueItems");

            migrationBuilder.DropTable(
                name: "PharmacySaleItems");

            migrationBuilder.DropTable(
                name: "StockTransfers");

            migrationBuilder.DropTable(
                name: "StockReceipts");

            migrationBuilder.DropTable(
                name: "SupplierPayments");

            migrationBuilder.DropTable(
                name: "SupplierReturns");

            migrationBuilder.DropTable(
                name: "PatientMedicineIssues");

            migrationBuilder.DropTable(
                name: "MedicineBatches");

            migrationBuilder.DropTable(
                name: "PharmacySales");

            migrationBuilder.DropTable(
                name: "Medicines");

            migrationBuilder.DropTable(
                name: "Suppliers");

            migrationBuilder.DropTable(
                name: "StockLocations");

            migrationBuilder.DropTable(
                name: "PharmacyLookups");

            migrationBuilder.DropIndex(
                name: "IX_Charges_PharmacyIssueId",
                table: "Charges");

            migrationBuilder.DropIndex(
                name: "IX_Charges_PharmacySaleId",
                table: "Charges");

            migrationBuilder.DropColumn(
                name: "PharmacyIssueId",
                table: "Charges");

            migrationBuilder.DropColumn(
                name: "PharmacySaleId",
                table: "Charges");

            migrationBuilder.DropSequence(
                name: "medicine_code_seq");

            migrationBuilder.DropSequence(
                name: "pharm_adjustment_seq");

            migrationBuilder.DropSequence(
                name: "pharm_issue_seq");

            migrationBuilder.DropSequence(
                name: "pharm_receipt_seq");

            migrationBuilder.DropSequence(
                name: "pharm_return_seq");

            migrationBuilder.DropSequence(
                name: "pharm_sale_seq");

            migrationBuilder.DropSequence(
                name: "pharm_supplier_payment_seq");

            migrationBuilder.DropSequence(
                name: "pharm_supplier_return_seq");

            migrationBuilder.DropSequence(
                name: "pharm_transfer_seq");

            migrationBuilder.DropSequence(
                name: "supplier_code_seq");
        }
    }
}
