using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuanLyChoThuePhongTroWeb.Migrations
{
    /// <inheritdoc />
    public partial class ThemMaChiNhanh : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MaChiNhanh",
                table: "chi_nhanh",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("UPDATE chi_nhanh SET \"MaChiNhanh\" = 'CN' || \"ChiNhanhId\" WHERE \"MaChiNhanh\" = '';");

            migrationBuilder.CreateIndex(
                name: "IX_chi_nhanh_MaChiNhanh",
                table: "chi_nhanh",
                column: "MaChiNhanh",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_chi_nhanh_MaChiNhanh",
                table: "chi_nhanh");

            migrationBuilder.DropColumn(
                name: "MaChiNhanh",
                table: "chi_nhanh");
        }
    }
}
