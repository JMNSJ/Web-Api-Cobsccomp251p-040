using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SlseaSolarApi.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceSecretHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeviceSecretHash",
                table: "SolarInstallations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeviceSecretHash",
                table: "SolarInstallations");
        }
    }
}
