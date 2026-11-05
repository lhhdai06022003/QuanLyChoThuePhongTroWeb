using System.Collections.Generic;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels
{
    public record GiaHanHopDongViewModel(GiaHanHopDongFormDto Form, List<DieuKhoanMauRes> DieuKhoanMau);
}
