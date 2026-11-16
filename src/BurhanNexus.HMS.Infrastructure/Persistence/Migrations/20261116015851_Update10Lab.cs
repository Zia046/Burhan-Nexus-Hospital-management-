using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BurhanNexus.HMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Update10Lab : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "SimpleWorkflow",
                table: "LabSettings",
                type: "boolean",
                nullable: false,
                defaultValue: true); // update 10: the simple laboratory workflow is the default (Lab Settings can switch it off)

            migrationBuilder.AddColumn<long>(
                name: "BookingReceiptId",
                table: "LabOrders",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SimpleWorkflow",
                table: "LabSettings");

            migrationBuilder.DropColumn(
                name: "BookingReceiptId",
                table: "LabOrders");
        }
    }
}
