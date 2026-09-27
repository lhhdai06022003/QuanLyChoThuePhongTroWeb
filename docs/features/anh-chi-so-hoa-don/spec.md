# Đặc tả module 1: Ảnh chỉ số, OCR và phát hành hóa đơn

- Chủ sở hữu: Người A.
- Ngày cập nhật: 24/09/2026.
- Phạm vi: ảnh điện/nước, OCR gợi ý, nhân viên xác nhận, hóa đơn nháp, Admin chốt, công bố và gửi email hóa đơn trực tiếp; giữ nguyên schema hiện có trong giai đoạn đầu.
- Ngoài phạm vi: thu tiền và đối soát thuộc module 2; phòng công khai và giữ chỗ thuộc người B.

## 1. Hiện trạng và mục tiêu

Schema hiện có `AnhChiSoDongHo`, `DichVuDienNuocCuaPhong`, `HoaDon` và lịch sử trạng thái, nhưng workflow OCR → duyệt → nháp → chốt chưa được nối đầy đủ. Lệnh build và kiểm thử luồng thủ công đã chạy không chứng minh workflow mới hoàn thành.

Một phòng/kỳ cần chỉ số điện và nước chính thức trước khi tạo nháp. Khách gửi ảnh mới khi ảnh/OCR lỗi; AI ghi gợi ý, còn nhân viên đang được phân công tại chi nhánh hoặc Admin xác nhận số chính thức. Nháp không hiển thị cho khách. Giữ ảnh cũ, người/ngày xác nhận trên ảnh, lý do nhập tay trong ghi chú duyệt kỳ và lịch sử trạng thái hóa đơn bằng các trường hiện có. Không lưu số khách đề xuất riêng, nguồn/người xác nhận riêng từng loại hoặc lịch sử mọi lần sửa chỉ số.

## 2. Vai trò và phạm vi chi nhánh — giai đoạn hiện tại

Hệ thống hiện có `Admin`, `NhanVien`, `KhachThue` và `KhachVangLai`. Trong giai đoạn này, mọi tài khoản `NhanVien` có phân công `NhanVienChiNhanh.IsActive = true` tại chi nhánh của phòng/hóa đơn được cùng thực hiện các thao tác nhân viên của module. Không cấp quyền A/B riêng, không tạo bảng `nhan_vien_quyen_chi_nhanh` và không có màn hình Admin cấp quyền chức năng. Admin được thao tác trên mọi chi nhánh; chỉ Admin được chốt hoặc trả lại hóa đơn.

| Hành động | Khách thuê đúng phòng/kỳ | Nhân viên đang được phân công tại chi nhánh | Admin |
| --- | --- | --- | --- |
| Xem ảnh/chỉ số trong phạm vi được phép | Có, của phòng mình | Có | Có |
| Tải ảnh mới khi ảnh/OCR lỗi | Có, trước khi kỳ bị chốt | Có | Có |
| Thử OCR lại trên ảnh chưa xác nhận | Không | Có | Có |
| Xác nhận ảnh, nhập/chỉnh số chính thức | Không | Có | Có |
| Xem, tạo/tính lại nháp và gửi chờ duyệt | Không | Có | Có |
| Chốt hoặc trả lại nháp | Không | Không | Có |
| Công bố/thử gửi lại email, hủy hóa đơn đủ điều kiện | Không | Có | Có |
| Xem hóa đơn đã gửi, gồm bản đã hủy | Có, thuộc hợp đồng của mình | Có | Có |

Nhân viên được tự xác nhận ảnh/số do mình gửi. Với mỗi lệnh của nhân viên, Application kiểm tra role, phân công còn hiệu lực và chi nhánh thật của phòng/hợp đồng; không tin `ChiNhanhId`, role hoặc actor do client truyền và không chỉ dựa vào ẩn nút Web. Endpoint cũ cũng phải dùng cùng quy tắc. Khi thu hồi phân công, nhân viên mất mọi thao tác tại chi nhánh đó ngay. Vì tất cả nhân viên được phân công có cùng quyền, một nhân viên chỉ phụ trách HR nhưng vẫn mang role `NhanVien` tại chi nhánh cũng có thể thực hiện thao tác chỉ số/hóa đơn trong giai đoạn này; việc tách chức năng là phạm vi phát triển sau.

## 3. Ảnh, OCR và chỉ số

1. Mỗi ảnh gắn với một phòng, kỳ tháng/năm và đúng một loại đồng hồ. Khách chỉ gửi ảnh cho phòng thuộc hợp đồng của tài khoản mình, khi kỳ chưa chốt. Khi OCR sai hoặc ảnh lỗi, khách được tải ảnh mới; không có ô nhập số đề xuất của khách trong giai đoạn giữ nguyên schema. Ảnh cũ còn để đối chiếu. Ảnh mới chưa tự thay ảnh chính thức; khi nhân viên duyệt ảnh mới, chuyển cờ ảnh chính thức trong một transaction.
2. Lưu tệp và `AnhChiSoDongHo` trước rồi tự chạy OCR. OCR lỗi vẫn giữ ảnh. Nhân viên/Admin có thể thử OCR lại trên ảnh chưa xác nhận; retry đưa ảnh chưa xác nhận về trạng thái xử lý rồi cập nhật kết quả OCR của cùng ảnh; cần mở rộng method Domain hiện chỉ nhận ảnh mới/đang xử lý. Khách xử lý ảnh/OCR sai bằng cách tải ảnh mới.
3. Adapter AI chỉ phân tích ảnh và trả kết quả có cấu trúc. Application ghi số OCR hợp lệ vào `GiaTriAIGoiY`, độ tin cậy nếu có, thời gian và lỗi xử lý. AI không trực tiếp gọi DbContext và không ghi `GiaTriXacNhan`, `ChiSoDienMoi` hoặc `ChiSoNuocMoi`. Không tự duyệt theo ngưỡng tin cậy.
4. Nhân viên/Admin đối chiếu số AI với ảnh, rồi chọn gợi ý AI hoặc nhập số đúng. Khi xác nhận ảnh, lưu `GiaTriXacNhan`, `NguoiXacNhanId`, `NgayXacNhan` và ghi chú nếu cần; cập nhật số chính thức của kỳ cùng transaction. Nếu đổi từ ảnh sang nhập tay, bỏ cờ chính thức của ảnh cũ trong cùng transaction. Chỉ một ảnh chính thức mỗi loại/kỳ theo unique index hiện có.
5. Khi không có ảnh hoặc OCR không đọc được, nhân viên/Admin được nhập số chính thức thủ công với lý do bắt buộc. Nếu xác nhận trên ảnh không đọc được, lưu lý do ở `GhiChuXacNhan` của ảnh. Nếu hoàn toàn không có ảnh, lưu lý do trong `GhiChuDuyet` của kỳ, ghi rõ điện/nước trong cùng ghi chú; thao tác duyệt không được xóa ghi chú này. Không tạo ảnh giả. `NguoiDuyetId`/`NgayDuyet` chỉ biểu thị người và thời điểm duyệt cả kỳ; không suy ra người nhập hoặc nguồn của từng loại khi schema không lưu riêng.
6. Số mới không âm, tối đa ba chữ số thập phân, không nhỏ hơn số cũ từ kỳ trước gần nhất. Không sửa kỳ đã khóa bởi kỳ sau hoặc hóa đơn đã chốt. Application chỉ gửi duyệt/cập nhật `DichVuDienNuocCuaPhong` sang `DaDuyet` khi cả hai chỉ số được xác nhận qua ảnh chính thức hoặc được nhập thủ công trong cùng thao tác duyệt kỳ có lý do; không coi giá trị mặc định 0 là bằng chứng đã xác nhận. Nháp chỉ dùng kỳ `DaDuyet`.
7. Sửa số của ảnh đã xác nhận (người dùng chốt ngày 27/09/2026): Cho phép nhân viên có quyền `MeterReview` tại chi nhánh của phòng (đang được phân công, `IsActive`) hoặc Admin sửa số của ảnh đang là chỉ số chính thức (`DaXacNhan`, `DuocChonLamChiSoChinhThuc = true`). Bắt buộc có lý do từ 1 đến 500 ký tự. Số mới phải hợp lệ (không âm, tối đa 3 chữ số thập phân, không nhỏ hơn số cũ từ kỳ trước, khác số hiện tại). Chỉ sửa khi kỳ chưa có kỳ sau và chưa có hóa đơn ngoài trạng thái `Nhap`. Thao tác sửa trong một transaction: gọi `image.SuaGiaTriXacNhan` (cập nhật `GiaTriXacNhan`, `NguoiXacNhanId`, `NgayXacNhan`, lưu `[Sửa {cũ} → {mới}] {lyDo}`); cập nhật số tương ứng trên kỳ (nếu kỳ đã `DaDuyet` thì gọi `period.DuyetLai` để ghi người/ngày sửa mới nhất mà không mất ghi chú cũ); tự động tính lại hóa đơn nháp liên kết.

## 4. Hóa đơn nháp, duyệt và chốt

1. Admin hoặc nhân viên đang được phân công tại chi nhánh chọn kỳ và một/nhiều phòng rồi bấm tạo hàng loạt. Không tự tạo sau OCR. Preview báo thiếu hợp đồng, thiếu số duyệt, hóa đơn còn hiệu lực hoặc dữ liệu sai cho từng phòng.
2. Mỗi hợp đồng/kỳ chỉ có một hóa đơn hoạt động. Nháp có `TrangThaiPhatHanh = Nhap` riêng với trạng thái thanh toán. Tính bằng `HoaDonCalculatorService` và giá trị đã duyệt; tiền hai chữ số thập phân, chỉ số ba chữ số.
3. Trước chốt, sửa số chính thức làm tính lại phần điện/nước của nháp và tổng liên quan trong transaction; không âm thầm đổi tiền phòng/dịch vụ/sự cố đã chụp. Khi nháp đang `ChoDuyet`, Admin trả về `Nhap` có lý do trước khi sửa.
4. Nhân viên đang được phân công tại chi nhánh hoặc Admin gửi `Nhap` sang `ChoDuyet`. Chỉ Admin chốt hoặc trả lại có lý do. Nháp Admin tạo cũng đi qua `ChoDuyet`. Mỗi chuyển trạng thái ghi `LichSuTrangThaiHoaDon`.
5. Chỉ Admin chốt khi chỉ số đã duyệt, chi tiết/tổng tiền hợp lệ và không có hóa đơn hoạt động khác cùng hợp đồng/kỳ. Sau `DaChot`, khóa số kỳ, chi tiết và tổng tiền. Khách chỉ thấy hóa đơn ở `DaGui`.

## 5. Công bố, hủy và bản thay thế

1. Admin hoặc nhân viên đang được phân công tại chi nhánh công bố hóa đơn `DaChot`. Ghi `DaGui` và tạo `ThongBao` trên cổng khách trong một transaction. Sau commit, nếu khách có địa chỉ email, gửi email hóa đơn trực tiếp trong request; không tạo hàng chờ email hoặc cột trạng thái email. Không có email vẫn công bố thành công.
2. `DaGui` chỉ nghĩa là đã công bố trên cổng. Kết quả gửi email thành công/thất bại/không có địa chỉ chỉ trả trong phản hồi của lần bấm; lỗi tạo PDF hoặc SMTP không hoàn tác `DaGui` hay thông báo cổng. Không hiển thị trạng thái email đã lưu sau khi tải lại trang.
3. Admin hoặc nhân viên đang được phân công tại chi nhánh chỉ được hủy `DaChot`/`DaGui` khi chưa ghi nhận bất kỳ khoản thanh toán nào và không có yêu cầu/minh chứng đang chờ đối soát. Hủy cần lý do và lịch sử; bản hủy chỉ đọc, không cho thanh toán hoặc gửi lại email. Email đã gửi trước khi hủy không thể thu hồi. Khách vẫn xem bản hủy đã từng công bố, PDF phải ghi rõ đã hủy.
4. Bản hủy dùng `TrangThaiPhatHanh = DaHuy` và `IsDeleted = true` để unique index hiện có cho phép bản thay thế. Truy vấn hóa đơn hiệu lực loại bản hủy, truy vấn lịch sử lấy tường minh bản hủy. Mã bản thay thế dùng mã gốc của hợp đồng/kỳ với hậu tố `-R1`, `-R2`; cấp số trong transaction chống trùng. Không thêm khóa ngoại thay thế theo quyết định đã chốt.
5. Bản thay thế đi qua toàn bộ `Nhap → ChoDuyet → DaChot → DaGui`. Chỉ bản hiệu lực đã gửi được tạo yêu cầu thanh toán ở module 2.

## 6. Gửi email hóa đơn trực tiếp trong giai đoạn đầu

Không thêm `TrangThaiGuiEmail`, thời điểm gửi, số lần thử, lỗi gửi, khóa sự kiện email, lease worker hay các cột email khác vào `thong_bao`. Bảng này chỉ lưu thông báo trên cổng theo cấu trúc hiện có. Lệnh công bố ghi `DaGui` và một thông báo cổng trong transaction; Application gọi `IEmailService` sau khi commit, không gọi SMTP từ Controller. Kết quả email là dữ liệu tạm của phản hồi, không lưu vào `HoaDon` hay `ThongBao`.

### Gửi hàng loạt và gửi lại

Một lệnh hàng loạt nhận danh sách `HoaDonId` và xử lý từng hóa đơn độc lập. Phản hồi trả từng `HoaDonId` với kết quả công bố trên cổng và kết quả email tức thời: gửi thành công, lỗi tạo PDF/SMTP kèm thông báo an toàn, hoặc không có địa chỉ email. Bắt lỗi theo từng hóa đơn để tiếp tục xử lý các hóa đơn còn lại. Một hóa đơn lỗi không hoàn tác các hóa đơn đã công bố. HTTP không trả lỗi chung kiểu "toàn lô thất bại" khi một số hóa đơn đã công bố; giao diện giữ kết quả từng dòng trong lần thao tác đó. Không có thống kê trạng thái email bền vững sau khi tải lại trang.

Lệnh "Gửi hóa đơn" lặp trên hóa đơn `DaGui` chỉ trả "đã công bố", không đổi `NgayGui`, không tạo thêm `ThongBao`/lịch sử và không tự gửi thêm email. Transaction khóa hàng hóa đơn hoặc cập nhật có điều kiện `DaChot → DaGui` để hai request đồng thời chỉ công bố một lần. Admin hoặc nhân viên đúng chi nhánh có thao tác riêng "Gửi lại email" cho hóa đơn `DaGui` còn hiệu lực; thao tác này gửi trực tiếp và trả kết quả ngay, không lưu trạng thái. Giao diện báo rõ có thể gửi trùng vì hệ thống không biết email lần trước đã đến hay chưa. Hóa đơn `DaHuy` không được gửi lại.

Nếu ứng dụng ngừng sau khi commit công bố nhưng trước khi gọi SMTP, hoặc client mất phản hồi sau khi SMTP đã nhận thư, hệ thống không thể tự xác định kết quả email. Không tự động retry; nhân viên dùng thao tác gửi lại khi cần và chấp nhận khả năng trùng thư. Hóa đơn thay thế có `HoaDonId` mới và đi lại toàn bộ luồng công bố.

### Hai job nhắc email hiện có

`JsonContractExpiryAlertStateStore` và `JsonInvoiceReminderStateStore` vẫn được hai job cảnh báo hợp đồng/nhắc nợ dùng trong giai đoạn này. Lựa chọn bỏ trạng thái chỉ áp dụng cho email hóa đơn do nhân viên bấm gửi; việc thay hai file JSON cần đặc tả và triển khai riêng ở đợt sau. Không mô tả JSON là phương án lâu dài. Ngay trong đợt này, truy vấn nhắc nợ phải chỉ lấy hóa đơn `DaGui` còn hiệu lực, chưa thanh toán; quá hạn theo `HanThanhToan`, hoặc `NgayGui` nếu thiếu hạn, để job cũ không gửi nháp hoặc hóa đơn đã hủy.

## 7. Giữ nguyên schema và ranh giới kỹ thuật

- Giai đoạn này giữ nguyên schema ảnh/chỉ số, hóa đơn và `thong_bao`; không tạo migration mới, không thêm bảng/cột hoặc FK hóa đơn thay thế. Nếu nghiệp vụ sau này cần số khách đề xuất tách biệt hoặc lịch sử sửa chỉ số, phải chốt thay đổi schema ở đợt riêng.
- Application sở hữu use case, xác thực quyền, chuyển trạng thái, gửi email qua `IEmailService` sau commit và trả kết quả từng hóa đơn; DTO/`UploadFile`/store không dùng kiểu HTTP/EF. Infrastructure sở hữu EF, Cloudinary, Gemini và MailKit. Web/API v1 gọi cùng Application Service; endpoint gửi email cũ không được bỏ qua quy tắc mới.
- Kiểm tra quyền và chi nhánh tại Application mỗi lần công bố/gửi lại. Sửa truy vấn nhắc nợ để loại nháp, hóa đơn chưa công bố và bản đã hủy; chưa thay hai state store JSON hiện có.

## 8. Điều kiện nghiệm thu

1. Khách đúng phòng tải ảnh mới; sai phòng hoặc kỳ đã chốt bị chặn. OCR đọc được ghi `GiaTriAIGoiY`; lỗi vẫn giữ ảnh để đối chiếu. AI/khách không tự ghi số chính thức và không có trường số khách đề xuất riêng.
2. Nhân viên không có phân công còn hiệu lực tại chi nhánh bị từ chối tại Application; mọi nhân viên được phân công có cùng quyền thao tác nhân viên và được duyệt ảnh mình gửi. Chỉ Admin chốt/trả lại hóa đơn. Nhập tay thiếu lý do trong ghi chú hiện có, số âm/nhỏ hơn kỳ trước, hai ảnh chính thức cùng loại/kỳ đều bị chặn.
3. Tạo hàng loạt có preview và chống trùng; chỉnh số trước chốt tính lại nháp. Chỉ Admin chốt/trả lại; khách không thấy hóa đơn trước `DaGui`.
4. Gửi hàng loạt trả kết quả công bố và email ngay cho từng hóa đơn; một email lỗi không hoàn tác hóa đơn khác hoặc công bố trên cổng. Bấm "Gửi hóa đơn" lặp và request đồng thời không tạo thông báo/lịch sử công bố trùng hoặc tự gửi email lần nữa. Thao tác "Gửi lại email" trả kết quả tức thời và có cảnh báo khả năng gửi trùng. Không có cột/trạng thái email được lưu; nhắc nợ không gửi cho nháp/chưa gửi/bản hủy.
5. Chặn hủy khi có tiền/yêu cầu đang đối soát; bản hủy đã công bố chỉ đọc, bản thay thế có hậu tố và đi đủ workflow.
6. Unit test Domain/Application, PostgreSQL integration cho index ảnh/chỉ số và transaction công bố trên schema hiện có; Web integration cho HTTP/quyền và kết quả email bằng SMTP giả. Full suite dùng database riêng hậu tố `_test` hoặc `_integration_test`; EF báo không có pending model changes so với schema nền.

## 9. Giới hạn

OCR có thể sai; quyết định chỉ số chính thức thuộc nhân viên đúng chi nhánh hoặc Admin. Schema hiện tại không lưu số khách đề xuất tách biệt, nguồn/người xác nhận từng loại khi nhập tay hoặc lịch sử mọi lần sửa chỉ số. Sau khi tải lại trang không còn bằng chứng ứng dụng về kết quả email hóa đơn; gửi lại thủ công có thể tạo email trùng. Hai job nhắc nợ/cảnh báo hợp đồng còn phụ thuộc file JSON cũ cho tới đợt xử lý riêng. Quan hệ hóa đơn thay thế chỉ suy từ hợp đồng/kỳ/mã, không phải FK.

## 10. Phát triển sau: quyền riêng từng nhân viên (phương án 2)

Khi cần nhân viên có quyền A nhưng không có quyền B, giữ `Role.NhanVien` và các use case hiện tại; thay chính sách kiểm tra trong Application bằng grant theo từng phân công chi nhánh. Đây là giai đoạn sau, không thuộc migration, UI hoặc điều kiện nghiệm thu hiện tại.

Bảng dự kiến `nhan_vien_quyen_chi_nhanh` có `NhanVienQuyenChiNhanhId` (PK), `NhanVienChiNhanhId` (FK), `PermissionCode` varchar(64), `NgayCap` UTC, `NguoiCapId` (FK `NguoiDung`; Application yêu cầu Admin), `NgayThuHoi` nullable, `NguoiThuHoiId` nullable và `LyDoThuHoi` nullable. Mỗi lần cấp là một hàng; unique partial index trên (`NhanVienChiNhanhId`, `PermissionCode`) khi `NgayThuHoi IS NULL` giữ một grant hiệu lực và cho phép lưu lịch sử thu hồi/cấp lại. Mã dự kiến: `Meter.Upload`, `Meter.Review`, `Invoice.Draft`, `Invoice.Submit`, `Invoice.Send`, `Invoice.Cancel`; quyền chốt/trả lại vẫn chỉ Admin.

Lúc nâng cấp cần migration mới, màn Admin cấp/thu hồi, kiểm tra grant trong Application, test theo chức năng/chi nhánh và kế hoạch cấp quyền cho nhân viên hiện hữu trước khi bật chặn quyền mới. Không thể suy ra bộ quyền mong muốn của từng nhân viên từ role `NhanVien` hiện tại; Admin phải xác nhận phân công. Không sửa ngược lịch sử hóa đơn/ảnh để chuyển mô hình phân quyền.
