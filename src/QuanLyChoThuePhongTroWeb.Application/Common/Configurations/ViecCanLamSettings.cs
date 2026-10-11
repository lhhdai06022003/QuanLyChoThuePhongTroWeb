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
        // HD5: số kỳ-phòng tối đa được đánh giá lại mỗi lần tải, để trang việc cần làm không chậm khi có nhiều kỳ chốt mà chưa có hóa đơn.
        public int SoKyPhongDanhGiaHoaDonTuDong { get; init; } = 60;
    }
}
