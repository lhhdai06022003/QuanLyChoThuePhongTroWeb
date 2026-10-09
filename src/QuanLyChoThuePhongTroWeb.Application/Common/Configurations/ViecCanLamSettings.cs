namespace QuanLyChoThuePhongTroWeb.Application.Common.Configurations
{
    public sealed record ViecCanLamSettings
    {
        public int SuCoChoTiepNhanGapSauGio { get; init; } = 24;
        public int SuCoXuLyQuaLauSauNgay { get; init; } = 7;
        public int HopDongSapHetHanTrongNgay { get; init; } = 30;
        public int HopDongSapHetHanGapTrongNgay { get; init; } = 7;
        public int SoKyQuetChiSo { get; init; } = 6;
        public int SoDongHoaDonQuaHan { get; init; } = 50;
    }
}
