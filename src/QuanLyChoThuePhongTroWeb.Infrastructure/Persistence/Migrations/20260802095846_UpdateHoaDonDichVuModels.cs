using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuanLyChoThuePhongTroWeb.Migrations
{
    /// <inheritdoc />
    public partial class UpdateHoaDonDichVuModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_hoa_don_DichVuDienNuocCuaPhongId",
                table: "hoa_don");

            migrationBuilder.DropIndex(
                name: "IX_hoa_don_HopDongId",
                table: "hoa_don");

            migrationBuilder.AddColumn<int>(
                name: "LoaiDichVu",
                table: "dich_vu",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_hoa_don_DichVuDienNuocCuaPhongId",
                table: "hoa_don",
                column: "DichVuDienNuocCuaPhongId");

            migrationBuilder.CreateIndex(
                name: "IX_hoa_don_HopDongId_Thang_Nam",
                table: "hoa_don",
                columns: new[] { "HopDongId", "Thang", "Nam" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_hoa_don_DichVuDienNuocCuaPhongId",
                table: "hoa_don");

            migrationBuilder.DropIndex(
                name: "IX_hoa_don_HopDongId_Thang_Nam",
                table: "hoa_don");

            migrationBuilder.DropColumn(
                name: "LoaiDichVu",
                table: "dich_vu");

            migrationBuilder.CreateIndex(
                name: "IX_hoa_don_DichVuDienNuocCuaPhongId",
                table: "hoa_don",
                column: "DichVuDienNuocCuaPhongId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_hoa_don_HopDongId",
                table: "hoa_don",
                column: "HopDongId");
        }
    }
}
