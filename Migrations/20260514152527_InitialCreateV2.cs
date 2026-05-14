using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace QuanLyChoThuePhongTroWeb.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreateV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "chi_nhanh",
                columns: table => new
                {
                    ChiNhanhId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenChiNhanh = table.Column<string>(type: "text", nullable: false),
                    DiaChi = table.Column<string>(type: "text", nullable: false),
                    MoTa = table.Column<string>(type: "text", nullable: false),
                    SoDienThoai = table.Column<string>(type: "text", nullable: false),
                    NgayTao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NgayCapNhat = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chi_nhanh", x => x.ChiNhanhId);
                });

            migrationBuilder.CreateTable(
                name: "nguoi_dung",
                columns: table => new
                {
                    NguoiDungId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenDangNhap = table.Column<string>(type: "text", nullable: false),
                    MatKhauHash = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    HoTen = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    NgayTao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LanDangNhapCuoi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NguoiThueId = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_nguoi_dung", x => x.NguoiDungId);
                });

            migrationBuilder.CreateTable(
                name: "dich_vu",
                columns: table => new
                {
                    DichVuId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChiNhanhId = table.Column<int>(type: "integer", nullable: false),
                    TenDichVu = table.Column<string>(type: "text", nullable: false),
                    GiaDichVu = table.Column<double>(type: "double precision", nullable: false),
                    DonVi = table.Column<string>(type: "text", nullable: false),
                    GhiChu = table.Column<string>(type: "text", nullable: false),
                    NgayTao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NgayCapNhat = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dich_vu", x => x.DichVuId);
                    table.ForeignKey(
                        name: "FK_dich_vu_chi_nhanh_ChiNhanhId",
                        column: x => x.ChiNhanhId,
                        principalTable: "chi_nhanh",
                        principalColumn: "ChiNhanhId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "phong_tro",
                columns: table => new
                {
                    PhongTroId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChiNhanhId = table.Column<int>(type: "integer", nullable: false),
                    SoPhong = table.Column<string>(type: "text", nullable: false),
                    TangLau = table.Column<int>(type: "integer", nullable: false),
                    GiaThue = table.Column<double>(type: "double precision", nullable: false),
                    DienTich = table.Column<double>(type: "double precision", nullable: false),
                    SoNguoiToiDa = table.Column<int>(type: "integer", nullable: false),
                    TrangThai = table.Column<int>(type: "integer", nullable: false),
                    MoTa = table.Column<string>(type: "text", nullable: false),
                    NgayTao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NgayCapNhat = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_phong_tro", x => x.PhongTroId);
                    table.ForeignKey(
                        name: "FK_phong_tro_chi_nhanh_ChiNhanhId",
                        column: x => x.ChiNhanhId,
                        principalTable: "chi_nhanh",
                        principalColumn: "ChiNhanhId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "nguoi_thue",
                columns: table => new
                {
                    NguoiThueId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HoVaTen = table.Column<string>(type: "text", nullable: false),
                    SoDienThoai = table.Column<string>(type: "text", nullable: false),
                    CCCD = table.Column<string>(type: "text", nullable: false),
                    NgayCapCCCD = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NoiCapCCCD = table.Column<string>(type: "text", nullable: false),
                    NgaySinh = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    QueQUan = table.Column<string>(type: "text", nullable: false),
                    GhiChu = table.Column<string>(type: "text", nullable: false),
                    NgayTao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NgayCapNhat = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    NguoiDungId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_nguoi_thue", x => x.NguoiThueId);
                    table.ForeignKey(
                        name: "FK_nguoi_thue_nguoi_dung_NguoiDungId",
                        column: x => x.NguoiDungId,
                        principalTable: "nguoi_dung",
                        principalColumn: "NguoiDungId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "dang_ky_dich_vu",
                columns: table => new
                {
                    DangKyDichVuId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PhongTroId = table.Column<int>(type: "integer", nullable: false),
                    DichVuId = table.Column<int>(type: "integer", nullable: false),
                    SoLuong = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dang_ky_dich_vu", x => x.DangKyDichVuId);
                    table.ForeignKey(
                        name: "FK_dang_ky_dich_vu_dich_vu_DichVuId",
                        column: x => x.DichVuId,
                        principalTable: "dich_vu",
                        principalColumn: "DichVuId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_dang_ky_dich_vu_phong_tro_PhongTroId",
                        column: x => x.PhongTroId,
                        principalTable: "phong_tro",
                        principalColumn: "PhongTroId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "dich_vu_dien_nuoc_cua_phong",
                columns: table => new
                {
                    DichVuDienNuocCuaPhongId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PhongTroId = table.Column<int>(type: "integer", nullable: false),
                    Thang = table.Column<int>(type: "integer", nullable: false),
                    Nam = table.Column<int>(type: "integer", nullable: false),
                    ChiSoDienCu = table.Column<double>(type: "double precision", nullable: false),
                    ChiSoDienMoi = table.Column<double>(type: "double precision", nullable: false),
                    DonGiaDien = table.Column<double>(type: "double precision", nullable: false),
                    ChiSoNuocCu = table.Column<double>(type: "double precision", nullable: false),
                    ChiSoNuocMoi = table.Column<double>(type: "double precision", nullable: false),
                    DonGiaNuoc = table.Column<double>(type: "double precision", nullable: false),
                    NgayTao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NgayCapNhat = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dich_vu_dien_nuoc_cua_phong", x => x.DichVuDienNuocCuaPhongId);
                    table.ForeignKey(
                        name: "FK_dich_vu_dien_nuoc_cua_phong_phong_tro_PhongTroId",
                        column: x => x.PhongTroId,
                        principalTable: "phong_tro",
                        principalColumn: "PhongTroId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "hop_dong",
                columns: table => new
                {
                    HopDongId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MaHopDong = table.Column<string>(type: "text", nullable: false),
                    PhongTroId = table.Column<int>(type: "integer", nullable: false),
                    NguoiThueId = table.Column<int>(type: "integer", nullable: false),
                    ThoiDiemBatDau = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ThoiDiemKetThuc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TienCocPhong = table.Column<double>(type: "double precision", nullable: false),
                    TienThuePhong = table.Column<double>(type: "double precision", nullable: false),
                    TrangThaiHopDong = table.Column<int>(type: "integer", nullable: false),
                    NgayTao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NgayCapNhat = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hop_dong", x => x.HopDongId);
                    table.ForeignKey(
                        name: "FK_hop_dong_nguoi_thue_NguoiThueId",
                        column: x => x.NguoiThueId,
                        principalTable: "nguoi_thue",
                        principalColumn: "NguoiThueId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_hop_dong_phong_tro_PhongTroId",
                        column: x => x.PhongTroId,
                        principalTable: "phong_tro",
                        principalColumn: "PhongTroId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "chi_tiet_thanh_vien_hop_dong",
                columns: table => new
                {
                    ChiTietThanhVienHopDongId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HopDongId = table.Column<int>(type: "integer", nullable: false),
                    NguoiThueId = table.Column<int>(type: "integer", nullable: false),
                    NgayVao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NgayChuyenDi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chi_tiet_thanh_vien_hop_dong", x => x.ChiTietThanhVienHopDongId);
                    table.ForeignKey(
                        name: "FK_chi_tiet_thanh_vien_hop_dong_hop_dong_HopDongId",
                        column: x => x.HopDongId,
                        principalTable: "hop_dong",
                        principalColumn: "HopDongId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_chi_tiet_thanh_vien_hop_dong_nguoi_thue_NguoiThueId",
                        column: x => x.NguoiThueId,
                        principalTable: "nguoi_thue",
                        principalColumn: "NguoiThueId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "hoa_don",
                columns: table => new
                {
                    HoaDonId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MaHoaDon = table.Column<string>(type: "text", nullable: false),
                    HopDongId = table.Column<int>(type: "integer", nullable: false),
                    Thang = table.Column<int>(type: "integer", nullable: false),
                    Nam = table.Column<int>(type: "integer", nullable: false),
                    TongTien = table.Column<double>(type: "double precision", nullable: false),
                    TrangThaiHoaDon = table.Column<int>(type: "integer", nullable: false),
                    NgayTao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NgayCapNhat = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DichVuDienNuocCuaPhongId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hoa_don", x => x.HoaDonId);
                    table.ForeignKey(
                        name: "FK_hoa_don_dich_vu_dien_nuoc_cua_phong_DichVuDienNuocCuaPhongId",
                        column: x => x.DichVuDienNuocCuaPhongId,
                        principalTable: "dich_vu_dien_nuoc_cua_phong",
                        principalColumn: "DichVuDienNuocCuaPhongId");
                    table.ForeignKey(
                        name: "FK_hoa_don_hop_dong_HopDongId",
                        column: x => x.HopDongId,
                        principalTable: "hop_dong",
                        principalColumn: "HopDongId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "chi_tiet_hoa_don",
                columns: table => new
                {
                    ChiTietHoaDonId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HoaDonId = table.Column<int>(type: "integer", nullable: false),
                    TenDichVu = table.Column<string>(type: "text", nullable: false),
                    DonGia = table.Column<double>(type: "double precision", nullable: false),
                    SoLuong = table.Column<int>(type: "integer", nullable: false),
                    TongTien = table.Column<double>(type: "double precision", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DichVuId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chi_tiet_hoa_don", x => x.ChiTietHoaDonId);
                    table.ForeignKey(
                        name: "FK_chi_tiet_hoa_don_dich_vu_DichVuId",
                        column: x => x.DichVuId,
                        principalTable: "dich_vu",
                        principalColumn: "DichVuId");
                    table.ForeignKey(
                        name: "FK_chi_tiet_hoa_don_hoa_don_HoaDonId",
                        column: x => x.HoaDonId,
                        principalTable: "hoa_don",
                        principalColumn: "HoaDonId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "lich_su_thanh_toan",
                columns: table => new
                {
                    LichSuThanhToanId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MaGiaoDich = table.Column<string>(type: "text", nullable: false),
                    HoaDonId = table.Column<int>(type: "integer", nullable: false),
                    NguoiXacNhanId = table.Column<int>(type: "integer", nullable: true),
                    SoTienThanhToan = table.Column<double>(type: "double precision", nullable: false),
                    PhuongThucThanhToan = table.Column<int>(type: "integer", nullable: false),
                    NgayThanhToan = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GhiChu = table.Column<string>(type: "text", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lich_su_thanh_toan", x => x.LichSuThanhToanId);
                    table.ForeignKey(
                        name: "FK_lich_su_thanh_toan_hoa_don_HoaDonId",
                        column: x => x.HoaDonId,
                        principalTable: "hoa_don",
                        principalColumn: "HoaDonId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_lich_su_thanh_toan_nguoi_dung_NguoiXacNhanId",
                        column: x => x.NguoiXacNhanId,
                        principalTable: "nguoi_dung",
                        principalColumn: "NguoiDungId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_chi_tiet_hoa_don_DichVuId",
                table: "chi_tiet_hoa_don",
                column: "DichVuId");

            migrationBuilder.CreateIndex(
                name: "IX_chi_tiet_hoa_don_HoaDonId",
                table: "chi_tiet_hoa_don",
                column: "HoaDonId");

            migrationBuilder.CreateIndex(
                name: "IX_chi_tiet_thanh_vien_hop_dong_HopDongId",
                table: "chi_tiet_thanh_vien_hop_dong",
                column: "HopDongId");

            migrationBuilder.CreateIndex(
                name: "IX_chi_tiet_thanh_vien_hop_dong_NguoiThueId",
                table: "chi_tiet_thanh_vien_hop_dong",
                column: "NguoiThueId");

            migrationBuilder.CreateIndex(
                name: "IX_dang_ky_dich_vu_DichVuId",
                table: "dang_ky_dich_vu",
                column: "DichVuId");

            migrationBuilder.CreateIndex(
                name: "IX_dang_ky_dich_vu_PhongTroId",
                table: "dang_ky_dich_vu",
                column: "PhongTroId");

            migrationBuilder.CreateIndex(
                name: "IX_dich_vu_ChiNhanhId",
                table: "dich_vu",
                column: "ChiNhanhId");

            migrationBuilder.CreateIndex(
                name: "IX_dich_vu_dien_nuoc_cua_phong_PhongTroId",
                table: "dich_vu_dien_nuoc_cua_phong",
                column: "PhongTroId");

            migrationBuilder.CreateIndex(
                name: "IX_hoa_don_DichVuDienNuocCuaPhongId",
                table: "hoa_don",
                column: "DichVuDienNuocCuaPhongId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_hoa_don_HopDongId",
                table: "hoa_don",
                column: "HopDongId");

            migrationBuilder.CreateIndex(
                name: "IX_hop_dong_NguoiThueId",
                table: "hop_dong",
                column: "NguoiThueId");

            migrationBuilder.CreateIndex(
                name: "IX_hop_dong_PhongTroId",
                table: "hop_dong",
                column: "PhongTroId");

            migrationBuilder.CreateIndex(
                name: "IX_lich_su_thanh_toan_HoaDonId",
                table: "lich_su_thanh_toan",
                column: "HoaDonId");

            migrationBuilder.CreateIndex(
                name: "IX_lich_su_thanh_toan_NguoiXacNhanId",
                table: "lich_su_thanh_toan",
                column: "NguoiXacNhanId");

            migrationBuilder.CreateIndex(
                name: "IX_nguoi_thue_NguoiDungId",
                table: "nguoi_thue",
                column: "NguoiDungId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_phong_tro_ChiNhanhId",
                table: "phong_tro",
                column: "ChiNhanhId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "chi_tiet_hoa_don");

            migrationBuilder.DropTable(
                name: "chi_tiet_thanh_vien_hop_dong");

            migrationBuilder.DropTable(
                name: "dang_ky_dich_vu");

            migrationBuilder.DropTable(
                name: "lich_su_thanh_toan");

            migrationBuilder.DropTable(
                name: "dich_vu");

            migrationBuilder.DropTable(
                name: "hoa_don");

            migrationBuilder.DropTable(
                name: "dich_vu_dien_nuoc_cua_phong");

            migrationBuilder.DropTable(
                name: "hop_dong");

            migrationBuilder.DropTable(
                name: "nguoi_thue");

            migrationBuilder.DropTable(
                name: "phong_tro");

            migrationBuilder.DropTable(
                name: "nguoi_dung");

            migrationBuilder.DropTable(
                name: "chi_nhanh");
        }
    }
}
