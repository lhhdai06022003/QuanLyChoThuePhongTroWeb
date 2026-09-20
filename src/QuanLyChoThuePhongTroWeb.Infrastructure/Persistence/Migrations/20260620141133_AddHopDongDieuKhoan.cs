using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace QuanLyChoThuePhongTroWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddHopDongDieuKhoan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "dieu_khoan_mau",
                columns: table => new
                {
                    DieuKhoanMauId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TieuDe = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    NoiDung = table.Column<string>(type: "text", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    NgayTao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NgayCapNhat = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dieu_khoan_mau", x => x.DieuKhoanMauId);
                });

            migrationBuilder.CreateTable(
                name: "hop_dong_dieu_khoan",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HopDongId = table.Column<int>(type: "integer", nullable: false),
                    TieuDe = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    NoiDung = table.Column<string>(type: "text", nullable: false),
                    ThuTu = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hop_dong_dieu_khoan", x => x.Id);
                    table.ForeignKey(
                        name: "FK_hop_dong_dieu_khoan_hop_dong_HopDongId",
                        column: x => x.HopDongId,
                        principalTable: "hop_dong",
                        principalColumn: "HopDongId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_hop_dong_dieu_khoan_HopDongId",
                table: "hop_dong_dieu_khoan",
                column: "HopDongId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dieu_khoan_mau");

            migrationBuilder.DropTable(
                name: "hop_dong_dieu_khoan");
        }
    }
}
