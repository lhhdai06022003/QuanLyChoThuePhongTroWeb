# Đặc tả module 1: Ảnh chỉ số, OCR và phát hành hóa đơn

- Chủ sở hữu: Người A.
- Ngày cập nhật: 30/09/2026 (27/09: quyết định đợt 1–3 §4.6, §5.6–5.8, §6, §7.4, §11; phân công chi nhánh §12; chia đợt §13. 28/09: làm rõ đợt 2B §5.7–5.9, §6 modal gửi email cũ. 29/09: §13 đợt 2B xong; quyết định đợt 3A ở §3.8, §11.2 và tách §13 thành 3A/3B. 30/09: đợt 3A xong; bản hủy không còn khóa kỳ chỉ số, §5.5a; kỳ sau nháp không khóa kỳ trước, §5.5b; 30/09 sau review final: §3.6, §3.7 khớp §5.5a và §5.5b; §13 đợt 3B xong).
- Phạm vi: ảnh điện/nước, OCR gợi ý, nhân viên xác nhận, hóa đơn nháp, Admin chốt, công bố và gửi email hóa đơn trực tiếp; giữ nguyên schema hiện có trong giai đoạn đầu. Theo `docs/roadmap-60-ngay.md`, người A sở hữu **trọn luồng** module 1, tức cả màn quản lý (`Areas/QuanLyNhaTro`) lẫn các màn phía khách thuê (`Areas/KhachThue`) thuộc module này.
- Ngoài phạm vi: thu tiền và đối soát thuộc module 2 (cũng do A làm, đặc tả riêng); phòng công khai, lịch xem, khách vãng lai, giữ chỗ và area `KhachVangLai` thuộc người B.

## 1. Hiện trạng và mục tiêu

Schema hiện có `AnhChiSoDongHo`, `DichVuDienNuocCuaPhong`, `HoaDon` và lịch sử trạng thái, nhưng workflow OCR → duyệt → nháp → chốt chưa được nối đầy đủ. Lệnh build và kiểm thử luồng thủ công đã chạy không chứng minh workflow mới hoàn thành.

Một phòng/kỳ cần chỉ số điện và nước chính thức trước khi tạo nháp. Khách gửi ảnh mới khi ảnh/OCR lỗi; AI ghi gợi ý, còn nhân viên đang được phân công tại chi nhánh hoặc Admin xác nhận số chính thức. Nháp không hiển thị cho khách. Giữ ảnh cũ, người/ngày xác nhận trên ảnh, lý do nhập tay trong ghi chú duyệt kỳ và lịch sử trạng thái hóa đơn bằng các trường hiện có. Không lưu số khách đề xuất riêng, nguồn/người xác nhận riêng từng loại hoặc lịch sử mọi lần sửa chỉ số.

## 2. Vai trò và phạm vi chi nhánh — giai đoạn hiện tại

Hệ thống hiện có `Admin`, `NhanVien`, `KhachThue` và `KhachVangLai`. Trong giai đoạn này, mọi tài khoản `NhanVien` có phân công `NhanVienChiNhanh.IsActive = true` tại chi nhánh của phòng/hóa đơn được cùng thực hiện các thao tác nhân viên của module. Không cấp quyền A/B riêng, không tạo bảng `nhan_vien_quyen_chi_nhanh` và không có màn hình Admin cấp quyền chức năng. Admin phân công hoặc thu hồi nhân viên theo chi nhánh trên màn §12 (đợt 2A); chỉ Admin vào được màn tài khoản, chi nhánh và phân công. Admin được thao tác trên mọi chi nhánh; chỉ Admin được chốt hoặc trả lại hóa đơn.

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
6. Số mới không âm, tối đa ba chữ số thập phân, không nhỏ hơn số cũ từ kỳ trước gần nhất. Không sửa kỳ đã bị khóa: kỳ có hóa đơn hiệu lực ở `ChoDuyet`, `DaChot` hoặc `DaGui` (hóa đơn `Nhap` và `DaHuy` không khóa, mục 5a), hoặc có kỳ sau đã `DaDuyet` hay kỳ sau đã có hóa đơn hiệu lực khác `Nhap` và `DaHuy` (kỳ sau còn nháp không khóa, mục 5b). Application chỉ gửi duyệt/cập nhật `DichVuDienNuocCuaPhong` sang `DaDuyet` khi cả hai chỉ số được xác nhận qua ảnh chính thức hoặc được nhập thủ công trong cùng thao tác duyệt kỳ có lý do; không coi giá trị mặc định 0 là bằng chứng đã xác nhận. Nháp chỉ dùng kỳ `DaDuyet`.
7. Sửa số của ảnh đã xác nhận (người dùng chốt ngày 27/09/2026): Cho phép nhân viên có quyền `MeterReview` tại chi nhánh của phòng (đang được phân công, `IsActive`) hoặc Admin sửa số của ảnh đang là chỉ số chính thức (`DaXacNhan`, `DuocChonLamChiSoChinhThuc = true`). Bắt buộc có lý do từ 1 đến 500 ký tự. Số mới phải hợp lệ (không âm, tối đa 3 chữ số thập phân, không nhỏ hơn số cũ từ kỳ trước, khác số hiện tại). Chỉ sửa khi kỳ chưa bị khóa theo mục 6: kỳ không có hóa đơn hiệu lực ở `ChoDuyet`, `DaChot` hoặc `DaGui` (bản `DaHuy` không khóa, §5.5a), và không có kỳ sau đã `DaDuyet` hoặc đã có hóa đơn hiệu lực khác `Nhap` và `DaHuy` (kỳ sau còn nháp thì vẫn sửa được, chỉ số cũ của kỳ sau được cập nhật dây chuyền, §5.5b). Thao tác sửa trong một transaction: gọi `image.SuaGiaTriXacNhan` (cập nhật `GiaTriXacNhan`, `NguoiXacNhanId`, `NgayXacNhan`, lưu `[Sửa {cũ} → {mới}] {lyDo}`); cập nhật số tương ứng trên kỳ (nếu kỳ đã `DaDuyet` thì gọi `period.DuyetLai` để ghi người/ngày sửa mới nhất mà không mất ghi chú cũ); tự động tính lại hóa đơn nháp liên kết.
8. Quy tắc tải ảnh (người dùng chốt ngày 29/09/2026, áp dụng từ đợt 3A; Application kiểm tra, không tin giao diện):
   - Không ai được tải ảnh cho kỳ ở tương lai so với tháng hiện tại giờ VN.
   - Nếu phòng có hợp đồng trong tháng trước mà kỳ tháng trước chưa có bản ghi hoặc chưa `DaDuyet`, không ai được tải ảnh kỳ này. Lý do: kỳ sau đã chốt hoặc đã có hóa đơn phát hành khóa kỳ trước (xem mục 5b, kỳ sau chỉ là bản nháp thì không khóa).
   - Khách chỉ được tải ảnh cho tháng hiện tại hoặc tháng trước, và không được tải khi kỳ đã `DaDuyet`. Nhân viên/Admin vẫn tải và xác nhận được trên kỳ đã duyệt để sửa sai.
   - Xác nhận ảnh trên kỳ đã `DaDuyet` gọi `DuyetLai` để ghi người/ngày duyệt mới nhất, giống sửa số đã xác nhận (mục 7).

## 4. Hóa đơn nháp, duyệt và chốt

1. Admin hoặc nhân viên đang được phân công tại chi nhánh chọn kỳ và một/nhiều phòng rồi bấm tạo hàng loạt. Không tự tạo sau OCR. Preview báo thiếu hợp đồng, thiếu số duyệt, hóa đơn còn hiệu lực hoặc dữ liệu sai cho từng phòng.
2. Mỗi hợp đồng/kỳ chỉ có một hóa đơn hoạt động. Nháp có `TrangThaiPhatHanh = Nhap` riêng với trạng thái thanh toán. Tính bằng `HoaDonCalculatorService` và giá trị đã duyệt; tiền hai chữ số thập phân, chỉ số ba chữ số.
3. Trước chốt, sửa số chính thức làm tính lại phần điện/nước của nháp và tổng liên quan trong transaction; không âm thầm đổi tiền phòng/dịch vụ/sự cố đã chụp. Khi nháp đang `ChoDuyet`, Admin trả về `Nhap` có lý do trước khi sửa.
4. Nhân viên đang được phân công tại chi nhánh hoặc Admin gửi `Nhap` sang `ChoDuyet`. Chỉ Admin chốt hoặc trả lại có lý do. Nháp Admin tạo cũng đi qua `ChoDuyet`. Mỗi chuyển trạng thái ghi `LichSuTrangThaiHoaDon`.
5. Chỉ Admin chốt khi chỉ số đã duyệt, chi tiết/tổng tiền hợp lệ và không có hóa đơn hoạt động khác cùng hợp đồng/kỳ. Sau `DaChot`, khóa số kỳ, chi tiết và tổng tiền. Khách chỉ thấy hóa đơn ở `DaGui`.
6. **Chi phí sự cố (chốt 27/09/2026).** Sự cố được tính vào hóa đơn của tháng chứa `NgayXuLy` (giờ Việt Nam).
   - **Điều kiện tính:** đúng phòng và đúng khách (`NguoiThueId`) của hợp đồng, `TrangThai = DaHoanThanh`, `CongVaoHoaDon = true`, `ChiPhiSuaChua > 0`, chưa xóa.
   - **Ý nghĩa của cờ:** `CongVaoHoaDon` nghĩa là "sự cố được tính phí". Cờ này không bị tắt khi tạo hay hủy hóa đơn. Nhờ vậy, bản thay thế tự tính lại đủ các sự cố của tháng, và không có sự cố nào bị tính hai lần vì mỗi hợp đồng/tháng chỉ có một hóa đơn hoạt động.
   - **Gán `NgayXuLy`:** `NgayXuLy = UtcNow` mỗi khi trạng thái **chuyển sang** `DangXuLy`, `DaHoanThanh` hoặc `DaHuy`. Lưu lại mà không đổi trạng thái thì giữ nguyên `NgayXuLy`.
   - **Sửa sự cố làm đổi số tiền cần tính hoặc đổi tháng:** nếu hóa đơn của tháng đó còn ở `Nhap` thì tính lại các dòng sự cố và tổng tiền trong cùng transaction; nếu hóa đơn đã qua `Nhap` thì từ chối và yêu cầu Admin trả lại hoặc hủy trước; nếu chưa có hóa đơn thì lưu bình thường.
   - **Dữ liệu cũ:** các sự cố đã được code cũ tính vào hóa đơn đang có `CongVaoHoaDon = false`. Nếu hủy những hóa đơn đó, nhân viên phải bật lại cờ trước khi tạo bản thay thế.

## 5. Công bố, hủy và bản thay thế

1. Admin hoặc nhân viên đang được phân công tại chi nhánh công bố hóa đơn `DaChot`. Ghi `DaGui` và tạo `ThongBao` trên cổng khách trong một transaction. Sau commit, nếu khách có địa chỉ email, gửi email hóa đơn trực tiếp trong request; không tạo hàng chờ email hoặc cột trạng thái email. Không có email vẫn công bố thành công.
2. `DaGui` chỉ nghĩa là đã công bố trên cổng. Kết quả gửi email thành công/thất bại/không có địa chỉ chỉ trả trong phản hồi của lần bấm; lỗi tạo PDF hoặc SMTP không hoàn tác `DaGui` hay thông báo cổng. Không hiển thị trạng thái email đã lưu sau khi tải lại trang.
3. Admin hoặc nhân viên đang được phân công tại chi nhánh chỉ được hủy `DaChot`/`DaGui` khi chưa ghi nhận bất kỳ khoản thanh toán nào và không có yêu cầu/minh chứng đang chờ đối soát. Hủy cần lý do và lịch sử; bản hủy chỉ đọc, không cho thanh toán hoặc gửi lại email. Email đã gửi trước khi hủy không thể thu hồi. Khách vẫn xem bản hủy đã từng công bố, PDF phải ghi rõ đã hủy.
4. Bản hủy dùng `TrangThaiPhatHanh = DaHuy` và `IsDeleted = true` để unique index hiện có cho phép bản thay thế. Truy vấn hóa đơn hiệu lực loại bản hủy, truy vấn lịch sử lấy tường minh bản hủy. Mã bản thay thế dùng mã gốc của hợp đồng/kỳ với hậu tố `-R1`, `-R2`; cấp số trong transaction chống trùng. Không thêm khóa ngoại thay thế theo quyết định đã chốt.
5. Bản thay thế đi qua toàn bộ `Nhap → ChoDuyet → DaChot → DaGui`. Chỉ bản hiệu lực đã gửi được tạo yêu cầu thanh toán ở module 2. Domain `KiemTraDuDieuKienYeuCauThanhToan` hiện vẫn chấp nhận cả `DaChot`; trong đợt 2 phải siết lại để chỉ chấp nhận `DaGui`.
5a. **Bản hủy không khóa kỳ chỉ số (chốt 30/09/2026):** kỳ chỉ số chỉ bị khóa khi có hóa đơn hiệu lực khác `Nhap` và khác `DaHuy` (`ChoDuyet`, `DaChot`, `DaGui`). Sau khi hủy, nhân viên đúng chi nhánh hoặc Admin sửa lại ảnh/số của kỳ rồi tạo bản thay thế theo mục 5.4–5.5. Bản hủy giữ nguyên số tiền đã chụp, không tính lại theo chỉ số mới. Bản thay thế đã công bố (`DaGui`) khóa kỳ trở lại. Thay đổi này đảo quy tắc đợt 3A trước đó (bản hủy vẫn khóa kỳ); code ở `MeterImageStore.HasLockedInvoiceAsync`, test `HasLockedInvoiceAsync_WhenOnlyCancelledInvoice_ReturnsFalse`.
5b. **Kỳ sau chỉ là bản nháp không khóa kỳ trước (chốt 30/09/2026, phương án B):** kỳ chỉ số chỉ bị khóa bởi kỳ sau khi kỳ sau đã `DaDuyet` hoặc đã có hóa đơn hiệu lực khác `Nhap` và khác `DaHuy`. Kỳ sau còn `Nhap`/`ChoDuyet`/`TuChoi` và chưa có hóa đơn phát hành thì vẫn được sửa kỳ trước. Khi số mới của kỳ trước đổi, hệ thống trong cùng giao dịch cập nhật dây chuyền chỉ số cũ của các kỳ sau còn nháp: kỳ sau chưa có số riêng (mới bằng cũ) thì cả số cũ và số mới dịch theo; kỳ sau đã có số mới riêng thì chỉ đổi số cũ. Nếu số mới riêng đó nhỏ hơn số cũ mới, thao tác bị từ chối kèm thông báo kỳ nào cần sửa trước, không lưu gì. Hóa đơn `Nhap` của kỳ sau (nếu có) được tính lại. Áp dụng cho xác nhận ảnh, sửa số đã xác nhận và duyệt kỳ. Code ở `MeterImageStore.HasSubsequentPeriodAsync`, `DienNuocStore.GetLockedRoomIdsAsync` và `MeterReadingWorkflowService.CascadeSubsequentPeriodsAsync`. Quy tắc "khóa vĩnh viễn kỳ trước" ở phần điều kiện tải ảnh chỉ còn đúng cho kỳ sau đã chốt hoặc đã có hóa đơn phát hành.
6. **Người nhận (chốt 27/09/2026):** thông báo trên cổng và email chỉ gửi cho **khách đứng tên hợp đồng** (`HopDong.NguoiThueId`), đúng một thông báo và một email cho mỗi hóa đơn. Thông báo đi tới tài khoản `NguoiDung` đang hoạt động gắn với người thuê đó. Nếu người thuê không có tài khoản hoạt động, hóa đơn vẫn được công bố và kết quả trả về ghi "khách chưa có tài khoản cổng". Thành viên khác của hợp đồng không nhận thông báo hay email trong giai đoạn này.
7. **Hạn thanh toán (chốt 27/09/2026, làm rõ 28/09/2026):** khi công bố, gán `HanThanhToan` bằng **23:59:59 giờ Việt Nam của ngày (ngày gửi theo giờ Việt Nam + N ngày)**, lưu UTC. Ví dụ: công bố lúc 15:00 ngày 28/09 giờ VN, N = 7, thì hạn là 23:59:59 ngày 05/10 giờ VN, tức 16:59:59 ngày 05/10 UTC. N lấy từ cấu hình `InvoiceIssuanceOptions:SoNgayHanThanhToan`, mặc định 7, và phải > 0 (kiểm tra lúc khởi động). Hóa đơn đã có `HanThanhToan` thì không ghi đè. Lệnh công bố lặp trên `DaGui` không đổi hạn.
8. **Nội dung thông báo cổng:** tiêu đề "Hóa đơn tháng {T}/{N}", nội dung gồm mã hóa đơn, tổng tiền và hạn thanh toán, `LinhVuc = "HoaDon"`. `LinkDieuHuong = /KhachThue/HoaDon?hoaDonId={HoaDonId}` (chốt 28/09/2026): cổng khách chưa có trang chi tiết riêng, nên đợt 2B dẫn về trang danh sách hóa đơn; đợt 3 (K2) đọc tham số `hoaDonId` để mở đúng hóa đơn, không phải sửa link đã lưu. Sau khi commit, đẩy real-time qua `IThongBaoNotifier` theo kiểu best-effort: lỗi SignalR chỉ ghi log, không ảnh hưởng kết quả công bố.
9. **Hạn thanh toán trong email (chốt 28/09/2026):** email hóa đơn (công bố và gửi lại) ghi "Hạn thanh toán: hết ngày dd/MM/yyyy" theo giờ Việt Nam. PDF, màn quản lý và cổng khách chưa hiển thị hạn ở đợt 2B; để đợt 3.

## 6. Gửi email hóa đơn trực tiếp trong giai đoạn đầu

Không thêm `TrangThaiGuiEmail`, thời điểm gửi, số lần thử, lỗi gửi, khóa sự kiện email, lease worker hay các cột email khác vào `thong_bao`. Bảng này chỉ lưu thông báo trên cổng theo cấu trúc hiện có. Lệnh công bố ghi `DaGui` và một thông báo cổng trong transaction; Application gọi `IEmailService` sau khi commit, không gọi SMTP từ Controller. Kết quả email là dữ liệu tạm của phản hồi, không lưu vào `HoaDon` hay `ThongBao`.

### Gửi hàng loạt và gửi lại

Một lệnh hàng loạt nhận danh sách `HoaDonId`, **tối đa 50 hóa đơn mỗi lần** (chốt 27/09/2026; vượt quá thì trả 400 trước khi xử lý), bỏ `HoaDonId` trùng, và xử lý từng hóa đơn độc lập theo thứ tự `HoaDonId` tăng dần. Phản hồi trả từng `HoaDonId` với kết quả công bố trên cổng và kết quả email tức thời: gửi thành công, lỗi tạo PDF/SMTP kèm thông báo an toàn, hoặc không có địa chỉ email. Bắt lỗi theo từng hóa đơn để tiếp tục xử lý các hóa đơn còn lại. Một hóa đơn lỗi không hoàn tác các hóa đơn đã công bố. HTTP không trả lỗi chung kiểu "toàn lô thất bại" khi một số hóa đơn đã công bố; giao diện giữ kết quả từng dòng trong lần thao tác đó. Không có thống kê trạng thái email bền vững sau khi tải lại trang.

Lệnh "Gửi hóa đơn" lặp trên hóa đơn `DaGui` chỉ trả "đã công bố", không đổi `NgayGui`, không tạo thêm `ThongBao`/lịch sử và không tự gửi thêm email. Transaction khóa hàng hóa đơn hoặc cập nhật có điều kiện `DaChot → DaGui` để hai request đồng thời chỉ công bố một lần. Admin hoặc nhân viên đúng chi nhánh có thao tác riêng "Gửi lại email" cho hóa đơn `DaGui` còn hiệu lực; thao tác này gửi trực tiếp và trả kết quả ngay, không lưu trạng thái. Giao diện báo rõ có thể gửi trùng vì hệ thống không biết email lần trước đã đến hay chưa. Hóa đơn `DaHuy` không được gửi lại.

**Modal "Gửi email hàng loạt" cũ (chốt 28/09/2026):** màn Hóa đơn đang có modal gửi lại email lần lượt cho mọi hóa đơn `DaGui` chưa thanh toán, không cảnh báo gửi trùng. Đợt 2B gỡ modal này khỏi giao diện; gửi lại email chỉ làm cho từng hóa đơn. Đợt 3 thay bằng "Công bố các hóa đơn đã chọn" (§11.3 Q2). Endpoint `GetUnpaidList` giữ nguyên.

Nếu ứng dụng ngừng sau khi commit công bố nhưng trước khi gọi SMTP, hoặc client mất phản hồi sau khi SMTP đã nhận thư, hệ thống không thể tự xác định kết quả email. Không tự động retry; nhân viên dùng thao tác gửi lại khi cần và chấp nhận khả năng trùng thư. Hóa đơn thay thế có `HoaDonId` mới và đi lại toàn bộ luồng công bố.

### Hai job nhắc email hiện có

`JsonContractExpiryAlertStateStore` và `JsonInvoiceReminderStateStore` vẫn được hai job cảnh báo hợp đồng/nhắc nợ dùng trong giai đoạn này. Lựa chọn bỏ trạng thái chỉ áp dụng cho email hóa đơn do nhân viên bấm gửi; việc thay hai file JSON cần đặc tả và triển khai riêng ở đợt sau. Không mô tả JSON là phương án lâu dài. Ngay trong đợt này, truy vấn nhắc nợ phải chỉ lấy hóa đơn `DaGui` còn hiệu lực, chưa thanh toán; quá hạn theo `HanThanhToan`, hoặc `NgayGui` nếu thiếu hạn, để job cũ không gửi nháp hoặc hóa đơn đã hủy.

## 7. Giữ nguyên schema và ranh giới kỹ thuật

- Giai đoạn này giữ nguyên schema ảnh/chỉ số, hóa đơn và `thong_bao`; không tạo migration mới, không thêm bảng/cột hoặc FK hóa đơn thay thế. Nếu nghiệp vụ sau này cần số khách đề xuất tách biệt hoặc lịch sử sửa chỉ số, phải chốt thay đổi schema ở đợt riêng.
- Application sở hữu use case, xác thực quyền, chuyển trạng thái, gửi email qua `IEmailService` sau commit và trả kết quả từng hóa đơn; DTO/`UploadFile`/store không dùng kiểu HTTP/EF. Infrastructure sở hữu EF, Cloudinary, Gemini và MailKit. Web/API v1 gọi cùng Application Service; endpoint gửi email cũ không được bỏ qua quy tắc mới.
- Kiểm tra quyền và chi nhánh tại Application mỗi lần công bố/gửi lại. Sửa truy vấn nhắc nợ để loại nháp, hóa đơn chưa công bố và bản đã hủy; chưa thay hai state store JSON hiện có.
- **Phân quyền giai đoạn này (chốt 27/09/2026):** chỉ có role `NhanVien` kèm phân công chi nhánh. `EmployeeActionCodes` là nhãn thao tác, không phải quyền riêng: nhân viên được phân công đều qua mọi mã, trừ nhóm chỉ Admin (chốt, trả lại). Không thêm mã mới cho từng thao tác.
- **API v1 (chốt 27/09/2026):** hoãn. Đợt 3 chỉ làm MVC. `Web/Api/V1` giữ nguyên khung và sẽ được đặc tả ở đợt sau.

## 8. Điều kiện nghiệm thu

1. Khách đúng phòng tải ảnh mới; sai phòng hoặc kỳ đã chốt bị chặn. OCR đọc được ghi `GiaTriAIGoiY`; lỗi vẫn giữ ảnh để đối chiếu. AI/khách không tự ghi số chính thức và không có trường số khách đề xuất riêng.
2. Nhân viên không có phân công còn hiệu lực tại chi nhánh bị từ chối tại Application; mọi nhân viên được phân công có cùng quyền thao tác nhân viên và được duyệt ảnh mình gửi. Chỉ Admin chốt/trả lại hóa đơn. Nhập tay thiếu lý do trong ghi chú hiện có, số âm/nhỏ hơn kỳ trước, hai ảnh chính thức cùng loại/kỳ đều bị chặn.
3. Tạo hàng loạt có preview và chống trùng; chỉnh số trước chốt tính lại nháp. Chỉ Admin chốt/trả lại; khách không thấy hóa đơn trước `DaGui`.
4. Gửi hàng loạt trả kết quả công bố và email ngay cho từng hóa đơn; một email lỗi không hoàn tác hóa đơn khác hoặc công bố trên cổng. Bấm "Gửi hóa đơn" lặp và request đồng thời không tạo thông báo/lịch sử công bố trùng hoặc tự gửi email lần nữa. Thao tác "Gửi lại email" trả kết quả tức thời và có cảnh báo khả năng gửi trùng. Không có cột/trạng thái email được lưu; nhắc nợ không gửi cho nháp/chưa gửi/bản hủy.
5. Chặn hủy khi có tiền/yêu cầu đang đối soát; bản hủy đã công bố chỉ đọc, bản thay thế có hậu tố và đi đủ workflow.
6. Unit test Domain/Application, PostgreSQL integration cho index ảnh/chỉ số và transaction công bố trên schema hiện có; Web integration cho HTTP/quyền và kết quả email bằng SMTP giả. Full suite dùng database riêng hậu tố `_test` hoặc `_integration_test`; EF báo không có pending model changes so với schema nền.
7. Sự cố chỉ vào hóa đơn tháng chứa `NgayXuLy` khi đã hoàn thành và được tính phí. Bản thay thế có lại đủ các sự cố đó. Sửa sự cố khi hóa đơn tháng đó đã qua `Nhap` thì bị chặn.
8. Công bố gán hạn thanh toán bằng `NgayGui + N`. Chỉ khách đứng tên hợp đồng nhận thông báo và email. Lô trên 50 hóa đơn bị từ chối. Yêu cầu thanh toán chỉ tạo được cho bản `DaGui`.
9. Giao diện theo §11. Khách thuê tải ảnh chỉ số và xem hóa đơn đã công bố trên điện thoại. Nhân viên duyệt ảnh, xác nhận, sửa số đã xác nhận, xử lý vòng đời hóa đơn và xem kết quả công bố/email từng dòng. Mọi nút chỉ là gợi ý giao diện; Application vẫn quyết định quyền.
10. Phân công chi nhánh và khóa màn quản trị đạt §12.7.

## 9. Giới hạn

OCR có thể sai; quyết định chỉ số chính thức thuộc nhân viên đúng chi nhánh hoặc Admin. Schema hiện tại không lưu số khách đề xuất tách biệt, nguồn/người xác nhận từng loại khi nhập tay hoặc lịch sử mọi lần sửa chỉ số. Sau khi tải lại trang không còn bằng chứng ứng dụng về kết quả email hóa đơn; gửi lại thủ công có thể tạo email trùng. Hai job nhắc nợ/cảnh báo hợp đồng còn phụ thuộc file JSON cũ cho tới đợt xử lý riêng. Quan hệ hóa đơn thay thế chỉ suy từ hợp đồng/kỳ/mã, không phải FK. Phân công chi nhánh chỉ giữ lần phân công và thu hồi gần nhất của mỗi cặp nhân viên–chi nhánh. Đổi role khỏi `NhanVien` không tự thu hồi phân công; nếu đổi lại thành `NhanVien`, phân công cũ còn hiệu lực có tác dụng trở lại. Kiểm tra lại phiên tốn thêm một truy vấn theo khóa chính cho mỗi request đã đăng nhập. Các màn Phòng trọ, Người thuê, Hợp đồng, Dịch vụ chưa lọc theo chi nhánh.

## 10. Phát triển sau: quyền riêng từng nhân viên (phương án 2)

Khi cần nhân viên có quyền A nhưng không có quyền B, giữ `Role.NhanVien` và các use case hiện tại; thay chính sách kiểm tra trong Application bằng grant theo từng phân công chi nhánh. Đây là giai đoạn sau, không thuộc migration, UI hoặc điều kiện nghiệm thu hiện tại.

Bảng dự kiến `nhan_vien_quyen_chi_nhanh` có `NhanVienQuyenChiNhanhId` (PK), `NhanVienChiNhanhId` (FK), `PermissionCode` varchar(64), `NgayCap` UTC, `NguoiCapId` (FK `NguoiDung`; Application yêu cầu Admin), `NgayThuHoi` nullable, `NguoiThuHoiId` nullable và `LyDoThuHoi` nullable. Mỗi lần cấp là một hàng; unique partial index trên (`NhanVienChiNhanhId`, `PermissionCode`) khi `NgayThuHoi IS NULL` giữ một grant hiệu lực và cho phép lưu lịch sử thu hồi/cấp lại. Mã dự kiến: `Meter.Upload`, `Meter.Review`, `Invoice.Draft`, `Invoice.Submit`, `Invoice.Send`, `Invoice.Cancel`; quyền chốt/trả lại vẫn chỉ Admin.

Lúc nâng cấp cần migration mới, màn Admin cấp/thu hồi, kiểm tra grant trong Application, test theo chức năng/chi nhánh và kế hoạch cấp quyền cho nhân viên hiện hữu trước khi bật chặn quyền mới. Không thể suy ra bộ quyền mong muốn của từng nhân viên từ role `NhanVien` hiện tại; Admin phải xác nhận phân công. Không sửa ngược lịch sử hóa đơn/ảnh để chuyển mô hình phân quyền.

## 11. Giao diện MVC (đợt 3)

Chỉ làm MVC, không có API v1 (§7). Mọi POST có antiforgery (header `RequestVerificationToken` cho AJAX). Web không reference Domain và map DTO của Application sang ViewModel. Nút ẩn/hiện theo role và trạng thái chỉ để hỗ trợ người dùng; Application luôn kiểm tra lại. Giao diện dùng layout, Bootstrap, DataTables và SweetAlert hiện có của từng area, và hiển thị tốt trên màn hình rộng từ 360px.

### 11.1 Quy tắc tải ảnh chung

- Web nhận `IFormFile` rồi chuyển sang `UploadFile`. Định dạng và dung lượng theo `MeterImageOptions`: JPEG, PNG, WebP, tối đa 5 MB.
- Web kiểm tra thêm **nội dung tệp** bằng chữ ký đầu tệp (magic bytes) khớp với định dạng khai báo trước khi gọi Application.
- Ô chọn tệp dùng `accept="image/jpeg,image/png,image/webp"` và `capture="environment"`, để điện thoại mở thẳng camera sau.
- Sau khi tải, hiển thị trạng thái nhận diện trả về ngay trong phản hồi. Không cần tải lại cả trang.

### 11.2 Cổng khách thuê (`Areas/KhachThue`)

**K1 — Trang "Chỉ số điện nước" (mới, thêm vào sidebar khách thuê).**
- Chọn kỳ tháng/năm, mặc định là tháng hiện tại. Nếu khách có nhiều hợp đồng còn hiệu lực trong kỳ thì chọn thêm phòng. Khách xem được ảnh các kỳ cũ nhưng chỉ tải ảnh cho tháng hiện tại và tháng trước (chốt 29/09/2026, §3.8).
- Có hai thẻ Điện và Nước. Mỗi thẻ liệt kê các ảnh đã gửi của kỳ, mới nhất trước, gồm ảnh thu nhỏ (bấm để phóng to), ngày gửi và trạng thái:

  | Trạng thái | Hiển thị cho khách |
  |---|---|
  | `MoiTaiLen`, `DangXuLy` | "Đang nhận diện" |
  | `DocDuoc` | "AI gợi ý: {số} (chưa phải số chính thức)" |
  | `KhongDocDuoc`, `Loi`, `CanChupLai` | "Không đọc được, vui lòng chụp lại" |
  | `DaXacNhan` | "Đã xác nhận: {GiaTriXacNhan}", thêm nhãn "Số chính thức" nếu `DuocChonLamChiSoChinhThuc` |
  | `DaThayThe` | "Đã được thay bằng ảnh khác" |
- Nút "Tải ảnh mới" cho từng loại. Nút bị khóa, kèm lý do, khi kỳ sau đã chốt/đã có hóa đơn phát hành hoặc hóa đơn của kỳ đã qua `Nhap`.
- **Không** có ô nhập số (§3.1).
- Khách chỉ thấy và tải ảnh cho phòng thuộc hợp đồng của mình. Application kiểm tra bằng hợp đồng còn hiệu lực trong kỳ; không tin `PhongTroId` do client gửi lên.

**K2 — Trang "Hóa đơn" (sửa trang hiện có).**
- Danh sách chỉ gồm hóa đơn `DaGui` và bản `DaHuy` đã từng công bố (đã làm ở đợt 1). Bản hủy có nhãn "Đã hủy" kèm lý do hủy lấy từ lịch sử.
- Trang chi tiết có tải PDF. PDF bản hủy có dấu "ĐÃ HỦY" rõ ràng.
- VietQR và các nút thanh toán chỉ hiện với hóa đơn `DaGui` chưa thanh toán đủ. Luồng thanh toán thuộc module 2.
- Bấm vào thông báo hóa đơn trên cổng sẽ mở đúng trang chi tiết.

### 11.3 Cổng quản lý (`Areas/QuanLyNhaTro`)

**Q1 — Ảnh chỉ số, gắn vào màn "Chốt điện nước" hiện có.**
- Mỗi dòng phòng có thêm cột "Ảnh" cho điện và nước, gồm số ảnh của kỳ và nhãn trạng thái của ảnh chính thức hoặc ảnh mới nhất.
- Bấm vào cột "Ảnh" mở modal của phòng/kỳ. Modal có:
  - ảnh lớn (phóng to/xoay được) và danh sách các ảnh của kỳ;
  - số AI gợi ý cùng độ tin cậy, tách riêng với số chính thức;
  - thao tác theo trạng thái ảnh:
    - "Thử nhận diện lại" cho ảnh `KhongDocDuoc`/`Loi` hoặc ảnh đang xử lý đã quá hạn;
    - "Xác nhận" với ô số điền sẵn số AI gợi ý và cho sửa (bắt buộc lý do nếu ảnh không đọc được);
    - "Sửa số đã xác nhận" (bắt buộc lý do, §3.7) cho ảnh chính thức;
    - "Tải ảnh thay khách".
- Sau mỗi thao tác, cập nhật lại dòng phòng và cột số mới trên bảng chốt. Luồng nhập tay kèm lý do đã có giữ nguyên.

**Q2 — Màn "Hóa đơn" (sửa trang hiện có).**
- Thêm bộ lọc và cột **trạng thái phát hành** (Nháp / Chờ duyệt / Đã chốt / Đã gửi / Đã hủy), tách riêng với trạng thái thanh toán.
- Nút theo trạng thái:

  | Trạng thái | Nút |
  |---|---|
  | `Nhap` | Sửa, Gửi duyệt |
  | `ChoDuyet` | Chốt, Trả lại (có lý do); chỉ Admin |
  | `DaChot` | Công bố, Hủy |
  | `DaGui` | Gửi lại email (hộp xác nhận cảnh báo có thể gửi trùng), Hủy |
  | `DaHuy` | Chỉ xem |
- Chọn nhiều hóa đơn `DaChot` rồi bấm "Công bố các hóa đơn đã chọn" (tối đa 50). Modal kết quả hiện từng dòng: mã hóa đơn, kết quả công bố ("Đã công bố", "Đã công bố trước đó", hoặc lỗi) và kết quả email ("Đã gửi", "Lỗi gửi", "Không có email", "Khách chưa có tài khoản cổng").
  Kết quả này chỉ tồn tại trong lần thao tác đó. Tải lại trang chỉ còn trạng thái phát hành, không hiện trạng thái email (§5.2).
- Modal chi tiết hóa đơn có tab "Lịch sử trạng thái" lấy từ `LichSuTrangThaiHoaDon`.
- Preview và tạo nháp hàng loạt giữ như hiện nay, cộng thêm ghi chú từng phòng từ đợt 1.

**Q3 — Màn "Yêu cầu sự cố".** Nếu sửa sự cố bị từ chối vì hóa đơn tháng đó đã qua `Nhap` (§4.6), hiển thị đúng thông báo trả về.

### 11.4 Kiểm thử giao diện

Web integration test cho từng route mới:
- chặn khi không đăng nhập, sai role hoặc sai chi nhánh;
- chặn khi thiếu antiforgery;
- ảnh sai định dạng, quá dung lượng, hoặc nội dung không khớp chữ ký;
- khách truy cập ảnh hay hóa đơn của phòng khác bằng cách đoán id;
- khách không thấy nháp;
- modal kết quả công bố nhận đúng kết quả từng dòng khi dùng SMTP giả.

Người dùng thử tay trên DB dev bằng trình duyệt máy tính và điện thoại, hoặc chế độ giả lập thiết bị di động.

## 12. Phân công nhân viên theo chi nhánh và khóa màn quản trị (đầu đợt 2, chốt 27/09/2026)

Đây là nền dùng chung cho module 1–2: mọi kiểm tra quyền nhân viên ở §2 dựa vào phân công chi nhánh, nhưng hiện chưa có cách tạo phân công ngoài SQL. Phần này **không đổi schema**, chỉ dùng bảng `nhan_vien_chi_nhanh` hiện có. Nó không phải "cấp quyền chức năng" của §10: mọi nhân viên được phân công vẫn có cùng quyền.

### 12.1 Hiện trạng đã kiểm tra trong code

- Bảng `nhan_vien_chi_nhanh` có `NguoiDungId`, `ChiNhanhId`, `IsActive`, `NgayPhanCong`, `NguoiPhanCongId`, `NgayThuHoi`, `NguoiThuHoiId`, `LyDo` (tối đa 500 ký tự), và unique index (`NguoiDungId`, `ChiNhanhId`). Mỗi cặp nhân viên–chi nhánh chỉ có **một dòng**.
- Application đã **đọc** phân công (`EmployeeAccessService`, `EmployeeBranchStore`, mỗi lần kiểm tra đều đọc DB). Chưa có use case **ghi** phân công.
- `NguoiDungController` và `ChiNhanhsController` kế thừa `AdminBaseController` (`[Authorize(Roles = "Admin,NhanVien")]`). Vì vậy nhân viên gọi được API tạo, sửa, xóa tài khoản (kể cả đổi role của chính mình thành Admin) và API thêm, sửa, xóa chi nhánh. Menu không lọc theo role.
- Cookie đăng nhập giữ claim role tới 30 ngày và không kiểm tra lại DB. Admin khóa tài khoản hoặc đổi role thì phiên cũ vẫn giữ quyền cũ ở các attribute `[Authorize(Roles = ...)]`.
- Chỉ các màn Điện nước, Hóa đơn, Sự cố lọc dữ liệu theo phân công. Các màn Phòng trọ, Người thuê, Hợp đồng, Dịch vụ vẫn hiện mọi chi nhánh; **giữ nguyên trong đợt này** (§12.8).

### 12.2 Quy tắc phân công

1. **Chỉ Admin** được xem màn và phân công/thu hồi. Application kiểm tra actor là Admin **đang hoạt động trong DB** (qua `IEmployeeBranchStore.GetActorWithActiveBranchesAsync`), không dựa vào claim của cookie.
2. **Đối tượng được phân công:** tài khoản role `NhanVien`, chưa xóa mềm. Admin không cần phân công vì đã có mọi chi nhánh. Tài khoản `KhachThue`/`KhachVangLai`, hoặc tài khoản đã xóa, bị từ chối. Tài khoản `NhanVien` đang khóa (`IsActive = false`) vẫn phân công được, nhưng giao diện ghi rõ "Tài khoản đang khóa, phân công chưa có hiệu lực".
3. **Chi nhánh:** phải tồn tại và chưa xóa mềm mới được phân công. Phân công sẵn có tại chi nhánh đã xóa vẫn hiển thị và vẫn thu hồi được.
4. **Phân công** (giao diện chỉ hỏi xác nhận, không hỏi lý do — người dùng chốt 28/09/2026; Application vẫn nhận lý do tùy chọn: cắt khoảng trắng, tối đa 500 ký tự, chuỗi rỗng coi như không có):
   - Chưa có dòng: thêm dòng `IsActive = true`, `NgayPhanCong = UtcNow`, `NguoiPhanCongId = actor`, `LyDo = lý do`.
   - Có dòng đã thu hồi: **dùng lại dòng đó**: `IsActive = true`, `NgayPhanCong = UtcNow`, `NguoiPhanCongId = actor`, `NgayThuHoi = null`, `NguoiThuHoiId = null`, `LyDo = lý do mới`. Thông tin lần thu hồi trước bị ghi đè (người dùng chấp nhận ngày 27/09/2026; roadmap ghi ngày/người chỉ phục vụ truy vết).
   - Đang hiệu lực: trả thành công với thông báo "Nhân viên đã phụ trách chi nhánh này", không đổi dữ liệu.
5. **Thu hồi** (lý do **bắt buộc**, 1–500 ký tự sau khi cắt khoảng trắng):
   - Dòng đang hiệu lực: `IsActive = false`, `NgayThuHoi = UtcNow`, `NguoiThuHoiId = actor`, `LyDo = lý do`; giữ `NgayPhanCong`/`NguoiPhanCongId`.
   - Chưa có dòng hoặc đã thu hồi: trả thành công với thông báo "Nhân viên không còn phụ trách chi nhánh này", không đổi dữ liệu.
   - Có hiệu lực ngay ở request kế tiếp của nhân viên, vì kiểm tra quyền đọc DB mỗi lần. Thao tác đã qua bước kiểm tra quyền trong transaction đang chạy vẫn hoàn tất.
   - Không chặn thu hồi khi còn việc dở (ảnh chưa duyệt, hóa đơn nháp); dữ liệu thuộc chi nhánh nên nhân viên khác hoặc Admin làm tiếp. Được thu hồi cả phân công cuối cùng; khi đó nhân viên không còn thấy dữ liệu module 1–2.
6. **Đồng thời:** trong một transaction, khóa hàng `nguoi_dung` của nhân viên đích bằng `FromSqlInterpolated ... FOR UPDATE`, rồi mới đọc dòng phân công của cặp. Mọi thao tác trên cùng nhân viên vì thế chạy tuần tự; hai lệnh phân công cùng lúc chỉ tạo một dòng. Unique index là lớp chặn cuối; nếu vẫn vi phạm thì trả thông báo cố định, không lộ lỗi DB.
7. Không xóa cứng dòng phân công. Invariant đặt trong Domain: thêm method `NhanVienChiNhanh.ThuHoi(actorId, nowUtc, lyDo)` (chặn khi đã thu hồi hoặc thiếu lý do) và `PhanCongLai(actorId, nowUtc, lyDo)` (chặn khi đang hiệu lực). Không đổi cột hay mapping.
8. **Không sửa `NguoiDungService`.** Khi Admin đổi role khỏi `NhanVien` hoặc xóa tài khoản, các dòng phân công giữ nguyên nhưng không còn hiệu lực, vì kiểm tra quyền yêu cầu role `NhanVien` và tài khoản hoạt động. Nếu sau đó đổi lại thành `NhanVien`, các dòng còn `IsActive = true` có hiệu lực trở lại; Admin rà lại trên màn phân công (§9).

### 12.3 Khóa màn quản trị chỉ cho Admin

- `NguoiDungController`: mọi action trừ `DangNhap` và `DangXuat` thêm `[Authorize(Roles = "Admin")]`. `ChiNhanhsController`: khóa toàn bộ (đã kiểm tra: API của controller này chỉ được màn "Chi nhánh" gọi). Nhân viên gọi các route này bị chặn: vì `AccessDeniedPath` trùng trang đăng nhập nên nhận 302 về `/QuanLyNhaTro/DangNhap` (chốt Q3 ngày 28/09/2026), không phải 403.
- Menu: `MenuItem` thêm danh sách role được xem. Ẩn nhóm "Quản lý người dùng" (Quản lý tài khoản, Phân công chi nhánh) và mục "Chi nhánh" với `NhanVien`. Ẩn menu chỉ hỗ trợ người dùng; attribute và Application mới là chốt chặn.
- **Kiểm tra lại phiên theo DB:** thêm `CookieAuthenticationEvents.OnValidatePrincipal` trong `Program.cs`. Mỗi request đã đăng nhập gọi một use case Application mới (ví dụ `IUserSessionService.IsSessionValidAsync(nguoiDungId, AppRole role)`) để đọc tài khoản theo khóa chính: tồn tại, chưa xóa, `IsActive` và role trùng claim. Không khớp thì `RejectPrincipal()` rồi đăng xuất, và request đi về trang đăng nhập như chưa đăng nhập. Kết quả: khóa tài khoản hoặc đổi role có hiệu lực từ request kế tiếp, cho mọi role. Đây là thay đổi dùng chung (`Program.cs` do A sở hữu). Phải báo người B vì phiên đăng nhập của khách vãng lai sau này cũng đi qua kiểm tra này.

### 12.4 Hợp đồng Application

Đặt trong `Features/NhanViens`. Web chỉ dùng DTO và enum của Application, không tham chiếu Domain.

```csharp
public interface IEmployeeBranchAssignmentService
{
    Task<ServiceResult<IReadOnlyList<EmployeeBranchSummaryDto>>> GetEmployeesAsync(int? chiNhanhId, int actorId, CancellationToken ct = default);
    Task<ServiceResult<EmployeeBranchDetailDto>> GetEmployeeAsync(int nguoiDungId, int actorId, CancellationToken ct = default);
    Task<ServiceResult<IReadOnlyList<BranchOptionDto>>> GetBranchOptionsAsync(int actorId, CancellationToken ct = default);
    Task<ServiceResult> AssignAsync(AssignEmployeeBranchRequest request, int actorId, CancellationToken ct = default);
    Task<ServiceResult> RevokeAsync(RevokeEmployeeBranchRequest request, int actorId, CancellationToken ct = default);
}
```

- `EmployeeBranchSummaryDto`: `NguoiDungId`, `TenDangNhap`, `IsActive`, danh sách chi nhánh **đang phụ trách** (`ChiNhanhId`, `MaChiNhanh`, `TenChiNhanh`) và `LanThayDoiGanNhatUtc` (lớn nhất giữa `NgayPhanCong` và `NgayThuHoi` của các dòng). Lọc `chiNhanhId` trả nhân viên đang phụ trách chi nhánh đó.
- `EmployeeBranchDetailDto`: thông tin tài khoản và một dòng cho **mỗi chi nhánh chưa xóa**, cộng các chi nhánh đã xóa nhưng còn dòng phân công. Mỗi dòng có `ChiNhanhId`, `MaChiNhanh`, `TenChiNhanh`, `ChiNhanhDaXoa`, `TrangThai` (enum Application `EmployeeBranchState { ChuaPhanCong, DangPhuTrach, DaThuHoi }`), `NgayPhanCongUtc`, `NguoiPhanCong` (tên đăng nhập), `NgayThuHoiUtc`, `NguoiThuHoi`, `LyDo`.
- `AssignEmployeeBranchRequest { NguoiDungId, ChiNhanhId, LyDo? }`, `RevokeEmployeeBranchRequest { NguoiDungId, ChiNhanhId, LyDo }`.
- Store: mở rộng `IEmployeeBranchStore` (hoặc thêm `IEmployeeBranchAssignmentStore` trong cùng feature) với `LockEmployeeAsync` (`FOR UPDATE`, trả role/`IsActive`/`IsDeleted`), `IsBranchAssignableAsync`, `GetAssignmentAsync` (tracked), `AddAssignmentAsync` và hai truy vấn chiếu (projection) cho danh sách và chi tiết. Store không trả `IQueryable`. Không dùng `Update()` trên entity đang được theo dõi.
- Lỗi trả `ServiceResult.Fail` với thông báo cố định; không trả `ex.Message`. Mọi lệnh ghi dùng `IUnitOfWork` transaction.

### 12.5 Giao diện (`Areas/QuanLyNhaTro`)

**Trang "Phân công chi nhánh"**, route `/QuanLyNhaTro/PhanCongChiNhanh`, controller mới kế thừa `AdminBaseController` và gắn `[Authorize(Roles = "Admin")]`. Mục menu nằm trong "Quản lý người dùng".
- Đầu trang: dropdown lọc chi nhánh (mặc định "Tất cả") và ô tìm kiếm của DataTables.
- Bảng nhân viên gồm các cột:
  - Tên đăng nhập;
  - Trạng thái tài khoản ("Hoạt động" hoặc "Đang khóa");
  - Chi nhánh đang phụ trách: badge mã chi nhánh, rê chuột để xem tên. Nếu không có thì hiện "Chưa phân công" màu cam;
  - Thay đổi gần nhất (giờ Việt Nam);
  - Nút "Phân công".
- Modal "Phân công chi nhánh — {Tên đăng nhập}":
  - Cảnh báo nếu tài khoản đang khóa.
  - Một dòng cho mỗi chi nhánh, gồm các cột: Chi nhánh | Trạng thái (Đang phụ trách / Đã thu hồi / Chưa phân công) | Phân công lúc, bởi ai | Thu hồi lúc, bởi ai | Lý do.
  - Thao tác trên từng dòng:
    - "Phân công": hộp xác nhận, không có ô lý do;
    - "Thu hồi": SweetAlert bắt nhập lý do.
  - Chi nhánh đã xóa hiện màu xám, không có nút "Phân công".
- Sau mỗi thao tác, tải lại modal và dòng tương ứng trong bảng, rồi hiện thông báo từ `ServiceResult`. Mọi POST gửi antiforgery qua header `RequestVerificationToken`. Thời gian lưu UTC, hiển thị giờ Việt Nam.

**Màn "Quản lý tài khoản":** dòng có role `NhanVien` thêm nút "Chi nhánh", mở trang phân công đã lọc sẵn đúng nhân viên (`?nguoiDungId=`).

**Nhân viên chưa được phân công:** trên các màn Điện nước, Hóa đơn, Sự cố, nếu `IEmployeeAccessService.GetScopeAsync` trả phạm vi rỗng thì hiện banner "Bạn chưa được phân công chi nhánh nào. Vui lòng liên hệ Admin." thay vì chỉ hiện bảng trống.

### 12.6 Kiểm thử

- **Domain:** `ThuHoi` chặn khi đã thu hồi hoặc thiếu lý do. `PhanCongLai` chặn khi đang hiệu lực, xóa `NgayThuHoi`/`NguoiThuHoiId` và ghi người/ngày mới.
- **Application (fake store):**
  - Actor không phải Admin đang hoạt động bị từ chối: nhân viên (kể cả đang được phân công), khách thuê, Admin đã khóa.
  - Đích không phải `NhanVien` hoặc đã xóa, chi nhánh không tồn tại hoặc đã xóa: bị từ chối.
  - Phân công mới; phân công lại dùng lại dòng cũ và xóa thông tin thu hồi; phân công lặp không đổi dữ liệu.
  - Thu hồi thiếu lý do hoặc lý do quá 500 ký tự bị từ chối; lý do được cắt khoảng trắng; thu hồi lặp không đổi dữ liệu.
  - Sau khi thu hồi, `CanPerformAsync` của nhân viên tại chi nhánh đó trả `false`.
- **Infrastructure (PostgreSQL test):**
  - Hai lệnh phân công cùng cặp chạy đồng thời chỉ tạo một dòng và không ném lỗi. Dùng kết nối giữ khóa, dò `pg_locks` để thấy request thứ hai đang chờ, không dùng `Task.Delay`.
  - Phân công lại sau thu hồi vẫn chỉ có một dòng.
  - Lọc danh sách theo chi nhánh đúng.
  - Truy vấn khóa là `FOR UPDATE`.
- **Web:**
  - Nhân viên bị chặn (302 về `DangNhap`) ở mọi route còn lại của `NguoiDungController` (`GetAllApi`, `GetByIdApi`, `CreateApi`, `EditApi`, `DeleteApi`, `ResetPasswordToPhoneApi`, trang quản lý), ở mọi route `ChiNhanhs` và ở route phân công. Admin gọi được.
  - Thiếu antiforgery bị chặn.
  - `DangNhap`/`DangXuat` vẫn truy cập ẩn danh.
  - HTML menu của nhân viên không có link quản trị.
  - Kiểm tra lại phiên: sau khi khóa tài khoản hoặc đổi role trong DB, request kế tiếp bằng cookie cũ không còn được coi là đã đăng nhập.
- **Thử tay trên DB dev:** Admin phân công một nhân viên vào chi nhánh 1, thu hồi, phân công lại. Nhân viên đó đăng nhập: không thấy menu quản trị; gõ URL quản lý tài khoản bị chặn; chỉ thấy dữ liệu Điện nước/Hóa đơn của chi nhánh đang phụ trách. Admin khóa tài khoản nhân viên khi họ đang đăng nhập: request kế tiếp bị đưa về trang đăng nhập.

### 12.7 Nghiệm thu

Admin phân công, thu hồi và phân công lại từ giao diện, không cần SQL, và mỗi cặp nhân viên–chi nhánh chỉ có một dòng. Thu hồi có hiệu lực ngay. Nhân viên không vào được màn tài khoản, chi nhánh và phân công, kể cả khi gọi thẳng API. Khóa tài khoản hoặc đổi role làm mất phiên cũ ở request kế tiếp. Không có migration; EF không báo thay đổi model.

### 12.8 Ngoài phạm vi

- Lọc theo chi nhánh cho các màn cũ (Phòng trọ, Người thuê, Hợp đồng, Dịch vụ): các màn này chạm entity dùng chung `PhongTro`/`HopDong`, nên cần thỏa thuận với người B. Để ở đợt sau.
- Quyền chức năng riêng từng nhân viên: §10.
- Lịch sử đầy đủ mọi lần phân công/thu hồi: cần migration bỏ unique và thêm partial index.
- Màn nhân viên tự đổi mật khẩu.

## 13. Chia đợt triển khai (chốt 27/09/2026)

| Đợt | Nội dung | Trạng thái |
|---|---|---|
| 1 | §4 và §4.6: nháp → chờ duyệt → chốt/trả lại, hủy và bản thay thế, sự cố theo tháng `NgayXuLy`, lọc cổng khách, `ThuTien`, truy vấn nhắc nợ | Xong, commit `577ae59` |
| 2A | §12: phân công chi nhánh, khóa màn quản trị, kiểm tra lại phiên. Kèm hai P3 của đợt 1: mốc tháng dùng khoảng nửa mở `[đầu tháng, đầu tháng sau)` ở cả ba chỗ tính tháng; `GetContractIdsForTenantRoomPeriodsAsync` phải dùng `periods` hoặc đổi tên cho đúng hành vi | Xong, commit `b1cf7f2` |
| 2B | §5 và §6: công bố `DaChot → DaGui` kèm `ThongBao` trong transaction, email sau commit, gửi hàng loạt ≤ 50, "Gửi lại email", `HanThanhToan`, người nhận, thông báo real-time; siết `KiemTraDuDieuKienYeuCauThanhToan` chỉ nhận `DaGui`. Trên màn hóa đơn hiện có chỉ thêm nút "Công bố" và "Gửi lại email" cho từng hóa đơn để thử tay, gỡ modal "Gửi email hàng loạt" cũ (§6). Quyết định bổ sung 28/09/2026: §5.7 hạn cuối ngày giờ VN, §5.8 link thông báo, §5.9 hạn trong email. Kế hoạch: `docs/KeHoachHienTai/2026-09-28-ke-hoach-dot-2b-cong-bo-hoa-don.md` | Xong, commit `07f37a3` (29/09/2026). Báo cáo: `docs/KeHoachHienTai/2026-09-29-bao-cao-dot-2b-cong-bo-hoa-don.md` |
| 3A | §11.1, K1, Q1 và §3.8: giao diện ảnh chỉ số ở cổng khách và màn "Chốt điện nước", kèm quy tắc tải ảnh và duyệt lại khi xác nhận. Kế hoạch: `docs/KeHoachHienTai/2026-09-29-ke-hoach-dot-3a-anh-chi-so-giao-dien.md` | Xong, commit `8f1a2c0` (30/09/2026) |
| 3B | K2 (gồm PDF bản hủy có dấu "ĐÃ HỦY"), Q2 (chọn nhiều và modal kết quả), Q3. Chỉ làm MVC. Quyết định 29/09/2026: giữ modal chi tiết của khách và tự mở theo `?hoaDonId=`; màn quản lý chỉ hiện bản hủy khi lọc "Đã hủy"; chỉ người đứng tên hợp đồng xem hóa đơn; chọn nhiều giữ qua các trang, tối đa 50. Kế hoạch: `docs/KeHoachHienTai/2026-09-29-ke-hoach-dot-3b-giao-dien-hoa-don.md`; chỉ bắt đầu sau khi 3A đã commit | Xong, commit `9e627c0` (30/09/2026). Sửa sau thử tay: `74db25a`, `937ad78`. |

Hai lựa chọn giao diện đã chốt ngày 27/09/2026: duyệt ảnh ngay trong màn "Chốt điện nước" (Q1); khách thấy số AI gợi ý kèm nhãn "chưa phải số chính thức" (K1).
