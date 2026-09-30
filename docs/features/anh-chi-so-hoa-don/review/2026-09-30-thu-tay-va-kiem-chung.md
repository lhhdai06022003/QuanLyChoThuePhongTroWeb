# Kết quả thử tay và kiểm chứng module ảnh chỉ số và hóa đơn

- Ngày: 30/09/2026
- Nhánh: `feature/anh-chi-so-hoa-don`
- Commit của các đợt: 1 `577ae59`, 2A `b1cf7f2`, 2B `07f37a3`, 3A `8f1a2c0`, 3B `9e627c0`; sửa sau thử tay `74db25a`, `937ad78`.

## 1. Kiểm chứng tự động (chạy lúc 30/09/2026 sau commit `937ad78`)

| Lệnh | Kết quả |
|---|---|
| `dotnet build QuanLyChoThuePhongTroWeb.sln` | 0 lỗi. 129 cảnh báo nullable (CS8618, CS8619, CS8601, CS8629, CS8602), không thuộc file mới của đợt 3B |
| `dotnet test QuanLyChoThuePhongTroWeb.sln` với `QLCTPT_TEST_CONNECTION_STRING` trỏ DB `quanlyphongtro_test` | Domain 100, Application 493, Infrastructure 250, Web 186; tổng 1029, 0 lỗi |
| `dotnet ef migrations has-pending-model-changes` | "No changes have been made to the model since the last migration" |
| `git diff --check` | Không có lỗi khoảng trắng |

Không tạo migration nào, không đổi schema.

## 2. Thử tay

Người dùng thử trực tiếp trên DB dev (`QuanLyPhongTroDb`) và xác nhận:
- Đợt 3A: "đã ổn" (30/09/2026).
- Đợt 3B: "tương đối ổn", đồng ý với bản cập nhật hiện tại (30/09/2026).
- Toàn module: "thử tay toàn module tôi đã làm rồi tương đối ổn".

Bản ghi này không liệt kê kết quả từng kịch bản nhỏ vì người dùng chỉ xác nhận ở mức tổng thể. Kịch bản 3B đã đưa cho người dùng gồm: bộ lọc và bản hủy, công bố nhiều hóa đơn, tab lịch sử trạng thái, XSS, cổng khách, quyền theo chi nhánh, màn sự cố, hủy và tạo bản thay thế, giờ hiển thị.

## 3. Lỗi phát hiện khi thử tay và cách xử lý

| Lỗi | Xử lý | Commit |
|---|---|---|
| Tải ảnh làm tải lại trang (jQuery nạp sau script của modal) | Bọc script trong `DOMContentLoaded` | đợt 3A `8f1a2c0` |
| Hóa đơn hủy vẫn khóa kỳ chỉ số, nhân viên không sửa được số | `HasLockedInvoiceAsync` bỏ qua hóa đơn `DaHuy` (spec §5.5a) | `8f1a2c0` |
| Tab lịch sử trạng thái đọc sai tên trường JSON | Sửa view, thêm test khóa tên trường | 3B `9e627c0` |
| Hóa đơn tính thừa dòng "Điện" và "Nước" (dịch vụ tạo tay có `LoaiDichVu = Khac` nên bị tính như dịch vụ cố định) | `MeterServiceClassifier` nhận diện theo loại, tên chuẩn và đơn vị kWh/m3; sửa cả luồng tính lại khi sửa số | `74db25a` |
| Kỳ 8 vẫn bị khóa vì đã có kỳ 9 nháp | Kỳ sau chỉ khóa kỳ trước khi đã duyệt hoặc đã có hóa đơn phát hành; sửa số thì cập nhật dây chuyền chỉ số cũ kỳ sau (spec §5.5b) | `937ad78` |

## 4. Việc còn mở, không chặn việc gộp

- Ảnh PNG bị từ chối "Nội dung tệp không khớp định dạng ảnh" khi đuôi tệp không đúng nội dung thật. `MeterImageUploadValidator` đối chiếu loại khai báo với magic bytes và hiện giữ nguyên; chưa có quyết định nới hay giữ.
- Gọi Gemini OCR với ảnh công tơ thật chưa được xác nhận trong phiên này.
- Chưa có nút giao diện để tạo bản thay thế (`-R1`, `-R2`); bản thay thế đi qua luồng phát sinh hóa đơn thường.
- Tra cứu chi tiết hóa đơn ở màn quản lý chỉ phân biệt "Đã thanh toán" và "Chưa thanh toán" trong chuỗi trạng thái; trạng thái "Thanh toán một phần" hiển thị đúng ở danh sách.
- Các thông báo lỗi "Kỳ này đã có kỳ sau" chưa đổi chữ dù nay chỉ áp dụng khi kỳ sau đã chốt hoặc có hóa đơn phát hành.
- API v1 hoãn theo spec §7.
- Nhánh chưa push, chưa gửi người B review (plan mục 10, dòng cuối).
