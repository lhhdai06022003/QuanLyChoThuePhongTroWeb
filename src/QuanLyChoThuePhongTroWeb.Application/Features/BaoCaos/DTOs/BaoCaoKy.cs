using System;

namespace QuanLyChoThuePhongTroWeb.Application.Features.BaoCaos.DTOs
{
    // Một kỳ báo cáo theo tháng. Key dùng cùng biểu thức để lọc HoaDon/DichVuDienNuoc trong SQL.
    public readonly record struct BaoCaoKy(int Thang, int Nam)
    {
        public int Key => Nam * 100 + Thang;
        public string Text => $"T{Thang}/{Nam}";
        public DateOnly NgayDau => new(Nam, Thang, 1);
        public DateOnly NgayCuoi => NgayDau.AddMonths(1).AddDays(-1);
        public BaoCaoKy Next() => Thang == 12 ? new BaoCaoKy(1, Nam + 1) : new BaoCaoKy(Thang + 1, Nam);
        public static BaoCaoKy Of(DateOnly ngayVn) => new(ngayVn.Month, ngayVn.Year);
    }
}
