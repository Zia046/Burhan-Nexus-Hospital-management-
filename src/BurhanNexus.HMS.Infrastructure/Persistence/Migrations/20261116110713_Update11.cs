using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BurhanNexus.HMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Update11 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "DoctorSettlements",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "ToDoctor"); // existing settlements were all paid to the doctor

            migrationBuilder.AddColumn<string>(
                name: "ReceivedBy",
                table: "DoctorSettlements",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Kind",
                table: "DoctorSettlements");

            migrationBuilder.DropColumn(
                name: "ReceivedBy",
                table: "DoctorSettlements");
        }
    }
}
