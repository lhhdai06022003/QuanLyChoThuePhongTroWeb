using System;
using QuanLyChoThuePhongTroWeb.Application.Common.Files;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.DTOs
{
    public class UploadMeterImageRequest
    {
        public int PhongTroId { get; set; }
        public int Thang { get; set; }
        public int Nam { get; set; }
        public LoaiDongHo LoaiDongHo { get; set; }
        public UploadFile File { get; set; } = null!;
    }

    public class MeterImageAccessContext
    {
        public int AnhChiSoDongHoId { get; set; }
        public int DichVuDienNuocCuaPhongId { get; set; }
        public int PhongTroId { get; set; }
        public int ChiNhanhId { get; set; }
        public TrangThaiXuLyAnhChiSo TrangThaiXuLy { get; set; }
        public LoaiDongHo LoaiDongHo { get; set; }
        public bool DuocChonLamChiSoChinhThuc { get; set; }
        public bool IsDeleted { get; set; }
    }

    public class MeterImageWorkflowResult
    {
        public int AnhChiSoDongHoId { get; set; }
        public int DichVuDienNuocCuaPhongId { get; set; }
        public int PhongTroId { get; set; }
        public int Thang { get; set; }
        public int Nam { get; set; }
        public LoaiDongHo LoaiDongHo { get; set; }
        public string Url { get; set; } = string.Empty;
        public TrangThaiXuLyAnhChiSo TrangThaiXuLy { get; set; }
        public decimal? GiaTriAIGoiY { get; set; }
        public decimal? GiaTriXacNhan { get; set; }
        public double? DoTinCay { get; set; }
        public string? ThongBaoLoi { get; set; }
        public DateTime NgayGui { get; set; }
        public DateTime? NgayXuLy { get; set; }
        public DateTime? NgayXacNhan { get; set; }
        public bool DuocChonLamChiSoChinhThuc { get; set; }
        public string? GhiChuXacNhan { get; set; }
    }

    public class ConfirmMeterImageRequest
    {
        public int AnhChiSoDongHoId { get; set; }
        public decimal GiaTriXacNhan { get; set; }
        public string? GhiChu { get; set; }
    }

    public record CorrectConfirmedMeterImageRequest(int AnhChiSoDongHoId, decimal GiaTriMoi, string LyDo);

    public enum MeterReadingSubmissionMode
    {
        OfficialImage = 1,
        Manual = 2
    }

    public class ApproveMeterPeriodItem
    {
        public int PhongTroId { get; set; }
        public MeterReadingSubmissionMode DienMode { get; set; } = MeterReadingSubmissionMode.Manual;
        public MeterReadingSubmissionMode NuocMode { get; set; } = MeterReadingSubmissionMode.Manual;
        public decimal? ChiSoDienMoiThuCong { get; set; }
        public decimal? ChiSoNuocMoiThuCong { get; set; }
        public string? LyDoDienThuCong { get; set; }
        public string? LyDoNuocThuCong { get; set; }
    }

    public class ApproveMeterPeriodsRequest
    {
        public int Thang { get; set; }
        public int Nam { get; set; }
        public System.Collections.Generic.IReadOnlyList<ApproveMeterPeriodItem> DanhSachPhong { get; set; } = System.Array.Empty<ApproveMeterPeriodItem>();
    }

    public class MeterPeriodsApprovalResult
    {
        public System.Collections.Generic.IReadOnlyList<MeterPeriodApprovalResult> Items { get; set; } = System.Array.Empty<MeterPeriodApprovalResult>();
    }

    public enum MeterReadingEvidenceSource
    {
        AnhXacNhan,
        NhapThuCong
    }

    public class MeterPeriodApprovalResult
    {
        public int DichVuDienNuocCuaPhongId { get; set; }
        public int PhongTroId { get; set; }
        public int Thang { get; set; }
        public int Nam { get; set; }
        public TrangThaiGhiNhan TrangThaiGhiNhan { get; set; }
        public decimal ChiSoDienCu { get; set; }
        public decimal ChiSoDienMoi { get; set; }
        public MeterReadingEvidenceSource NguonBangChungDien { get; set; }
        public decimal ChiSoNuocCu { get; set; }
        public decimal ChiSoNuocMoi { get; set; }
        public MeterReadingEvidenceSource NguonBangChungNuoc { get; set; }
        public DateTime? NgayDuyet { get; set; }
        public int? NguoiDuyetId { get; set; }
        public string? GhiChuDuyet { get; set; }
    }
}
