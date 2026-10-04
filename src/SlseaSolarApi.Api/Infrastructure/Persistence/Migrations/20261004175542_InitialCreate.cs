using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SlseaSolarApi.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Provinces",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Provinces", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Districts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ProvinceId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Districts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Districts_Provinces_ProvinceId",
                        column: x => x.ProvinceId,
                        principalTable: "Provinces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GridSubstations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CapacityKw = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DistrictId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GridSubstations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GridSubstations_Districts_DistrictId",
                        column: x => x.DistrictId,
                        principalTable: "Districts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FullName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ProvinceId = table.Column<int>(type: "integer", nullable: true),
                    DistrictId = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_Districts_DistrictId",
                        column: x => x.DistrictId,
                        principalTable: "Districts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Users_Provinces_ProvinceId",
                        column: x => x.ProvinceId,
                        principalTable: "Provinces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SolarInstallations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Reference = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    MeterId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    InverterId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CapacityKw = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CommissionedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    GridSubstationId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SolarInstallations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SolarInstallations_GridSubstations_GridSubstationId",
                        column: x => x.GridSubstationId,
                        principalTable: "GridSubstations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GenerationReadings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SolarInstallationId = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PowerKw = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    EnergyKwh = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    Voltage = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IngestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SequenceNo = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GenerationReadings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GenerationReadings_SolarInstallations_SolarInstallationId",
                        column: x => x.SolarInstallationId,
                        principalTable: "SolarInstallations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Districts_Code",
                table: "Districts",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Districts_ProvinceId_Name",
                table: "Districts",
                columns: new[] { "ProvinceId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GenerationReadings_SolarInstallationId_SequenceNo",
                table: "GenerationReadings",
                columns: new[] { "SolarInstallationId", "SequenceNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GenerationReadings_SolarInstallationId_Timestamp",
                table: "GenerationReadings",
                columns: new[] { "SolarInstallationId", "Timestamp" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GridSubstations_Code",
                table: "GridSubstations",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GridSubstations_DistrictId",
                table: "GridSubstations",
                column: "DistrictId");

            migrationBuilder.CreateIndex(
                name: "IX_Provinces_Code",
                table: "Provinces",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Provinces_Name",
                table: "Provinces",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SolarInstallations_GridSubstationId",
                table: "SolarInstallations",
                column: "GridSubstationId");

            migrationBuilder.CreateIndex(
                name: "IX_SolarInstallations_MeterId",
                table: "SolarInstallations",
                column: "MeterId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SolarInstallations_Reference",
                table: "SolarInstallations",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_DistrictId",
                table: "Users",
                column: "DistrictId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_ProvinceId",
                table: "Users",
                column: "ProvinceId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GenerationReadings");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "SolarInstallations");

            migrationBuilder.DropTable(
                name: "GridSubstations");

            migrationBuilder.DropTable(
                name: "Districts");

            migrationBuilder.DropTable(
                name: "Provinces");
        }
    }
}
