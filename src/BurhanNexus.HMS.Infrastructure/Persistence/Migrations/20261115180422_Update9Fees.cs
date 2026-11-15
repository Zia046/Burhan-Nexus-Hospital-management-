using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BurhanNexus.HMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Update9Fees : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "WaiverAppointmentId",
                table: "Refunds",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "AppointmentId",
                table: "DoctorLedger",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AmountPaid",
                table: "Appointments",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ChargeId",
                table: "Appointments",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "IdempotencyKey",
                table: "Appointments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentMethodName",
                table: "Appointments",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentStatus",
                table: "Appointments",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "NotRecorded"); // existing appointments: fee not recorded at booking (paid at the cashier, if at all)

            migrationBuilder.AddColumn<long>(
                name: "ReceiptId",
                table: "Appointments",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "WaivedAfterPayment",
                table: "Appointments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "WaivedAmount",
                table: "Appointments",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WaivedByUserId",
                table: "Appointments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WaivedByUsername",
                table: "Appointments",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WaivedUtc",
                table: "Appointments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WaiverDoctorId",
                table: "Appointments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WaiverDoctorName",
                table: "Appointments",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WaiverReason",
                table: "Appointments",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "WaiverRefundId",
                table: "Appointments",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_Refunds_WaiverAppointment",
                table: "Refunds",
                column: "WaiverAppointmentId",
                unique: true,
                filter: "\"WaiverAppointmentId\" IS NOT NULL AND \"Status\" <> 'Rejected'");

            migrationBuilder.CreateIndex(
                name: "UX_DoctorLedger_FeeWaiver",
                table: "DoctorLedger",
                column: "AppointmentId",
                unique: true,
                filter: "\"AppointmentId\" IS NOT NULL AND \"EntryType\" = 'FeeWaiver'");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_PaymentStatus_AppointmentDate",
                table: "Appointments",
                columns: new[] { "PaymentStatus", "AppointmentDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_ReceiptId",
                table: "Appointments",
                column: "ReceiptId");

            migrationBuilder.CreateIndex(
                name: "UX_Appointments_Idempotency",
                table: "Appointments",
                column: "IdempotencyKey",
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Appointments_Receipts_ReceiptId",
                table: "Appointments",
                column: "ReceiptId",
                principalTable: "Receipts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Appointments_Receipts_ReceiptId",
                table: "Appointments");

            migrationBuilder.DropIndex(
                name: "UX_Refunds_WaiverAppointment",
                table: "Refunds");

            migrationBuilder.DropIndex(
                name: "UX_DoctorLedger_FeeWaiver",
                table: "DoctorLedger");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_PaymentStatus_AppointmentDate",
                table: "Appointments");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_ReceiptId",
                table: "Appointments");

            migrationBuilder.DropIndex(
                name: "UX_Appointments_Idempotency",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "WaiverAppointmentId",
                table: "Refunds");

            migrationBuilder.DropColumn(
                name: "AppointmentId",
                table: "DoctorLedger");

            migrationBuilder.DropColumn(
                name: "AmountPaid",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "ChargeId",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "PaymentMethodName",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "PaymentStatus",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "ReceiptId",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "WaivedAfterPayment",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "WaivedAmount",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "WaivedByUserId",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "WaivedByUsername",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "WaivedUtc",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "WaiverDoctorId",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "WaiverDoctorName",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "WaiverReason",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "WaiverRefundId",
                table: "Appointments");
        }
    }
}
