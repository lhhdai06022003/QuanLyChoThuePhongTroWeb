using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Web.Helpers;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests
{
    // Màn của nhân viên không được hiện tên enum thô (MoiTao, ChoNhanVienXuLy...). Mỗi giá trị enum phải có nhãn tiếng Việt;
    // thêm giá trị mới vào enum mà quên nhãn thì test này FAIL. Không cần DB.
    public class GuestStatusTextTests
    {
        public static IEnumerable<object[]> ViewingNames() => Enum.GetNames<TrangThaiYeuCauXemPhong>().Select(n => new object[] { n });
        public static IEnumerable<object[]> ReservationNames() => Enum.GetNames<TrangThaiYeuCauGiuCho>().Select(n => new object[] { n });
        public static IEnumerable<object[]> PaymentNames() => Enum.GetNames<TrangThaiYeuCauThanhToan>().Select(n => new object[] { n });
        public static IEnumerable<object[]> EvidenceNames() => Enum.GetNames<TrangThaiMinhChungThanhToan>().Select(n => new object[] { n });

        [Theory, MemberData(nameof(ViewingNames))]
        public void Viewing_EveryEnumValueHasAVietnameseLabel(string name) =>
            Assert.NotEqual(name, GuestStatusText.Viewing(name));

        [Theory, MemberData(nameof(ReservationNames))]
        public void Reservation_EveryEnumValueHasAVietnameseLabel(string name) =>
            Assert.NotEqual(name, GuestStatusText.Reservation(name));

        [Theory, MemberData(nameof(PaymentNames))]
        public void Payment_EveryEnumValueHasAVietnameseLabel(string name) =>
            Assert.NotEqual(name, GuestStatusText.Payment(name));

        [Theory, MemberData(nameof(EvidenceNames))]
        public void Evidence_EveryEnumValueHasAVietnameseLabel(string name) =>
            Assert.NotEqual(name, GuestStatusText.Evidence(name));

        [Fact]
        public void Labels_MatchWhatTheGuestPagesShow()
        {
            Assert.Equal("Chờ nhân viên duyệt", GuestStatusText.Reservation("MoiTao"));
            Assert.Equal("Chờ nhân viên xác nhận", GuestStatusText.Viewing("ChoNhanVienXuLy"));
            Assert.Equal("Đã báo chuyển, chờ đối soát", GuestStatusText.Payment("DaBaoChuyen"));
            Assert.Equal("Đã đối soát", GuestStatusText.Evidence("DaXacNhan"));
        }

        [Fact]
        public void Payment_NullMeansNoPaymentRequestYet()
        {
            Assert.Equal("Chưa có", GuestStatusText.Payment(null));
        }

        [Fact]
        public void UnknownValue_IsReturnedUnchanged_AndNullBecomesEmptyText()
        {
            Assert.Equal("GiaTriMoi", GuestStatusText.Reservation("GiaTriMoi"));
            Assert.Equal(string.Empty, GuestStatusText.Viewing(null));
        }
    }
}
