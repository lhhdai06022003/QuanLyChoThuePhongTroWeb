# LƯỢC ĐỒ QUAN HỆ THỰC THỂ (ERD) - HỆ THỐNG QUẢN LÝ THUÊ PHÒNG TRỌ

Tài liệu này mô tả chi tiết cấu trúc cơ sở dữ liệu (PostgreSQL) của hệ thống được lập bản đồ thông qua Entity Framework Core.

---

## 1. Lược đồ ERD (Mermaid.js)

```mermaid
erDiagram
    ChiNhanh ||--o{ PhongTro : ""
    ChiNhanh ||--o{ DichVuChiNhanh : ""
    DichVu ||--o{ DichVuChiNhanh : ""
    DichVuChiNhanh ||--o{ DangKyDichVu : ""
    PhongTro ||--o{ DangKyDichVu : ""
    PhongTro ||--o{ DichVuDienNuocCuaPhong : ""
    PhongTro ||--o{ HopDong : ""
    NguoiThue ||--o{ HopDong : ""
    NguoiThue ||--o{ ChiTietThanhVienHopDong : ""
    HopDong ||--o{ ChiTietThanhVienHopDong : ""
    HopDong ||--o{ HopDongDieuKhoan : ""
    HopDong ||--o{ HoaDon : ""
    DichVuDienNuocCuaPhong |o--o| HoaDon : ""
    HoaDon ||--o{ ChiTietHoaDon : ""
    DichVu ||--o{ ChiTietHoaDon : ""
    HoaDon ||--o{ LichSuThanhToan : ""
    NguoiDung ||--o{ LichSuThanhToan : ""
    NguoiThue |o--o| NguoiDung : ""

    ChiNhanh {
        int ChiNhanhId PK
        string MaChiNhanh UK
        string TenChiNhanh
        string DiaChi
        string MoTa
        string SoDienThoai
        DateTime NgayTao
        DateTime NgayCapNhat
        bool IsDeleted
    }

    PhongTro {
        int PhongTroId PK
        int ChiNhanhId FK
        string SoPhong
        int TangLau
        double GiaThue
        double DienTich
        int SoNguoiToiDa
        TrangThaiPhong TrangThai
        string MoTa
        DateTime NgayTao
        DateTime NgayCapNhat
        bool IsDeleted
    }

    DichVu {
        int DichVuId PK
        string TenDichVu
        string DonVi
        string GhiChu
        DateTime NgayTao
        DateTime NgayCapNhat
        bool IsDeleted
    }

    DichVuChiNhanh {
        int DichVuChiNhanhId PK
        int ChiNhanhId FK
        int DichVuId FK
        double GiaDichVu
        bool MacDinh
        DateTime NgayTao
        DateTime NgayCapNhat
        bool IsDeleted
    }

    DangKyDichVu {
        int DangKyDichVuId PK
        int PhongTroId FK
        int DichVuChiNhanhId FK
        int SoLuong
        DateTime NgayBatDau
        DateTime NgayKetThuc
    }

    DichVuDienNuocCuaPhong {
        int DichVuDienNuocCuaPhongId PK
        int PhongTroId FK
        int Thang
        int Nam
        double ChiSoDienCu
        double ChiSoDienMoi
        double DonGiaDien
        double ChiSoNuocCu
        double ChiSoNuocMoi
        double DonGiaNuoc
        DateTime NgayTao
        DateTime NgayCapNhat
        bool IsDeleted
    }

    HopDong {
        int HopDongId PK
        string MaHopDong UK
        int PhongTroId FK
        int NguoiThueId FK
        DateTime ThoiDiemBatDau
        DateTime ThoiDiemKetThuc
        double TienCocPhong
        double TienThuePhong
        TrangThaiHopDong TrangThaiHopDong
        DateTime NgayTao
        DateTime NgayCapNhat
        bool IsDeleted
    }

    ChiTietThanhVienHopDong {
        int ChiTietThanhVienHopDongId PK
        int HopDongId FK
        int NguoiThueId FK
        DateTime NgayVao
        DateTime NgayChuyenDi
        bool IsDeleted
    }

    HopDongDieuKhoan {
        int Id PK
        int HopDongId FK
        string TieuDe
        string NoiDung
        int ThuTu
    }

    DieuKhoanMau {
        int DieuKhoanMauId PK
        string TieuDe
        string NoiDung
        bool IsDeleted
        DateTime NgayTao
        DateTime NgayCapNhat
    }

    NguoiThue {
        int NguoiThueId PK
        string HoVaTen
        string Email
        string SoDienThoai
        string CCCD
        DateTime NgayCapCCCD
        string NoiCapCCCD
        DateTime NgaySinh
        string QueQuan
        string GhiChu
        DateTime NgayTao
        DateTime NgayCapNhat
        bool IsDeleted
    }

    NguoiDung {
        int NguoiDungId PK
        string TenDangNhap UK
        string MatKhauHash
        bool IsActive
        Role Role
        DateTime NgayTao
        DateTime LanDangNhapCuoi
        int NguoiThueId FK "Nullable"
        bool IsDeleted
    }

    HoaDon {
        int HoaDonId PK
        string MaHoaDon UK
        int HopDongId FK
        int Thang
        int Nam
        double TongTien
        TrangThaiHoaDon TrangThaiHoaDon
        DateTime NgayTao
        DateTime NgayCapNhat
        bool IsDeleted
        int DichVuDienNuocCuaPhongId FK "Nullable"
    }

    ChiTietHoaDon {
        int ChiTietHoaDonId PK
        int HoaDonId FK
        string TenDichVu
        double DonGia
        int SoLuong
        double TongTien
        bool IsDeleted
        int DichVuId FK "Nullable"
    }

    LichSuThanhToan {
        int LichSuThanhToanId PK
        string MaGiaoDich
        int HoaDonId FK
        int NguoiXacNhanId FK "Nullable"
        double SoTienThanhToan
        PhuongThucThanhToan PhuongThucThanhToan
        DateTime NgayThanhToan
        string GhiChu
        bool IsDeleted
    }
```

---

## 2. Mô tả các Danh mục Enums của hệ thống

### 2.1. TrangThaiPhong
*   `Trong = 0`: Phòng còn trống, sẵn sàng cho thuê.
*   `DaThue = 1`: Phòng đã lập hợp đồng và có khách ở.
*   `BaoTri = 2`: Phòng đang bảo trì cơ sở vật chất.

### 2.2. Role (Vai trò tài khoản)
*   `Admin = 0`: Chủ nhà trọ / Quản trị viên hệ thống.
*   `NhanVien = 1`: Nhân viên quản lý chi nhánh.
*   `KhachThue = 2`: Người thuê phòng.

### 2.3. TrangThaiHopDong
*   `DangHoatDong = 1`: Hợp đồng đang trong thời hạn thuê.
*   `DaKetThuc = 2`: Hợp đồng đã hoàn tất và khách đã thanh lý phòng.
*   `DaHuy = 3`: Hợp đồng bị hủy bỏ trước thời hạn.

### 2.4. TrangThaiHoaDon
*   `ChuaThanhToan = 0`: Hóa đơn mới phát sinh chưa thu tiền.
*   `DaThanhToan = 1`: Hóa đơn đã được ghi nhận thu tiền đầy đủ.

### 2.5. PhuongThucThanhToan
*   `TienMat = 0`: Giao dịch trực tiếp bằng tiền mặt.
*   `ChuyenKhoan = 1`: Thanh toán qua chuyển khoản ngân hàng (VietQR).
