# Spec đợt 3: nhóm thu tiền

- Trạng thái: **đã code và kiểm trên nhánh** (12/11/2026), người dùng duyệt bảy điểm ở mục 3 ngày 12/11/2026
- Nhánh: `feature/thiet-ke-lai-giao-dien`
- Cách thực thi: `/doi-ngu` đội mới (Opus điều phối và viết phương án; co-van Fable soát phương án; Sonnet làm V1, V2a, V3, V4, V5 song song sau V1; Opus làm V2b, V6 và các chỉnh sau khi xem trình duyệt). Phương án chi tiết: `.bangiao/doi/phuong-an.md` (ngoài Git).

## 1. Vì sao làm

- Hóa đơn, Đối chiếu, SePay, Điện nước (và màn hóa đơn, điện nước của khách thuê) tự ghi đè màu thương hiệu thành xanh teal `#0f6b63` theo "hướng A" chốt 30/09/2026, nên trông như một sản phẩm khác sau đợt 1.
- `hoadon.css` ép nút xác nhận SweetAlert teal trên mọi trang nạp nó, kể cả hộp "Hủy hóa đơn", "Từ chối minh chứng".
- Lịch sử thanh toán có bảy ô màu đặc chữ trắng.
- Dưới 768px các bảng cuộn ngang (bảng điện nước rộng tối thiểu 1120px), nhân viên ghi số bằng điện thoại phải kéo ngang.

## 2. Thiết kế

1. **Bỏ màu teal riêng**: xóa biến ghi đè trong `hoadon.css` (`.hd-page`) và `diennuoc.css` (`.dn-page`, `.kt-page`, `.dn-savebar`), xóa quy tắc ép nút `.swal2-confirm`; mọi màn dùng biến của `trosync-nen.css`. Bỏ luôn lề âm của `.hd-page` (chỉ dùng để tô nền teal tràn mép, làm trang rộng hơn 375px).
2. **Bảng thành thẻ dưới 768px** bằng lớp dùng chung `.ts-bang-the` (`trosync-nen.css`): mỗi dòng một thẻ, mỗi ô có nhãn cột bên trái (`data-label`). `site.js` gán nhãn từ `<th>` khi DataTables vẽ lại (`draw.dt`); bảng tự ghép chuỗi (Điện nước, SePay) ghi `data-label` thẳng trong chuỗi mẫu; `<th data-the-nhan="">` là ô không nhãn. Ô có `div` con hoặc nhiều dòng con thì nhãn ở trên, các dòng xếp dọc căn phải; ô rỗng ẩn; dòng `colspan` hiện khối. Khung `.table-responsive` chứa bảng thẻ không cắt menu ⋯.
3. **Điện nước**: mỗi phòng một thẻ lưới hai cột: phòng và trạng thái; người đại diện; điện cũ | nước cũ; ô nhập điện mới | nước mới; nút Ảnh | Chốt phòng. Thanh "Chốt N phòng" nằm trên thanh tab dưới.
4. **Lịch sử thanh toán**: hai ô nhạt (ô tổng giữ nhãn động "Tiền đã nhận" / "Đã thu kỳ" do JS ghi; ô "Số giao dịch"), một thẻ "Theo phương thức" năm dòng. Dưới 576px mỗi ô chiếm cả hàng; số liệu không ngắt giữa các nhóm nghìn. Tiêu đề trang "Lịch sử thanh toán", bỏ màu thương hiệu và icon.
5. **Lặt vặt**: badge "Chờ Admin" `bg-orange-lt`; nút từ chối minh chứng dùng `btn-danger`; bỏ `table-light`, `btn-xs`, `font-weight-normal|medium`; `<link>` CSS chuyển vào `@section Styles`; cảnh báo cấu hình SePay bọc chữ trong `div` để xuống dòng.

Không làm: đổi điều kiện hiện nút, phép tính tiền, endpoint, controller, DTO, schema; đổi `style="display: none;"` sang `d-none` (JS bật bằng `.toggle()`); thêm `timeZone` vào `dt()` của đối chiếu (server đã trả giờ Việt Nam không offset).

## 3. Điểm người dùng đã xác nhận (12/11/2026)

1. Bỏ màu teal riêng của nhóm thu tiền, kể cả ba màn khách thuê dùng chung CSS (đảo quyết định "hướng A" 30/09/2026).
2. Bảng thành thẻ dưới 768px, Điện nước có thẻ riêng.
3. Lịch sử thanh toán: hai ô nhạt và thẻ theo phương thức.
4. Commit cục bộ từng việc, không push.
5. (Câu của Fable) Nút xác nhận hộp thoại theo quy ước chung: xanh thương hiệu, đỏ cho hộp cảnh báo.
6. (Câu của Fable) Màu điện `var(--ts-wait)`, nước giữ `#1d6fa5` / tối `#6bb7e8`.
7. (Câu của Fable) Chấp nhận mất sắp xếp theo cột và ô chọn tất cả dưới 768px.

## 4. Kết quả

| Việc | Người làm | Commit | Nội dung |
| --- | --- | --- | --- |
| V1 | Sonnet | `d436338` | `.ts-bang-the`, `.ts-o-so-*`, `TroBangThe` trong `site.js` |
| V2b | Opus | `1f57537` | View hóa đơn: `ts-bang-the`, `data-the-nhan`, bỏ `table-light`, `btn-xs`, khối `<style>` trùng |
| V2a | Sonnet | `c0a0c06` | `hoadon.css` bỏ teal và quy tắc nút SweetAlert, quy tắc ô đầu chỉ từ 768px, bỏ selector chết `#thuTienModal` |
| V4 | Sonnet (+ Opus sửa tiêu đề) | `0a0e91d` | Lịch sử thanh toán |
| V5 | Sonnet | `bb5dd61` | Đối chiếu, SePay |
| V3 | Sonnet | `ade906a` | Điện nước: bỏ teal, thẻ lưới, thanh chốt trên thanh tab, lớp Tabler sai trong modal ảnh |
| V6 | Opus | `a22dee9` | `NhomThuTienDot3Tests` (đọc file, không cần DB) |
| Chỉnh sau trình duyệt và nghiệm thu | Opus | commit kế tiếp | Bỏ lề âm `.hd-page`; ô thẻ co và xuống dòng, ô có `div` con hoặc nhiều dòng xếp dọc; số liệu không ngắt; hai ô Lịch sử xếp dọc dưới 576px và từ 992px (`col-12 col-sm-6 col-lg-12`, sửa S1 của co-van: số tổng tràn thẻ ở 1366px); tiêu đề tab "Lịch sử thanh toán"; cảnh báo SePay bọc chữ trong `div`; test thêm chốt `display: none`, `data-label` bảng tự ghép, ô 44px chỉ từ 768px |

Nghiệm thu: co-van Fable lần 1 CAN SUA (S1 số tổng Lịch sử tràn thẻ trên máy tính; S2 chưa thử gõ số trên phòng chưa khóa; S3 test còn hở; S4 lặt vặt). Opus sửa S1, S3, S4 và làm S2, có số đo bên dưới.

Kiểm trình duyệt (DB `quanlyphongtro_test`, Admin): năm màn ở 375px sáng và tối không tràn ngang, console không lỗi (ngoài SignalR mất kết nối lúc khởi động lại web); menu ⋯ của thẻ hóa đơn mở đủ, không bị thanh tab che; thanh chốt điện nước nằm ngay trên thanh tab; 1366px bảng vẫn dạng hàng. Khách thuê: Hóa đơn và Điện nước dùng xanh TroSync, không tràn ở 375px. Số tổng Lịch sử ở 375, 576, 992, 1024, 1366, 1440px: `scrollWidth = clientWidth`, nằm trong thẻ. Điện nước 375px trên phòng chưa khóa (`PA0bf27`, kỳ 11/2026): gõ số nhỏ hơn số cũ hiện lỗi dưới ô, không đè; gõ đúng hiện "+120 kWh", "+7 m³"; thanh "Chốt 1 phòng" bấm được, hộp lý do nhập tay nút xanh; chốt xong thẻ vẽ lại giữ bố cục và nhãn, trạng thái "Đã chốt". Ảnh `giao-dien-3-01…13` (.jpg, công cụ chụp xuất jpg).

## 5. Còn để ý

- Bảng hóa đơn ở 1366px có sidebar rộng hơn khung khi mã hóa đơn dài (dữ liệu test): có từ trước, `hoadon.css` chỉ cho cuộn ngang dưới 1200px. Chưa sửa vì cuộn ngang ở màn rộng sẽ cắt menu ⋯.
- `DienNuoc/Index.cshtml:47,60` dùng `DateTime.UtcNow.AddHours(7)` thay `VietnamTime` (ngoài phạm vi).
- `Web.IntegrationTests` toàn bộ: hai lượt đầu FAIL vì môi trường (DB test 23 GB, Npgsql hết thời gian chờ, fixture dọn dẹp thất bại nên mọi test bị tính thêm một FAIL). Người dùng cho dọn: xóa và tạo lại `quanlyphongtro_test` ngày 12/11/2026, chạy lại PASS 910/910 trong 3 phút 35 giây.
- Nhóm nút "Khoảng thời gian" của Lịch sử tràn khung lọc ở khoảng 1366px (có từ trước, `LichSuThanhToan/Index.cshtml` cột `col-xl-3`).
- Còn hex có chủ đích trong `diennuoc.css`: nền khung xem ảnh đồng hồ tối (`#1a1a1a`, dự phòng `#1d273b`) và chữ trắng trên nền thương hiệu (`#fff`); màu nước `#1d6fa5` / `#6bb7e8` theo lựa chọn của người dùng.
- Lỗi "chỉ số mới nhỏ hơn cũ" trong thẻ điện nước căn giữa theo lớp `text-center` có sẵn của ô.
