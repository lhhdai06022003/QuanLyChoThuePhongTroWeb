using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReservationContractCredit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "GiaThueDaChot",
                table: "yeu_cau_giu_cho",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "can_tru_tien_giu_cho_hoa_don",
                columns: table => new
                {
                    CanTruTienGiuChoHoaDonId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ApDungTienGiuChoVaoTienCocId = table.Column<int>(type: "integer", nullable: false),
                    HoaDonId = table.Column<int>(type: "integer", nullable: false),
                    SoTienCanTru = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    NgayCanTru = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_can_tru_tien_giu_cho_hoa_don", x => x.CanTruTienGiuChoHoaDonId);
                    table.CheckConstraint("CK_CanTruGiuCho_SoTien", "\"SoTienCanTru\" > 0");
                    table.ForeignKey(
                        name: "FK_can_tru_tien_giu_cho_hoa_don_ap_dung_tien_giu_cho_vao_tien_~",
                        column: x => x.ApDungTienGiuChoVaoTienCocId,
                        principalTable: "ap_dung_tien_giu_cho_vao_tien_coc",
                        principalColumn: "ApDungTienGiuChoVaoTienCocId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_can_tru_tien_giu_cho_hoa_don_hoa_don_HoaDonId",
                        column: x => x.HoaDonId,
                        principalTable: "hoa_don",
                        principalColumn: "HoaDonId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_can_tru_tien_giu_cho_hoa_don_ApDungTienGiuChoVaoTienCocId",
                table: "can_tru_tien_giu_cho_hoa_don",
                column: "ApDungTienGiuChoVaoTienCocId");

            migrationBuilder.CreateIndex(
                name: "IX_can_tru_tien_giu_cho_hoa_don_HoaDonId",
                table: "can_tru_tien_giu_cho_hoa_don",
                column: "HoaDonId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "can_tru_tien_giu_cho_hoa_don");

            migrationBuilder.DropColumn(
                name: "GiaThueDaChot",
                table: "yeu_cau_giu_cho");
        }
    }
}
