using System;
using System.Collections.Generic;
using System.Linq;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.Services
{
    // Hợp nhất các khoảng ngày thuê trùng hoặc liền nhau để mỗi ngày chỉ được đếm một lần.
    public static class KhoangNgayThue
    {
        public static IReadOnlyList<(DateOnly Tu, DateOnly Den)> HopNhat(IEnumerable<(DateOnly Tu, DateOnly Den)> khoang)
        {
            var ketQua = new List<(DateOnly Tu, DateOnly Den)>();
            var sapXep = khoang
                .Where(k => k.Den >= k.Tu)
                .OrderBy(k => k.Tu)
                .ToList();

            DateOnly? tu = null;
            DateOnly? den = null;
            foreach (var k in sapXep)
            {
                if (tu == null)
                {
                    tu = k.Tu;
                    den = k.Den;
                }
                else if (k.Tu.DayNumber <= den!.Value.DayNumber + 1)
                {
                    if (k.Den > den)
                    {
                        den = k.Den;
                    }
                }
                else
                {
                    ketQua.Add((tu.Value, den!.Value));
                    tu = k.Tu;
                    den = k.Den;
                }
            }

            if (tu != null)
            {
                ketQua.Add((tu.Value, den!.Value));
            }

            return ketQua;
        }
    }
}
