using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace QuanLyChoThuePhongTroWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddPublicViewingAndReservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "DuocDangTin",
                table: "phong_tro",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "MaCongKhai",
                table: "phong_tro",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NguoiDangTinId",
                table: "phong_tro",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TieuDeDangTin",
                table: "phong_tro",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "anh_phong_tro",
                columns: table => new
                {
                    AnhPhongTroId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PhongTroId = table.Column<int>(type: "integer", nullable: false),
                    Url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    PublicId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ThuTuHienThi = table.Column<int>(type: "integer", nullable: false),
                    LaAnhDaiDien = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ChuThich = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    NguoiTaiLenId = table.Column<int>(type: "integer", nullable: false),
                    NgayTaiLen = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_anh_phong_tro", x => x.AnhPhongTroId);
                    table.ForeignKey(
                        name: "FK_anh_phong_tro_nguoi_dung_NguoiTaiLenId",
                        column: x => x.NguoiTaiLenId,
                        principalTable: "nguoi_dung",
                        principalColumn: "NguoiDungId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_anh_phong_tro_phong_tro_PhongTroId",
                        column: x => x.PhongTroId,
                        principalTable: "phong_tro",
                        principalColumn: "PhongTroId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "khach_vang_lai",
                columns: table => new
                {
                    KhachVangLaiId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NguoiDungId = table.Column<int>(type: "integer", nullable: false),
                    HoTen = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    EmailNormalized = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    SoDienThoai = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DaXacMinhEmail = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    NgayTao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NgayCapNhat = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_khach_vang_lai", x => x.KhachVangLaiId);
                    table.ForeignKey(
                        name: "FK_khach_vang_lai_nguoi_dung_NguoiDungId",
                        column: x => x.NguoiDungId,
                        principalTable: "nguoi_dung",
                        principalColumn: "NguoiDungId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "khung_gio_xem_phong",
                columns: table => new
                {
                    KhungGioXemPhongId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PhongTroId = table.Column<int>(type: "integer", nullable: false),
                    ThoiGianBatDau = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ThoiGianKetThuc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SoLuongToiDa = table.Column<int>(type: "integer", nullable: false),
                    NguoiPhuTrachId = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    NguoiTaoId = table.Column<int>(type: "integer", nullable: false),
                    NgayTao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NgayCapNhat = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_khung_gio_xem_phong", x => x.KhungGioXemPhongId);
                    table.CheckConstraint("CK_KhungGioXemPhong_SoLuongToiDa", "\"SoLuongToiDa\" > 0");
                    table.CheckConstraint("CK_KhungGioXemPhong_ThoiGian", "\"ThoiGianBatDau\" < \"ThoiGianKetThuc\"");
                    table.ForeignKey(
                        name: "FK_khung_gio_xem_phong_nguoi_dung_NguoiPhuTrachId",
                        column: x => x.NguoiPhuTrachId,
                        principalTable: "nguoi_dung",
                        principalColumn: "NguoiDungId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_khung_gio_xem_phong_nguoi_dung_NguoiTaoId",
                        column: x => x.NguoiTaoId,
                        principalTable: "nguoi_dung",
                        principalColumn: "NguoiDungId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_khung_gio_xem_phong_phong_tro_PhongTroId",
                        column: x => x.PhongTroId,
                        principalTable: "phong_tro",
                        principalColumn: "PhongTroId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "nhan_vien_chi_nhanh",
                columns: table => new
                {
                    NhanVienChiNhanhId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NguoiDungId = table.Column<int>(type: "integer", nullable: false),
                    ChiNhanhId = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    NgayPhanCong = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NguoiPhanCongId = table.Column<int>(type: "integer", nullable: false),
                    NgayThuHoi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NguoiThuHoiId = table.Column<int>(type: "integer", nullable: true),
                    LyDo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_nhan_vien_chi_nhanh", x => x.NhanVienChiNhanhId);
                    table.ForeignKey(
                        name: "FK_nhan_vien_chi_nhanh_chi_nhanh_ChiNhanhId",
                        column: x => x.ChiNhanhId,
                        principalTable: "chi_nhanh",
                        principalColumn: "ChiNhanhId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_nhan_vien_chi_nhanh_nguoi_dung_NguoiDungId",
                        column: x => x.NguoiDungId,
                        principalTable: "nguoi_dung",
                        principalColumn: "NguoiDungId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_nhan_vien_chi_nhanh_nguoi_dung_NguoiPhanCongId",
                        column: x => x.NguoiPhanCongId,
                        principalTable: "nguoi_dung",
                        principalColumn: "NguoiDungId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_nhan_vien_chi_nhanh_nguoi_dung_NguoiThuHoiId",
                        column: x => x.NguoiThuHoiId,
                        principalTable: "nguoi_dung",
                        principalColumn: "NguoiDungId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "yeu_cau_xem_phong",
                columns: table => new
                {
                    YeuCauXemPhongId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PhongTroId = table.Column<int>(type: "integer", nullable: false),
                    KhachVangLaiId = table.Column<int>(type: "integer", nullable: true),
                    KhungGioXemPhongId = table.Column<int>(type: "integer", nullable: true),
                    HoTen = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SoDienThoai = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    ThoiGianMongMuon = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ThoiGianXacNhan = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NguonTao = table.Column<int>(type: "integer", nullable: false),
                    HinhThucXacNhan = table.Column<int>(type: "integer", nullable: true),
                    TrangThai = table.Column<int>(type: "integer", nullable: false),
                    GhiChu = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    NgayTao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NgayCapNhat = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_yeu_cau_xem_phong", x => x.YeuCauXemPhongId);
                    table.ForeignKey(
                        name: "FK_yeu_cau_xem_phong_khach_vang_lai_KhachVangLaiId",
                        column: x => x.KhachVangLaiId,
                        principalTable: "khach_vang_lai",
                        principalColumn: "KhachVangLaiId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_yeu_cau_xem_phong_khung_gio_xem_phong_KhungGioXemPhongId",
                        column: x => x.KhungGioXemPhongId,
                        principalTable: "khung_gio_xem_phong",
                        principalColumn: "KhungGioXemPhongId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_yeu_cau_xem_phong_phong_tro_PhongTroId",
                        column: x => x.PhongTroId,
                        principalTable: "phong_tro",
                        principalColumn: "PhongTroId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "lich_su_trang_thai_yeu_cau_xem_phong",
                columns: table => new
                {
                    LichSuTrangThaiYeuCauXemPhongId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    YeuCauXemPhongId = table.Column<int>(type: "integer", nullable: false),
                    TrangThaiTruoc = table.Column<int>(type: "integer", nullable: true),
                    TrangThaiSau = table.Column<int>(type: "integer", nullable: false),
                    LoaiTacNhan = table.Column<int>(type: "integer", nullable: false),
                    NguoiThucHienId = table.Column<int>(type: "integer", nullable: true),
                    NgayThucHien = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LyDo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lich_su_trang_thai_yeu_cau_xem_phong", x => x.LichSuTrangThaiYeuCauXemPhongId);
                    table.ForeignKey(
                        name: "FK_lich_su_trang_thai_yeu_cau_xem_phong_nguoi_dung_NguoiThucHi~",
                        column: x => x.NguoiThucHienId,
                        principalTable: "nguoi_dung",
                        principalColumn: "NguoiDungId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_lich_su_trang_thai_yeu_cau_xem_phong_yeu_cau_xem_phong_YeuC~",
                        column: x => x.YeuCauXemPhongId,
                        principalTable: "yeu_cau_xem_phong",
                        principalColumn: "YeuCauXemPhongId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "yeu_cau_giu_cho",
                columns: table => new
                {
                    YeuCauGiuChoId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    KhachVangLaiId = table.Column<int>(type: "integer", nullable: false),
                    YeuCauXemPhongId = table.Column<int>(type: "integer", nullable: true),
                    PhongTroId = table.Column<int>(type: "integer", nullable: false),
                    SoTienGiuCho = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    HanThanhToan = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    HanKyHopDong = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NguoiDuyetId = table.Column<int>(type: "integer", nullable: true),
                    NgayDuyet = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TrangThai = table.Column<int>(type: "integer", nullable: false),
                    LyDoTuChoiHoacHuy = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    NgayTao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NgayCapNhat = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_yeu_cau_giu_cho", x => x.YeuCauGiuChoId);
                    table.CheckConstraint("CK_YeuCauGiuCho_SoTienGiuCho", "\"SoTienGiuCho\" IS NULL OR \"SoTienGiuCho\" > 0");
                    table.CheckConstraint("CK_YeuCauGiuCho_SoTienTheoTrangThai", "(\"TrangThai\" NOT IN (1, 2, 3, 4)) OR (\"SoTienGiuCho\" IS NOT NULL AND \"SoTienGiuCho\" > 0)");
                    table.ForeignKey(
                        name: "FK_yeu_cau_giu_cho_khach_vang_lai_KhachVangLaiId",
                        column: x => x.KhachVangLaiId,
                        principalTable: "khach_vang_lai",
                        principalColumn: "KhachVangLaiId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_yeu_cau_giu_cho_nguoi_dung_NguoiDuyetId",
                        column: x => x.NguoiDuyetId,
                        principalTable: "nguoi_dung",
                        principalColumn: "NguoiDungId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_yeu_cau_giu_cho_phong_tro_PhongTroId",
                        column: x => x.PhongTroId,
                        principalTable: "phong_tro",
                        principalColumn: "PhongTroId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_yeu_cau_giu_cho_yeu_cau_xem_phong_YeuCauXemPhongId",
                        column: x => x.YeuCauXemPhongId,
                        principalTable: "yeu_cau_xem_phong",
                        principalColumn: "YeuCauXemPhongId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ap_dung_tien_giu_cho_vao_tien_coc",
                columns: table => new
                {
                    ApDungTienGiuChoVaoTienCocId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    YeuCauGiuChoId = table.Column<int>(type: "integer", nullable: false),
                    HopDongId = table.Column<int>(type: "integer", nullable: false),
                    SoTienApDung = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    NgayApDung = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NguoiThucHienId = table.Column<int>(type: "integer", nullable: false),
                    GhiChu = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ap_dung_tien_giu_cho_vao_tien_coc", x => x.ApDungTienGiuChoVaoTienCocId);
                    table.CheckConstraint("CK_ApDungTienGiuChoVaoTienCoc_SoTienApDung", "\"SoTienApDung\" > 0");
                    table.ForeignKey(
                        name: "FK_ap_dung_tien_giu_cho_vao_tien_coc_hop_dong_HopDongId",
                        column: x => x.HopDongId,
                        principalTable: "hop_dong",
                        principalColumn: "HopDongId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ap_dung_tien_giu_cho_vao_tien_coc_nguoi_dung_NguoiThucHienId",
                        column: x => x.NguoiThucHienId,
                        principalTable: "nguoi_dung",
                        principalColumn: "NguoiDungId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ap_dung_tien_giu_cho_vao_tien_coc_yeu_cau_giu_cho_YeuCauGiu~",
                        column: x => x.YeuCauGiuChoId,
                        principalTable: "yeu_cau_giu_cho",
                        principalColumn: "YeuCauGiuChoId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "lich_su_trang_thai_yeu_cau_giu_cho",
                columns: table => new
                {
                    LichSuTrangThaiYeuCauGiuChoId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    YeuCauGiuChoId = table.Column<int>(type: "integer", nullable: false),
                    TrangThaiTruoc = table.Column<int>(type: "integer", nullable: true),
                    TrangThaiSau = table.Column<int>(type: "integer", nullable: false),
                    LoaiTacNhan = table.Column<int>(type: "integer", nullable: false),
                    NguoiThucHienId = table.Column<int>(type: "integer", nullable: true),
                    NgayThucHien = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LyDo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lich_su_trang_thai_yeu_cau_giu_cho", x => x.LichSuTrangThaiYeuCauGiuChoId);
                    table.ForeignKey(
                        name: "FK_lich_su_trang_thai_yeu_cau_giu_cho_nguoi_dung_NguoiThucHien~",
                        column: x => x.NguoiThucHienId,
                        principalTable: "nguoi_dung",
                        principalColumn: "NguoiDungId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_lich_su_trang_thai_yeu_cau_giu_cho_yeu_cau_giu_cho_YeuCauGi~",
                        column: x => x.YeuCauGiuChoId,
                        principalTable: "yeu_cau_giu_cho",
                        principalColumn: "YeuCauGiuChoId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "quyet_dinh_hoan_tien_giu_cho",
                columns: table => new
                {
                    QuyetDinhHoanTienGiuChoId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    YeuCauGiuChoId = table.Column<int>(type: "integer", nullable: false),
                    SoTienHoanDuyet = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    LyDo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    NguoiQuyetDinhId = table.Column<int>(type: "integer", nullable: false),
                    NgayQuyetDinh = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TrangThai = table.Column<int>(type: "integer", nullable: false),
                    GhiChu = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quyet_dinh_hoan_tien_giu_cho", x => x.QuyetDinhHoanTienGiuChoId);
                    table.CheckConstraint("CK_QuyetDinhHoanTienGiuCho_SoTienHoanDuyet", "\"SoTienHoanDuyet\" >= 0");
                    table.ForeignKey(
                        name: "FK_quyet_dinh_hoan_tien_giu_cho_nguoi_dung_NguoiQuyetDinhId",
                        column: x => x.NguoiQuyetDinhId,
                        principalTable: "nguoi_dung",
                        principalColumn: "NguoiDungId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_quyet_dinh_hoan_tien_giu_cho_yeu_cau_giu_cho_YeuCauGiuChoId",
                        column: x => x.YeuCauGiuChoId,
                        principalTable: "yeu_cau_giu_cho",
                        principalColumn: "YeuCauGiuChoId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "yeu_cau_thanh_toan_giu_cho",
                columns: table => new
                {
                    YeuCauThanhToanGiuChoId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    YeuCauGiuChoId = table.Column<int>(type: "integer", nullable: false),
                    MaYeuCau = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SoTien = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    NoiDungChuyenKhoan = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    HanThanhToan = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TrangThai = table.Column<int>(type: "integer", nullable: false),
                    NguoiTaoId = table.Column<int>(type: "integer", nullable: false),
                    NgayTao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NgayCapNhat = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_yeu_cau_thanh_toan_giu_cho", x => x.YeuCauThanhToanGiuChoId);
                    table.CheckConstraint("CK_YeuCauThanhToanGiuCho_SoTien", "\"SoTien\" > 0");
                    table.ForeignKey(
                        name: "FK_yeu_cau_thanh_toan_giu_cho_nguoi_dung_NguoiTaoId",
                        column: x => x.NguoiTaoId,
                        principalTable: "nguoi_dung",
                        principalColumn: "NguoiDungId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_yeu_cau_thanh_toan_giu_cho_yeu_cau_giu_cho_YeuCauGiuChoId",
                        column: x => x.YeuCauGiuChoId,
                        principalTable: "yeu_cau_giu_cho",
                        principalColumn: "YeuCauGiuChoId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "giao_dich_hoan_tien_giu_cho",
                columns: table => new
                {
                    GiaoDichHoanTienGiuChoId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    QuyetDinhHoanTienGiuChoId = table.Column<int>(type: "integer", nullable: false),
                    SoTienHoan = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MaGiaoDichHoan = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UrlHinhAnhMinhChung = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    NguoiXacNhanId = table.Column<int>(type: "integer", nullable: false),
                    NgayHoan = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GhiChu = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_giao_dich_hoan_tien_giu_cho", x => x.GiaoDichHoanTienGiuChoId);
                    table.CheckConstraint("CK_GiaoDichHoanTienGiuCho_SoTienHoan", "\"SoTienHoan\" > 0");
                    table.ForeignKey(
                        name: "FK_giao_dich_hoan_tien_giu_cho_nguoi_dung_NguoiXacNhanId",
                        column: x => x.NguoiXacNhanId,
                        principalTable: "nguoi_dung",
                        principalColumn: "NguoiDungId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_giao_dich_hoan_tien_giu_cho_quyet_dinh_hoan_tien_giu_cho_Qu~",
                        column: x => x.QuyetDinhHoanTienGiuChoId,
                        principalTable: "quyet_dinh_hoan_tien_giu_cho",
                        principalColumn: "QuyetDinhHoanTienGiuChoId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "lich_su_trang_thai_yeu_cau_thanh_toan_giu_cho",
                columns: table => new
                {
                    LichSuTrangThaiYeuCauThanhToanGiuChoId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    YeuCauThanhToanGiuChoId = table.Column<int>(type: "integer", nullable: false),
                    TrangThaiTruoc = table.Column<int>(type: "integer", nullable: true),
                    TrangThaiSau = table.Column<int>(type: "integer", nullable: false),
                    LoaiTacNhan = table.Column<int>(type: "integer", nullable: false),
                    NguoiThucHienId = table.Column<int>(type: "integer", nullable: true),
                    NgayThucHien = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LyDo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lich_su_trang_thai_yeu_cau_thanh_toan_giu_cho", x => x.LichSuTrangThaiYeuCauThanhToanGiuChoId);
                    table.ForeignKey(
                        name: "FK_lich_su_trang_thai_yeu_cau_thanh_toan_giu_cho_nguoi_dung_Ng~",
                        column: x => x.NguoiThucHienId,
                        principalTable: "nguoi_dung",
                        principalColumn: "NguoiDungId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_lich_su_trang_thai_yeu_cau_thanh_toan_giu_cho_yeu_cau_thanh~",
                        column: x => x.YeuCauThanhToanGiuChoId,
                        principalTable: "yeu_cau_thanh_toan_giu_cho",
                        principalColumn: "YeuCauThanhToanGiuChoId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "minh_chung_thanh_toan_giu_cho",
                columns: table => new
                {
                    MinhChungThanhToanGiuChoId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    YeuCauThanhToanGiuChoId = table.Column<int>(type: "integer", nullable: false),
                    UrlHinhAnh = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    PublicIdHinhAnh = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    MaGiaoDichNganHang = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SoTienKhaiBao = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    NgayChuyenTien = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GhiChuKhachHang = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TrangThai = table.Column<int>(type: "integer", nullable: false),
                    NguoiDoiChieuId = table.Column<int>(type: "integer", nullable: true),
                    NgayDoiChieu = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LyDoTuChoi = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    NgayTao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_minh_chung_thanh_toan_giu_cho", x => x.MinhChungThanhToanGiuChoId);
                    table.CheckConstraint("CK_MinhChungThanhToanGiuCho_SoTienKhaiBao", "\"SoTienKhaiBao\" > 0");
                    table.ForeignKey(
                        name: "FK_minh_chung_thanh_toan_giu_cho_nguoi_dung_NguoiDoiChieuId",
                        column: x => x.NguoiDoiChieuId,
                        principalTable: "nguoi_dung",
                        principalColumn: "NguoiDungId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_minh_chung_thanh_toan_giu_cho_yeu_cau_thanh_toan_giu_cho_Ye~",
                        column: x => x.YeuCauThanhToanGiuChoId,
                        principalTable: "yeu_cau_thanh_toan_giu_cho",
                        principalColumn: "YeuCauThanhToanGiuChoId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "giao_dich_giu_cho",
                columns: table => new
                {
                    GiaoDichGiuChoId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    YeuCauThanhToanGiuChoId = table.Column<int>(type: "integer", nullable: false),
                    MinhChungThanhToanGiuChoId = table.Column<int>(type: "integer", nullable: true),
                    MaGiaoDich = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SoTienThucNhan = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PhuongThuc = table.Column<int>(type: "integer", nullable: false),
                    NgayThucNhan = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NguoiXacNhanId = table.Column<int>(type: "integer", nullable: false),
                    NgayXacNhan = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GhiChu = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_giao_dich_giu_cho", x => x.GiaoDichGiuChoId);
                    table.CheckConstraint("CK_GiaoDichGiuCho_SoTienThucNhan", "\"SoTienThucNhan\" > 0");
                    table.ForeignKey(
                        name: "FK_giao_dich_giu_cho_minh_chung_thanh_toan_giu_cho_MinhChungTh~",
                        column: x => x.MinhChungThanhToanGiuChoId,
                        principalTable: "minh_chung_thanh_toan_giu_cho",
                        principalColumn: "MinhChungThanhToanGiuChoId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_giao_dich_giu_cho_nguoi_dung_NguoiXacNhanId",
                        column: x => x.NguoiXacNhanId,
                        principalTable: "nguoi_dung",
                        principalColumn: "NguoiDungId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_giao_dich_giu_cho_yeu_cau_thanh_toan_giu_cho_YeuCauThanhToa~",
                        column: x => x.YeuCauThanhToanGiuChoId,
                        principalTable: "yeu_cau_thanh_toan_giu_cho",
                        principalColumn: "YeuCauThanhToanGiuChoId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_phong_tro_MaCongKhai",
                table: "phong_tro",
                column: "MaCongKhai",
                unique: true,
                filter: "\"MaCongKhai\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_phong_tro_NguoiDangTinId",
                table: "phong_tro",
                column: "NguoiDangTinId");

            migrationBuilder.CreateIndex(
                name: "IX_anh_phong_tro_NguoiTaiLenId",
                table: "anh_phong_tro",
                column: "NguoiTaiLenId");

            migrationBuilder.CreateIndex(
                name: "IX_anh_phong_tro_PhongTroId",
                table: "anh_phong_tro",
                column: "PhongTroId",
                unique: true,
                filter: "\"LaAnhDaiDien\" = true AND \"IsActive\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_ap_dung_tien_giu_cho_vao_tien_coc_HopDongId",
                table: "ap_dung_tien_giu_cho_vao_tien_coc",
                column: "HopDongId");

            migrationBuilder.CreateIndex(
                name: "IX_ap_dung_tien_giu_cho_vao_tien_coc_NguoiThucHienId",
                table: "ap_dung_tien_giu_cho_vao_tien_coc",
                column: "NguoiThucHienId");

            migrationBuilder.CreateIndex(
                name: "IX_ap_dung_tien_giu_cho_vao_tien_coc_YeuCauGiuChoId",
                table: "ap_dung_tien_giu_cho_vao_tien_coc",
                column: "YeuCauGiuChoId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_giao_dich_giu_cho_MaGiaoDich",
                table: "giao_dich_giu_cho",
                column: "MaGiaoDich",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_giao_dich_giu_cho_MinhChungThanhToanGiuChoId",
                table: "giao_dich_giu_cho",
                column: "MinhChungThanhToanGiuChoId",
                unique: true,
                filter: "\"MinhChungThanhToanGiuChoId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_giao_dich_giu_cho_NguoiXacNhanId",
                table: "giao_dich_giu_cho",
                column: "NguoiXacNhanId");

            migrationBuilder.CreateIndex(
                name: "IX_giao_dich_giu_cho_YeuCauThanhToanGiuChoId",
                table: "giao_dich_giu_cho",
                column: "YeuCauThanhToanGiuChoId");

            migrationBuilder.CreateIndex(
                name: "IX_giao_dich_hoan_tien_giu_cho_MaGiaoDichHoan",
                table: "giao_dich_hoan_tien_giu_cho",
                column: "MaGiaoDichHoan",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_giao_dich_hoan_tien_giu_cho_NguoiXacNhanId",
                table: "giao_dich_hoan_tien_giu_cho",
                column: "NguoiXacNhanId");

            migrationBuilder.CreateIndex(
                name: "IX_giao_dich_hoan_tien_giu_cho_QuyetDinhHoanTienGiuChoId",
                table: "giao_dich_hoan_tien_giu_cho",
                column: "QuyetDinhHoanTienGiuChoId");

            migrationBuilder.CreateIndex(
                name: "IX_khach_vang_lai_NguoiDungId",
                table: "khach_vang_lai",
                column: "NguoiDungId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_khung_gio_xem_phong_NguoiPhuTrachId",
                table: "khung_gio_xem_phong",
                column: "NguoiPhuTrachId");

            migrationBuilder.CreateIndex(
                name: "IX_khung_gio_xem_phong_NguoiTaoId",
                table: "khung_gio_xem_phong",
                column: "NguoiTaoId");

            migrationBuilder.CreateIndex(
                name: "IX_khung_gio_xem_phong_PhongTroId",
                table: "khung_gio_xem_phong",
                column: "PhongTroId");

            migrationBuilder.CreateIndex(
                name: "IX_lich_su_trang_thai_yeu_cau_giu_cho_NguoiThucHienId",
                table: "lich_su_trang_thai_yeu_cau_giu_cho",
                column: "NguoiThucHienId");

            migrationBuilder.CreateIndex(
                name: "IX_lich_su_trang_thai_yeu_cau_giu_cho_YeuCauGiuChoId",
                table: "lich_su_trang_thai_yeu_cau_giu_cho",
                column: "YeuCauGiuChoId");

            migrationBuilder.CreateIndex(
                name: "IX_lich_su_trang_thai_yeu_cau_thanh_toan_giu_cho_NguoiThucHien~",
                table: "lich_su_trang_thai_yeu_cau_thanh_toan_giu_cho",
                column: "NguoiThucHienId");

            migrationBuilder.CreateIndex(
                name: "IX_lich_su_trang_thai_yeu_cau_thanh_toan_giu_cho_YeuCauThanhTo~",
                table: "lich_su_trang_thai_yeu_cau_thanh_toan_giu_cho",
                column: "YeuCauThanhToanGiuChoId");

            migrationBuilder.CreateIndex(
                name: "IX_lich_su_trang_thai_yeu_cau_xem_phong_NguoiThucHienId",
                table: "lich_su_trang_thai_yeu_cau_xem_phong",
                column: "NguoiThucHienId");

            migrationBuilder.CreateIndex(
                name: "IX_lich_su_trang_thai_yeu_cau_xem_phong_YeuCauXemPhongId",
                table: "lich_su_trang_thai_yeu_cau_xem_phong",
                column: "YeuCauXemPhongId");

            migrationBuilder.CreateIndex(
                name: "IX_minh_chung_thanh_toan_giu_cho_NguoiDoiChieuId",
                table: "minh_chung_thanh_toan_giu_cho",
                column: "NguoiDoiChieuId");

            migrationBuilder.CreateIndex(
                name: "IX_minh_chung_thanh_toan_giu_cho_YeuCauThanhToanGiuChoId",
                table: "minh_chung_thanh_toan_giu_cho",
                column: "YeuCauThanhToanGiuChoId");

            migrationBuilder.CreateIndex(
                name: "IX_nhan_vien_chi_nhanh_ChiNhanhId",
                table: "nhan_vien_chi_nhanh",
                column: "ChiNhanhId");

            migrationBuilder.CreateIndex(
                name: "IX_nhan_vien_chi_nhanh_NguoiDungId_ChiNhanhId",
                table: "nhan_vien_chi_nhanh",
                columns: new[] { "NguoiDungId", "ChiNhanhId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_nhan_vien_chi_nhanh_NguoiPhanCongId",
                table: "nhan_vien_chi_nhanh",
                column: "NguoiPhanCongId");

            migrationBuilder.CreateIndex(
                name: "IX_nhan_vien_chi_nhanh_NguoiThuHoiId",
                table: "nhan_vien_chi_nhanh",
                column: "NguoiThuHoiId");

            migrationBuilder.CreateIndex(
                name: "IX_quyet_dinh_hoan_tien_giu_cho_NguoiQuyetDinhId",
                table: "quyet_dinh_hoan_tien_giu_cho",
                column: "NguoiQuyetDinhId");

            migrationBuilder.CreateIndex(
                name: "IX_quyet_dinh_hoan_tien_giu_cho_YeuCauGiuChoId",
                table: "quyet_dinh_hoan_tien_giu_cho",
                column: "YeuCauGiuChoId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_yeu_cau_giu_cho_KhachVangLaiId",
                table: "yeu_cau_giu_cho",
                column: "KhachVangLaiId");

            migrationBuilder.CreateIndex(
                name: "IX_yeu_cau_giu_cho_NguoiDuyetId",
                table: "yeu_cau_giu_cho",
                column: "NguoiDuyetId");

            migrationBuilder.CreateIndex(
                name: "IX_yeu_cau_giu_cho_PhongTroId",
                table: "yeu_cau_giu_cho",
                column: "PhongTroId",
                unique: true,
                filter: "\"TrangThai\" IN (1, 2, 3)");

            migrationBuilder.CreateIndex(
                name: "IX_yeu_cau_giu_cho_YeuCauXemPhongId",
                table: "yeu_cau_giu_cho",
                column: "YeuCauXemPhongId");

            migrationBuilder.CreateIndex(
                name: "IX_yeu_cau_thanh_toan_giu_cho_MaYeuCau",
                table: "yeu_cau_thanh_toan_giu_cho",
                column: "MaYeuCau",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_yeu_cau_thanh_toan_giu_cho_NguoiTaoId",
                table: "yeu_cau_thanh_toan_giu_cho",
                column: "NguoiTaoId");

            migrationBuilder.CreateIndex(
                name: "IX_yeu_cau_thanh_toan_giu_cho_YeuCauGiuChoId",
                table: "yeu_cau_thanh_toan_giu_cho",
                column: "YeuCauGiuChoId");

            migrationBuilder.CreateIndex(
                name: "IX_yeu_cau_xem_phong_KhachVangLaiId",
                table: "yeu_cau_xem_phong",
                column: "KhachVangLaiId");

            migrationBuilder.CreateIndex(
                name: "IX_yeu_cau_xem_phong_KhungGioXemPhongId",
                table: "yeu_cau_xem_phong",
                column: "KhungGioXemPhongId");

            migrationBuilder.CreateIndex(
                name: "IX_yeu_cau_xem_phong_PhongTroId",
                table: "yeu_cau_xem_phong",
                column: "PhongTroId");

            migrationBuilder.AddForeignKey(
                name: "FK_phong_tro_nguoi_dung_NguoiDangTinId",
                table: "phong_tro",
                column: "NguoiDangTinId",
                principalTable: "nguoi_dung",
                principalColumn: "NguoiDungId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_phong_tro_nguoi_dung_NguoiDangTinId",
                table: "phong_tro");

            migrationBuilder.DropTable(
                name: "anh_phong_tro");

            migrationBuilder.DropTable(
                name: "ap_dung_tien_giu_cho_vao_tien_coc");

            migrationBuilder.DropTable(
                name: "giao_dich_giu_cho");

            migrationBuilder.DropTable(
                name: "giao_dich_hoan_tien_giu_cho");

            migrationBuilder.DropTable(
                name: "lich_su_trang_thai_yeu_cau_giu_cho");

            migrationBuilder.DropTable(
                name: "lich_su_trang_thai_yeu_cau_thanh_toan_giu_cho");

            migrationBuilder.DropTable(
                name: "lich_su_trang_thai_yeu_cau_xem_phong");

            migrationBuilder.DropTable(
                name: "nhan_vien_chi_nhanh");

            migrationBuilder.DropTable(
                name: "minh_chung_thanh_toan_giu_cho");

            migrationBuilder.DropTable(
                name: "quyet_dinh_hoan_tien_giu_cho");

            migrationBuilder.DropTable(
                name: "yeu_cau_thanh_toan_giu_cho");

            migrationBuilder.DropTable(
                name: "yeu_cau_giu_cho");

            migrationBuilder.DropTable(
                name: "yeu_cau_xem_phong");

            migrationBuilder.DropTable(
                name: "khach_vang_lai");

            migrationBuilder.DropTable(
                name: "khung_gio_xem_phong");

            migrationBuilder.DropIndex(
                name: "IX_phong_tro_MaCongKhai",
                table: "phong_tro");

            migrationBuilder.DropIndex(
                name: "IX_phong_tro_NguoiDangTinId",
                table: "phong_tro");

            migrationBuilder.DropColumn(
                name: "DuocDangTin",
                table: "phong_tro");

            migrationBuilder.DropColumn(
                name: "MaCongKhai",
                table: "phong_tro");

            migrationBuilder.DropColumn(
                name: "NguoiDangTinId",
                table: "phong_tro");

            migrationBuilder.DropColumn(
                name: "TieuDeDangTin",
                table: "phong_tro");
        }
    }
}
