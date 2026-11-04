using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRoomDepositAmount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "TienCocPhong",
                table: "phong_tro",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddCheckConstraint(
                name: "CK_PhongTro_TienCocPhong",
                table: "phong_tro",
                sql: "\"TienCocPhong\" >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PhongTro_TienCocPhong",
                table: "phong_tro");

            migrationBuilder.DropColumn(
                name: "TienCocPhong",
                table: "phong_tro");
        }
    }
}
