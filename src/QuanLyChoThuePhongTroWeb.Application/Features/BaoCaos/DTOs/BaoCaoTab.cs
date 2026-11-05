using System;

namespace QuanLyChoThuePhongTroWeb.Application.Features.BaoCaos.DTOs
{
    public enum BaoCaoTab
    {
        DoanhThu,
        CongNo,
        CongSuat,
        HopDong,
        DienNuoc
    }

    // Slug trên URL: doanh-thu | cong-no | cong-suat | hop-dong | dien-nuoc.
    public static class BaoCaoTabs
    {
        public static string Slug(BaoCaoTab tab)
        {
            return tab switch
            {
                BaoCaoTab.DoanhThu => "doanh-thu",
                BaoCaoTab.CongNo => "cong-no",
                BaoCaoTab.CongSuat => "cong-suat",
                BaoCaoTab.HopDong => "hop-dong",
                BaoCaoTab.DienNuoc => "dien-nuoc",
                _ => "doanh-thu"
            };
        }

        public static bool TryParse(string? slug, out BaoCaoTab tab)
        {
            tab = BaoCaoTab.DoanhThu;
            if (string.IsNullOrWhiteSpace(slug))
            {
                return false;
            }

            foreach (var candidate in Enum.GetValues<BaoCaoTab>())
            {
                if (string.Equals(Slug(candidate), slug.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    tab = candidate;
                    return true;
                }
            }

            return false;
        }
    }
}
