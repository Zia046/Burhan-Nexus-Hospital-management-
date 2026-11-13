using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BurhanNexus.HMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase3Finance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Data-safe: only new tables, sequences and columns. Existing services get Kind 'Other' (classified once at start-up)
            // and existing doctors get 'HospitalKeepsAll' until Admin configures a fee-sharing arrangement.
            migrationBuilder.CreateSequence(
                name: "adjustment_number_seq");

            migrationBuilder.CreateSequence(
                name: "charge_number_seq");

            migrationBuilder.CreateSequence(
                name: "deposit_number_seq");

            migrationBuilder.CreateSequence(
                name: "receipt_number_seq");

            migrationBuilder.CreateSequence(
                name: "refund_number_seq");

            migrationBuilder.CreateSequence(
                name: "settlement_number_seq");

            migrationBuilder.AddColumn<bool>(
                name: "AllowPartialPayment",
                table: "ServiceItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "DepartmentId",
                table: "ServiceItems",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "ServiceItems",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "ServiceItems",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Other");

            migrationBuilder.AddColumn<string>(
                name: "ShareType",
                table: "Doctors",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "HospitalKeepsAll");

            migrationBuilder.AddColumn<decimal>(
                name: "ShareValue",
                table: "Doctors",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "Counters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DepartmentId = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Counters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Counters_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DailyCashClosings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Scope = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: true),
                    CounterId = table.Column<int>(type: "integer", nullable: true),
                    ScopeName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    OpeningBalance = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    CashCollections = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    NonCashCollections = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    ReceiptCount = table.Column<int>(type: "integer", nullable: false),
                    CancelledCount = table.Column<int>(type: "integer", nullable: false),
                    CancelledAmount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    Refunds = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    DepositsSubmitted = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    DepositsConfirmed = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    Adjustments = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    ClosingBalance = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    Notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ClosedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClosedByUserId = table.Column<int>(type: "integer", nullable: true),
                    ClosedByUsername = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyCashClosings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FinanceSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    ReceiptPrefix = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CurrencySymbol = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    IssueZeroReceiptForFreeServices = table.Column<bool>(type: "boolean", nullable: false),
                    ShowCashierOnReceipt = table.Column<bool>(type: "boolean", nullable: false),
                    ReceiptFooter = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DefaultReceiptWidthMm = table.Column<int>(type: "integer", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceSettings", x => x.Id);
                    table.CheckConstraint("CK_FinanceSettings_Singleton", "\"Id\" = 1");
                });

            migrationBuilder.CreateTable(
                name: "FinancialAdjustments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AdjustmentNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Target = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: true),
                    DoctorId = table.Column<int>(type: "integer", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: true),
                    CreatedByUsername = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinancialAdjustments", x => x.Id);
                    table.CheckConstraint("CK_FinancialAdjustments_NonZero", "\"Amount\" <> 0");
                    table.ForeignKey(
                        name: "FK_FinancialAdjustments_Doctors_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "Doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinancialAdjustments_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaymentMethods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    CountsAsCashInHand = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresReference = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentMethods", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ServiceFees",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ServiceItemId = table.Column<int>(type: "integer", nullable: false),
                    Fee = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: true),
                    CreatedByUsername = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Note = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceFees", x => x.Id);
                    table.CheckConstraint("CK_ServiceFees_NonNegative", "\"Fee\" >= 0");
                    table.ForeignKey(
                        name: "FK_ServiceFees_ServiceItems_ServiceItemId",
                        column: x => x.ServiceItemId,
                        principalTable: "ServiceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CashDeposits",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DepositNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Username = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CounterId = table.Column<int>(type: "integer", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    ReceivedAmount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    Notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SubmittedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ReviewedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewedByUserId = table.Column<int>(type: "integer", nullable: true),
                    ReviewedByUsername = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ReviewRemarks = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashDeposits", x => x.Id);
                    table.CheckConstraint("CK_CashDeposits_Positive", "\"Amount\" > 0 AND (\"ReceivedAmount\" IS NULL OR (\"ReceivedAmount\" > 0 AND \"ReceivedAmount\" <= \"Amount\"))");
                    table.ForeignKey(
                        name: "FK_CashDeposits_Counters_CounterId",
                        column: x => x.CounterId,
                        principalTable: "Counters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashDeposits_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CounterAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CounterId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    AssignedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AssignedBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    EndedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndedBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CounterAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CounterAssignments_Counters_CounterId",
                        column: x => x.CounterId,
                        principalTable: "Counters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CounterAssignments_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ServiceCounters",
                columns: table => new
                {
                    ServiceItemId = table.Column<int>(type: "integer", nullable: false),
                    CounterId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceCounters", x => new { x.ServiceItemId, x.CounterId });
                    table.ForeignKey(
                        name: "FK_ServiceCounters_Counters_CounterId",
                        column: x => x.CounterId,
                        principalTable: "Counters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ServiceCounters_ServiceItems_ServiceItemId",
                        column: x => x.ServiceItemId,
                        principalTable: "ServiceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DoctorSettlements",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SettlementNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DoctorId = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    PaymentMethodId = table.Column<int>(type: "integer", nullable: false),
                    PaymentReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    PaidUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    RecordedByUserId = table.Column<int>(type: "integer", nullable: true),
                    RecordedByUsername = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DoctorSettlements", x => x.Id);
                    table.CheckConstraint("CK_DoctorSettlements_Positive", "\"Amount\" > 0");
                    table.ForeignKey(
                        name: "FK_DoctorSettlements_Doctors_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "Doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DoctorSettlements_PaymentMethods_PaymentMethodId",
                        column: x => x.PaymentMethodId,
                        principalTable: "PaymentMethods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Receipts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReceiptNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<long>(type: "bigint", nullable: true),
                    PayerName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    CounterId = table.Column<int>(type: "integer", nullable: false),
                    CashierUserId = table.Column<int>(type: "integer", nullable: false),
                    CashierUsername = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CashierName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    PaymentMethodId = table.Column<int>(type: "integer", nullable: false),
                    PaymentReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TotalFee = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    TotalDiscount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    TotalNet = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    AmountPaid = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    BalanceAfter = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    RefundedAmount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Workstation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CancelledUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledByUserId = table.Column<int>(type: "integer", nullable: true),
                    CancelledByUsername = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CancelReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Receipts", x => x.Id);
                    table.CheckConstraint("CK_Receipts_Amounts", "\"AmountPaid\" >= 0 AND \"RefundedAmount\" >= 0 AND \"RefundedAmount\" <= \"AmountPaid\"");
                    table.ForeignKey(
                        name: "FK_Receipts_Counters_CounterId",
                        column: x => x.CounterId,
                        principalTable: "Counters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Receipts_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Receipts_PaymentMethods_PaymentMethodId",
                        column: x => x.PaymentMethodId,
                        principalTable: "PaymentMethods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Receipts_Users_CashierUserId",
                        column: x => x.CashierUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Charges",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChargeNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PatientId = table.Column<long>(type: "bigint", nullable: true),
                    EncounterId = table.Column<long>(type: "bigint", nullable: true),
                    ServiceItemId = table.Column<int>(type: "integer", nullable: false),
                    ServiceKind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ServiceName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    DepartmentId = table.Column<int>(type: "integer", nullable: true),
                    DoctorId = table.Column<int>(type: "integer", nullable: true),
                    DoctorName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Fee = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    Discount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    NetAmount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    PaidAmount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    RefundedAmount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    AllowPartialPayment = table.Column<bool>(type: "boolean", nullable: false),
                    DiscountReason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DoctorShareType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DoctorShareValue = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    DoctorShareAmount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    OriginReceiptId = table.Column<long>(type: "bigint", nullable: true),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: true),
                    CreatedByUsername = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Charges", x => x.Id);
                    table.CheckConstraint("CK_Charges_Amounts", "\"Fee\" >= 0 AND \"Discount\" >= 0 AND \"Discount\" <= \"Fee\" AND \"NetAmount\" = \"Fee\" - \"Discount\"");
                    table.CheckConstraint("CK_Charges_Paid", "\"PaidAmount\" >= 0 AND \"PaidAmount\" <= \"NetAmount\" AND \"RefundedAmount\" >= 0 AND \"RefundedAmount\" <= \"PaidAmount\"");
                    table.ForeignKey(
                        name: "FK_Charges_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Charges_Doctors_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "Doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Charges_PatientEncounters_EncounterId",
                        column: x => x.EncounterId,
                        principalTable: "PatientEncounters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Charges_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Charges_Receipts_OriginReceiptId",
                        column: x => x.OriginReceiptId,
                        principalTable: "Receipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Charges_ServiceItems_ServiceItemId",
                        column: x => x.ServiceItemId,
                        principalTable: "ServiceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReceiptPrints",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReceiptId = table.Column<long>(type: "bigint", nullable: false),
                    CopyNumber = table.Column<int>(type: "integer", nullable: false),
                    Format = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Method = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    PrintedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PrintedByUserId = table.Column<int>(type: "integer", nullable: true),
                    PrintedByUsername = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Workstation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceiptPrints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReceiptPrints_Receipts_ReceiptId",
                        column: x => x.ReceiptId,
                        principalTable: "Receipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Refunds",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RefundNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ReceiptId = table.Column<long>(type: "bigint", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    PaymentMethodId = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RequestedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RequestedByUserId = table.Column<int>(type: "integer", nullable: true),
                    RequestedByUsername = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    DecidedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DecidedByUserId = table.Column<int>(type: "integer", nullable: true),
                    DecidedByUsername = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    DecisionNote = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Refunds", x => x.Id);
                    table.CheckConstraint("CK_Refunds_Positive", "\"Amount\" > 0");
                    table.ForeignKey(
                        name: "FK_Refunds_PaymentMethods_PaymentMethodId",
                        column: x => x.PaymentMethodId,
                        principalTable: "PaymentMethods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Refunds_Receipts_ReceiptId",
                        column: x => x.ReceiptId,
                        principalTable: "Receipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReceiptLines",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReceiptId = table.Column<long>(type: "bigint", nullable: false),
                    ChargeId = table.Column<long>(type: "bigint", nullable: false),
                    ServiceName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    DoctorName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    DepartmentId = table.Column<int>(type: "integer", nullable: true),
                    DoctorId = table.Column<int>(type: "integer", nullable: true),
                    Fee = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    Discount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    NetAmount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    PreviouslyPaid = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    BalanceAfter = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    DoctorShare = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    RefundedAmount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceiptLines", x => x.Id);
                    table.CheckConstraint("CK_ReceiptLines_Amounts", "\"Amount\" >= 0 AND \"DoctorShare\" >= 0 AND \"DoctorShare\" <= \"Amount\" AND \"RefundedAmount\" >= 0 AND \"RefundedAmount\" <= \"Amount\"");
                    table.ForeignKey(
                        name: "FK_ReceiptLines_Charges_ChargeId",
                        column: x => x.ChargeId,
                        principalTable: "Charges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReceiptLines_Receipts_ReceiptId",
                        column: x => x.ReceiptId,
                        principalTable: "Receipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CashierLedger",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    CounterId = table.Column<int>(type: "integer", nullable: true),
                    EntryType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    PaymentMethodId = table.Column<int>(type: "integer", nullable: true),
                    AffectsCashInHand = table.Column<bool>(type: "boolean", nullable: false),
                    ReceiptId = table.Column<long>(type: "bigint", nullable: true),
                    RefundId = table.Column<long>(type: "bigint", nullable: true),
                    DepositId = table.Column<long>(type: "bigint", nullable: true),
                    AdjustmentId = table.Column<long>(type: "bigint", nullable: true),
                    Reference = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: true),
                    CreatedByUsername = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashierLedger", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CashierLedger_CashDeposits_DepositId",
                        column: x => x.DepositId,
                        principalTable: "CashDeposits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashierLedger_Counters_CounterId",
                        column: x => x.CounterId,
                        principalTable: "Counters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashierLedger_FinancialAdjustments_AdjustmentId",
                        column: x => x.AdjustmentId,
                        principalTable: "FinancialAdjustments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashierLedger_PaymentMethods_PaymentMethodId",
                        column: x => x.PaymentMethodId,
                        principalTable: "PaymentMethods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashierLedger_Receipts_ReceiptId",
                        column: x => x.ReceiptId,
                        principalTable: "Receipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashierLedger_Refunds_RefundId",
                        column: x => x.RefundId,
                        principalTable: "Refunds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashierLedger_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DoctorLedger",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DoctorId = table.Column<int>(type: "integer", nullable: false),
                    EntryType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    GrossAmount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    HospitalShare = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    ReceiptId = table.Column<long>(type: "bigint", nullable: true),
                    ReceiptLineId = table.Column<long>(type: "bigint", nullable: true),
                    RefundId = table.Column<long>(type: "bigint", nullable: true),
                    SettlementId = table.Column<long>(type: "bigint", nullable: true),
                    AdjustmentId = table.Column<long>(type: "bigint", nullable: true),
                    Reference = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: true),
                    CreatedByUsername = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DoctorLedger", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DoctorLedger_DoctorSettlements_SettlementId",
                        column: x => x.SettlementId,
                        principalTable: "DoctorSettlements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DoctorLedger_Doctors_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "Doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DoctorLedger_FinancialAdjustments_AdjustmentId",
                        column: x => x.AdjustmentId,
                        principalTable: "FinancialAdjustments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DoctorLedger_ReceiptLines_ReceiptLineId",
                        column: x => x.ReceiptLineId,
                        principalTable: "ReceiptLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DoctorLedger_Receipts_ReceiptId",
                        column: x => x.ReceiptId,
                        principalTable: "Receipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DoctorLedger_Refunds_RefundId",
                        column: x => x.RefundId,
                        principalTable: "Refunds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceItems_DepartmentId",
                table: "ServiceItems",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_CashDeposits_CounterId",
                table: "CashDeposits",
                column: "CounterId");

            migrationBuilder.CreateIndex(
                name: "IX_CashDeposits_DepositNumber",
                table: "CashDeposits",
                column: "DepositNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CashDeposits_UserId_Status",
                table: "CashDeposits",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CashierLedger_AdjustmentId",
                table: "CashierLedger",
                column: "AdjustmentId");

            migrationBuilder.CreateIndex(
                name: "IX_CashierLedger_CounterId_BusinessDate",
                table: "CashierLedger",
                columns: new[] { "CounterId", "BusinessDate" });

            migrationBuilder.CreateIndex(
                name: "IX_CashierLedger_DepositId",
                table: "CashierLedger",
                column: "DepositId");

            migrationBuilder.CreateIndex(
                name: "IX_CashierLedger_PaymentMethodId",
                table: "CashierLedger",
                column: "PaymentMethodId");

            migrationBuilder.CreateIndex(
                name: "IX_CashierLedger_ReceiptId",
                table: "CashierLedger",
                column: "ReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_CashierLedger_RefundId",
                table: "CashierLedger",
                column: "RefundId");

            migrationBuilder.CreateIndex(
                name: "IX_CashierLedger_UserId_BusinessDate",
                table: "CashierLedger",
                columns: new[] { "UserId", "BusinessDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Charges_ChargeNumber",
                table: "Charges",
                column: "ChargeNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Charges_DepartmentId",
                table: "Charges",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Charges_DoctorId_BusinessDate",
                table: "Charges",
                columns: new[] { "DoctorId", "BusinessDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Charges_EncounterId",
                table: "Charges",
                column: "EncounterId");

            migrationBuilder.CreateIndex(
                name: "IX_Charges_OriginReceiptId",
                table: "Charges",
                column: "OriginReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_Charges_PatientId_Status",
                table: "Charges",
                columns: new[] { "PatientId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Charges_ServiceItemId",
                table: "Charges",
                column: "ServiceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CounterAssignments_UserId",
                table: "CounterAssignments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "UX_CounterAssignments_Current",
                table: "CounterAssignments",
                columns: new[] { "CounterId", "UserId" },
                unique: true,
                filter: "\"EndedUtc\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Counters_Code",
                table: "Counters",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Counters_DepartmentId",
                table: "Counters",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Counters_Name",
                table: "Counters",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DailyCashClosings_BusinessDate_Scope_UserId_CounterId",
                table: "DailyCashClosings",
                columns: new[] { "BusinessDate", "Scope", "UserId", "CounterId" });

            migrationBuilder.CreateIndex(
                name: "IX_DoctorLedger_AdjustmentId",
                table: "DoctorLedger",
                column: "AdjustmentId");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorLedger_DoctorId_BusinessDate",
                table: "DoctorLedger",
                columns: new[] { "DoctorId", "BusinessDate" });

            migrationBuilder.CreateIndex(
                name: "IX_DoctorLedger_ReceiptId",
                table: "DoctorLedger",
                column: "ReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorLedger_ReceiptLineId",
                table: "DoctorLedger",
                column: "ReceiptLineId");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorLedger_RefundId",
                table: "DoctorLedger",
                column: "RefundId");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorLedger_SettlementId",
                table: "DoctorLedger",
                column: "SettlementId");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorSettlements_DoctorId_BusinessDate",
                table: "DoctorSettlements",
                columns: new[] { "DoctorId", "BusinessDate" });

            migrationBuilder.CreateIndex(
                name: "IX_DoctorSettlements_PaymentMethodId",
                table: "DoctorSettlements",
                column: "PaymentMethodId");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorSettlements_SettlementNumber",
                table: "DoctorSettlements",
                column: "SettlementNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinancialAdjustments_AdjustmentNumber",
                table: "FinancialAdjustments",
                column: "AdjustmentNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinancialAdjustments_DoctorId",
                table: "FinancialAdjustments",
                column: "DoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialAdjustments_UserId",
                table: "FinancialAdjustments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentMethods_Code",
                table: "PaymentMethods",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentMethods_Name",
                table: "PaymentMethods",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptLines_ChargeId",
                table: "ReceiptLines",
                column: "ChargeId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptLines_ReceiptId",
                table: "ReceiptLines",
                column: "ReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptPrints_ReceiptId_CopyNumber",
                table: "ReceiptPrints",
                columns: new[] { "ReceiptId", "CopyNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Receipts_BusinessDate_CounterId",
                table: "Receipts",
                columns: new[] { "BusinessDate", "CounterId" });

            migrationBuilder.CreateIndex(
                name: "IX_Receipts_CashierUserId_BusinessDate",
                table: "Receipts",
                columns: new[] { "CashierUserId", "BusinessDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Receipts_CounterId",
                table: "Receipts",
                column: "CounterId");

            migrationBuilder.CreateIndex(
                name: "IX_Receipts_PatientId",
                table: "Receipts",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_Receipts_PaymentMethodId",
                table: "Receipts",
                column: "PaymentMethodId");

            migrationBuilder.CreateIndex(
                name: "UX_Receipts_Idempotency",
                table: "Receipts",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Receipts_Number",
                table: "Receipts",
                column: "ReceiptNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_PaymentMethodId",
                table: "Refunds",
                column: "PaymentMethodId");

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_ReceiptId_Status",
                table: "Refunds",
                columns: new[] { "ReceiptId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_RefundNumber",
                table: "Refunds",
                column: "RefundNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceCounters_CounterId",
                table: "ServiceCounters",
                column: "CounterId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceFees_ServiceItemId_EffectiveFrom",
                table: "ServiceFees",
                columns: new[] { "ServiceItemId", "EffectiveFrom" });

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceItems_Departments_DepartmentId",
                table: "ServiceItems",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ServiceItems_Departments_DepartmentId",
                table: "ServiceItems");

            migrationBuilder.DropTable(
                name: "CashierLedger");

            migrationBuilder.DropTable(
                name: "CounterAssignments");

            migrationBuilder.DropTable(
                name: "DailyCashClosings");

            migrationBuilder.DropTable(
                name: "DoctorLedger");

            migrationBuilder.DropTable(
                name: "FinanceSettings");

            migrationBuilder.DropTable(
                name: "ReceiptPrints");

            migrationBuilder.DropTable(
                name: "ServiceCounters");

            migrationBuilder.DropTable(
                name: "ServiceFees");

            migrationBuilder.DropTable(
                name: "CashDeposits");

            migrationBuilder.DropTable(
                name: "DoctorSettlements");

            migrationBuilder.DropTable(
                name: "FinancialAdjustments");

            migrationBuilder.DropTable(
                name: "ReceiptLines");

            migrationBuilder.DropTable(
                name: "Refunds");

            migrationBuilder.DropTable(
                name: "Charges");

            migrationBuilder.DropTable(
                name: "Receipts");

            migrationBuilder.DropTable(
                name: "Counters");

            migrationBuilder.DropTable(
                name: "PaymentMethods");

            migrationBuilder.DropIndex(
                name: "IX_ServiceItems_DepartmentId",
                table: "ServiceItems");

            migrationBuilder.DropColumn(
                name: "AllowPartialPayment",
                table: "ServiceItems");

            migrationBuilder.DropColumn(
                name: "DepartmentId",
                table: "ServiceItems");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "ServiceItems");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "ServiceItems");

            migrationBuilder.DropColumn(
                name: "ShareType",
                table: "Doctors");

            migrationBuilder.DropColumn(
                name: "ShareValue",
                table: "Doctors");

            migrationBuilder.DropSequence(
                name: "adjustment_number_seq");

            migrationBuilder.DropSequence(
                name: "charge_number_seq");

            migrationBuilder.DropSequence(
                name: "deposit_number_seq");

            migrationBuilder.DropSequence(
                name: "receipt_number_seq");

            migrationBuilder.DropSequence(
                name: "refund_number_seq");

            migrationBuilder.DropSequence(
                name: "settlement_number_seq");
        }
    }
}
