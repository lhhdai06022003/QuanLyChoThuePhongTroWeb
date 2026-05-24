using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.Requests;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels
{
    public class HopDongFilterReq : DataTableRequest
    {
        public int? ChiNhanhId { get; set; }
        public int? NguoiThueId { get; set; }
        public int? TrangThai { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }
}
