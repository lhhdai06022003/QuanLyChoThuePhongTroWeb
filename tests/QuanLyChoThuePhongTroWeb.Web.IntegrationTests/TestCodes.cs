using System;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests
{
    // Mã duy nhất cho dữ liệu seed trên DB test dùng chung (tích lũy qua nhiều lượt chạy).
    public static class TestCodes
    {
        // chi_nhanh."MaChiNhanh" unique, tối đa 10 ký tự: lấp phần sau tiền tố bằng hex ngẫu nhiên.
        // Mã cũ chỉ có 5-6 ký tự ngẫu nhiên (~1 triệu khả năng) nên trùng khi DB test có hàng chục nghìn chi nhánh.
        public static string MaChiNhanh(string prefix) => prefix + Guid.NewGuid().ToString("N")[..(10 - prefix.Length)];
    }
}
