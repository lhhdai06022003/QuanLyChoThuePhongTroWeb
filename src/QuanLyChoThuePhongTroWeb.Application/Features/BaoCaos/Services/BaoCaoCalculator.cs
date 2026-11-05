using System;
using System.Collections.Generic;
using QuanLyChoThuePhongTroWeb.Application.Features.BaoCaos.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.Features.BaoCaos.Services
{
    public enum NhomTuoiNo
    {
        ChuaDenHan,
        Tu1Den30,
        Tu31Den60,
        Tu61Den90,
        Tren90,
        KhongCoHan
    }

    // Hàm thuần dùng chung cho báo cáo. Thời gian lưu UTC; ngày VN = UTC + 7 giờ.
    public static class BaoCaoCalculator
    {
        public const int SoKyToiDa = 24;

        public static string? KiemTraKhoang(int tuThang, int tuNam, int denThang, int denNam)
        {
            if (tuThang < 1 || tuThang > 12 || denThang < 1 || denThang > 12
                || tuNam < 2000 || tuNam > 2100 || denNam < 2000 || denNam > 2100)
            {
                return "Kỳ báo cáo không hợp lệ.";
            }

            var soKy = (denNam * 12 + denThang) - (tuNam * 12 + tuThang) + 1;
            if (soKy < 1)
            {
                return "Kỳ bắt đầu phải trước hoặc bằng kỳ kết thúc.";
            }

            if (soKy > SoKyToiDa)
            {
                return $"Khoảng báo cáo tối đa {SoKyToiDa} kỳ.";
            }

            return null;
        }

        public static IReadOnlyList<BaoCaoKy> LietKeKy(BaoCaoKy tu, BaoCaoKy den)
        {
            var ketQua = new List<BaoCaoKy>();
            var ky = tu;
            while (ky.Key <= den.Key && ketQua.Count <= 1200)
            {
                ketQua.Add(ky);
                ky = ky.Next();
            }

            return ketQua;
        }

        public static DateOnly NgayVn(DateTime utc) => DateOnly.FromDateTime(utc.AddHours(7));

        public static BaoCaoKy KyCua(DateTime utc) => BaoCaoKy.Of(NgayVn(utc));

        public static decimal? TyLe(decimal tu, decimal mau)
        {
            return mau == 0m ? null : Math.Round(tu / mau * 100m, 1, MidpointRounding.AwayFromZero);
        }

        public static decimal? TyLe(int tu, int mau)
        {
            return mau == 0 ? null : Math.Round((decimal)tu / mau * 100m, 1, MidpointRounding.AwayFromZero);
        }

        // Số ngày quá hạn tính theo ngày lịch VN, tối thiểu 1 khi hạn đã qua trong cùng ngày.
        public static int SoNgayQuaHan(DateTime hanUtc, DateTime nowUtc)
        {
            return Math.Max(1, NgayVn(nowUtc).DayNumber - NgayVn(hanUtc).DayNumber);
        }

        // Quá hạn so bằng UTC (khớp InvoiceDebtQueries): hạn >= bây giờ là chưa đến hạn.
        public static NhomTuoiNo PhanNhomTuoiNo(DateTime? hanUtc, DateTime nowUtc)
        {
            if (hanUtc == null)
            {
                return NhomTuoiNo.KhongCoHan;
            }

            if (hanUtc.Value >= nowUtc)
            {
                return NhomTuoiNo.ChuaDenHan;
            }

            var ngay = SoNgayQuaHan(hanUtc.Value, nowUtc);
            if (ngay <= 30)
            {
                return NhomTuoiNo.Tu1Den30;
            }

            if (ngay <= 60)
            {
                return NhomTuoiNo.Tu31Den60;
            }

            return ngay <= 90 ? NhomTuoiNo.Tu61Den90 : NhomTuoiNo.Tren90;
        }

        // Cửa sổ có thể cho thuê của một phòng trong kỳ: từ max(ngày tạo phòng, ngày 1) đến min(cuối tháng, hôm nay).
        public static (DateOnly Tu, DateOnly Den)? CuaSoChoThue(DateOnly ngayTaoPhongVn, BaoCaoKy ky, DateOnly homNayVn)
        {
            var tu = ngayTaoPhongVn > ky.NgayDau ? ngayTaoPhongVn : ky.NgayDau;
            var den = ky.NgayCuoi < homNayVn ? ky.NgayCuoi : homNayVn;
            if (den < tu)
            {
                return null;
            }

            return (tu, den);
        }

        // Số ngày của các khoảng đã hợp nhất nằm trong [tu, den].
        public static int DemNgayGiao(IReadOnlyList<(DateOnly Tu, DateOnly Den)> daHopNhat, DateOnly tu, DateOnly den)
        {
            var tong = 0;
            foreach (var k in daHopNhat)
            {
                var a = k.Tu > tu ? k.Tu : tu;
                var b = k.Den < den ? k.Den : den;
                if (b >= a)
                {
                    tong += b.DayNumber - a.DayNumber + 1;
                }
            }

            return tong;
        }
    }
}
