namespace QuanLyChoThuePhongTroWeb.Application.Common.Enums
{
    public enum AppRole
    {
        Admin = 0,
        NhanVien = 1,
        KhachThue = 2
    }

    public enum AppTrangThaiPhong
    {
        Trong = 0,
        DaThue = 1,
        BaoTri = 2
    }

    public enum AppTrangThaiHopDong
    {
        DangHoatDong = 1,
        DaKetThuc = 2,
        DaHuy = 3
    }

    public enum AppTrangThaiHoaDon
    {
        ChuaThanhToan = 0,
        DaThanhToan = 1
    }

    public enum AppTrangThaiSuCo
    {
        ChoTiepNhan = 0,
        DangXuLy = 1,
        DaHoanThanh = 2,
        DaHuy = 3
    }

    public enum AppPhuongThucThanhToan
    {
        TienMat = 0,
        ChuyenKhoan = 1
    }

    public enum AppLoaiDongHo
    {
        Dien = 0,
        Nuoc = 1
    }

    public enum AppTrangThaiAnhChiSo
    {
        MoiTaiLen = 0,
        DangXuLy = 1,
        DocDuoc = 2,
        Loi = 3,
        KhongDocDuoc = 4,
        CanChupLai = 5,
        DaXacNhan = 6,
        DaThayThe = 7
    }

    // Trạng thái ghi nhận kỳ chỉ số đi ra ngoài Application; giá trị khớp enum Domain TrangThaiGhiNhan.
    public enum AppTrangThaiGhiNhan
    {
        Nhap = 0,
        ChoDuyet = 1,
        DaDuyet = 2,
        TuChoi = 3
    }

    // Giá trị khớp enum Domain TrangThaiYeuCauThanhToan (lượt thanh toán hóa đơn).
    public enum AppTrangThaiYeuCauThanhToan
    {
        ChoThanhToan = 0,
        DaBaoChuyen = 1,
        DangDoiChieu = 2,
        DaHoanTat = 3,
        TuChoi = 4,
        HetHan = 5,
        DaHuy = 6
    }

    // Giá trị khớp enum Domain TrangThaiMinhChungThanhToan.
    public enum AppTrangThaiMinhChungThanhToan
    {
        ChoXacNhan = 0,
        DaXacNhan = 1,
        TuChoi = 2
    }
}
