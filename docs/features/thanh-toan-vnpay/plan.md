# Kế hoạch: Thanh toán hóa đơn qua VNPay (đợt 2)

- **Nhánh:** `feature/thanh-toan-truc-tuyen` (Q9). **Spec:** [spec.md](spec.md), đã duyệt 25/10/2026.
- **Trạng thái:** Phần 1 xong 26/10/2026 (reviewer CHOT; toàn solution PASS: Domain 135, Application 1957, Infrastructure 619, Web 602; `has-pending-model-changes` PASS). Phần 2 chưa bắt đầu. Hai lượt `/ship` (Q13); mỗi lượt planner sinh kế hoạch chi tiết ở `.bangiao/ke-hoach.md` (không lưu Git, lượt sau ghi đè lượt trước). File này giữ cách chia phần, các quyết định thiết kế và điều kiện xong của từng phần.

## 1. Nguyên tắc chung

- Không đổi schema; kiểm `has-pending-model-changes`. Không đổi enum `PhuongThucThanhToan` (Q7).
- Không sửa `Program.cs`, `.csproj`, `appsettings.json`, fixture test, test kiến trúc. Chỉ thêm khối `VnPay` vào `appsettings.example.json`.
- Không tạo script phụ trợ; thử IPN bằng test xUnit tự ký tham số.
- Không tự thiết lập môi trường (Q10): code xong liệt kê thứ người dùng cần cung cấp.
- Test SePay hiện có phải PASS mà không sửa kỳ vọng. Có test nào phải sửa thì ghi lý do vào `.bangiao/thay-doi.md` và báo người dùng.
- Mỗi lượt `dotnet test` chạy một mình; integration test chỉ trên DB `_test`.
- Chỉ commit khi người dùng bảo, không push, commit không có dòng `Co-Authored-By`.

## 2. Quyết định thiết kế cho cả hai phần

### 2.1 Dùng chung phần ghi sổ với SePay

Cách ít xáo trộn nhất là để VNPay đi qua đúng `InvoiceTransferHandler` và `IPaymentOutcomePublisher` hiện có:

- Thêm enum `OnlinePaymentChannel { SePay, VnPay }` và thuộc tính `Kenh` (mặc định `SePay`) vào `IncomingBankTransfer`. Mọi chỗ tạo `IncomingBankTransfer` hiện có (webhook, đồng bộ bù, test) không phải đổi.
- `DedupKey`: `Kenh == VnPay` thì `VNPAY-{ReferenceCode}` (`ReferenceCode` = `vnp_TransactionNo`), còn lại giữ nguyên.
- VNPay dựng `IncomingBankTransfer` từ IPN: `ReferenceCode = vnp_TransactionNo`, `Gateway = vnp_BankCode`, `TransferAmount = vnp_Amount / 100`, `TransactionDateUtc` theo `vnp_PayDate` (spec §5.4), `Content = vnp_OrderInfo`, `Description = vnp_TxnRef`, `TransferType = "in"`, `SePayId = 0`, `AccountNumber = null`. Thêm `VnPayCardType` nếu cần cho ghi chú.
- `IVnPayPaymentService.HandleIpnAsync` gọi thẳng `InvoiceTransferHandler.HandleAsync(transfer, InvoiceTransferHandler.TransferCode(hoaDonId))` rồi `IPaymentOutcomePublisher.PublishAsync`, không qua `IncomingTransferClassifier` (bộ phân loại kiểm số tài khoản và nội dung chuyển khoản, không áp dụng cho VNPay). Đăng ký DI để lấy được `InvoiceTransferHandler` cụ thể (hiện chỉ đăng ký dưới `IIncomingTransferHandler`).
- Chữ phụ thuộc kênh đổi theo `Kenh`: `GhiChu` (`[VNPAY] ...`), lý do lịch sử hóa đơn, tiêu đề và nội dung thông báo trong app, tin Telegram ghi sổ, cảnh báo Telegram (tiêu đề "THANH TOÁN VNPAY CẦN HOÀN TIỀN", gợi ý hoàn tiền trên trang merchant), log.

Planner được đổi cách này nếu thấy rủi ro hơn (ví dụ tách bộ ghi sổ trung tính khỏi `IncomingBankTransfer`), nhưng phải nêu lý do trong `.bangiao/ke-hoach.md` và hỏi người dùng trước khi code.

### 2.2 Nhận diện dòng sổ theo tiền tố

- `InvoicePaymentPolicy`: thêm `VnPayMaGiaoDichPrefix = "VNPAY-"`, `VnPayLedgerKey(txnNo)`, `IsVnPayKey`, và hàm lấy mã tham chiếu của dòng VNPay. Dòng "hệ thống tự ghi" là dòng `SEPAY-` hoặc `VNPAY-`.
- `PaymentNotificationFormatter.PaymentMethodLabel`: `VNPAY-` → "Thẻ ATM / Visa / MasterCard (VNPay)". Panel khách, biên lai web, email biên lai đều đi qua hàm này.
- `InvoiceReceiptService`, `InvoiceDetailProjection`: mã tham chiếu của dòng VNPay; DTO lịch sử thanh toán thêm `LaVnPay`.

### 2.3 Bộ ký VNPay

- Lớp tĩnh thuần trong Application (`System.Security.Cryptography` thuộc BCL nên không vi phạm ranh giới), theo spec §5.3: lọc `vnp_*` khác rỗng, bỏ `vnp_SecureHash`/`vnp_SecureHashType`, sắp `StringComparer.Ordinal`, URL-encode giá trị theo `WebUtility.UrlEncode`, HMAC-SHA512 ra hex; so bằng `CryptographicOperations.FixedTimeEquals` sau khi chuẩn hóa hoa thường.
- Cách encode chỉ xác nhận được khi người dùng thử trên sandbox (V07, sau phần 2). Gom vào một hàm để sửa một chỗ nếu sai.

## 3. Phần 1 — Backend: cấu hình, bộ ký, IPN, ghi sổ, thông báo

Spec: §3, §5.2–§5.4, §6, §6.1, §7, §9.1–§9.2, §10, IPN ở §9.3.

| Việc | Ghi chú |
| --- | --- |
| `Common/Configurations/VnPayOptions.cs` + `Validate()` (spec §10); bind và validate trong `AddInfrastructure` như `SePayOptions`; khối `VnPay` trong `appsettings.example.json` | `ExpireMinutes` mặc định 15 (Q12), hợp lệ 5–60 |
| Bộ ký, tách `vnp_TxnRef` (`HD{1–9 chữ số}_{số}`), đổi giờ `yyyyMMddHHmmss` GMT+7 ↔ UTC | thuần, unit test đủ ca ở spec §11.1 |
| `OnlinePaymentChannel`, `IncomingBankTransfer.Kenh`, `DedupKey` theo kênh | §2.1 |
| `InvoiceTransferHandler`: ghi chú, lý do lịch sử, thông báo trong app theo kênh | hành vi SePay giữ nguyên |
| `PaymentNotificationFormatter`: tin ghi sổ, cảnh báo hoàn tiền, nhãn phương thức theo kênh | spec §7 |
| `IVnPayPaymentService.HandleIpnAsync` theo bảng spec §5.2 | trả `RspCode` + `Message`; không log tham số |
| `Web/Controllers/Api/VnPayIpnController` `GET /api/webhooks/payment/vnpay-ipn` | `[AllowAnonymous]`, `[IgnoreAntiforgeryToken]`; tắt thì 404; luôn HTTP 200 với JSON khi bật |
| Chặn ghi trùng `VNPAY-` (spec §6.1, Q11): `InvoiceLedgerService` (thu thủ công), `InvoicePaymentConfirmationService` (xác nhận minh chứng cũ), `FindSystemSePayEntryRecordedAfterAsync` tính cả `VNPAY-` (đổi tên cho đúng nghĩa nếu cần) | thông báo lỗi riêng cho VNPay |
| `InvoicePaymentPolicy`, `InvoiceReceiptService`, `InvoiceDetailProjection`, DTO `LaVnPay` | §2.2 |
| Test: Application unit (bộ ký, TxnRef, giờ, IPN, formatter, chặn trùng), Infrastructure (tìm dòng `VNPAY-` kể cả đã xóa mềm, ghi song song cùng `vnp_TransactionNo`), Web (IPN tắt 404, sai chữ ký `97`, hợp lệ ghi sổ + SignalR, gọi lại `02`, lỗi hệ thống `99`) | spec §11.1, §11.2 |

**Xong khi:** build 0 lỗi; toàn solution PASS một lượt; `has-pending-model-changes` PASS; reviewer CHOT. Chưa có giao diện nên chưa thử tay được trên sandbox.

**Kết quả (26/10/2026):** đạt cả bốn điều kiện. Quyết định khi lập kế hoạch chi tiết (người dùng duyệt): B1 `vnp_TransactionNo` rỗng hoặc khóa dài quá 100 ký tự → không ghi, `99`; B2 `vnp_Amount` thiếu/không phải số/tràn → `04`; B3 tiêu đề thông báo và Telegram dùng số khách trả, lý do lịch sử dùng số đã ghi sổ; B4 lý do cảnh báo hoàn tiền dùng chuỗi như SePay; B5' câu hủy lượt chờ, câu tiền thừa, câu chuyển Admin kiểm trùng có bản riêng cho VNPay. Khuyến nghị reviewer chuyển sang phần 2: T1 log dùng chung còn ghi "SePay {SePayId}" (VNPay ra "SePay 0"); T2 nếu nghiệm thu VNPay đòi `01` cho hóa đơn không tồn tại thì tách nhánh (hiện trả `02` + cảnh báo hoàn tiền, đúng spec); T3 ghi cảnh báo vào hướng dẫn: thu thủ công bằng mã TXN thô trong lúc VNPay còn gọi lại IPN sẽ sinh cảnh báo hoàn tiền sai; T4 (có từ đợt 1) thu thủ công chưa cấm mã bắt đầu `VNPAY-`/`SEPAY-`.

## 4. Phần 2 — Cổng khách thuê, cổng quản lý, tài liệu

Spec: §4, §5.1, §5.5, §8, §9.3, §11.3, §12.

| Việc | Ghi chú |
| --- | --- |
| `IVnPayPaymentService.CreatePaymentUrlAsync(hoaDonId, nguoiThueId, clientIp, returnUrl)` | kiểm như spec §4.2; tham số spec §5.1; `vnp_OrderInfo` bỏ dấu |
| `IVnPayPaymentService.GetReturnResultAsync(query, nguoiThueId)` | chỉ đọc; tìm dòng `VNPAY-{txn}` chưa xóa của hóa đơn để hiện nút biên lai (có thể cần method store mới, không đổi schema) |
| `ThanhToanHoaDonController`: `POST /KhachThue/HoaDon/ThanhToan/VnPay/{hoaDonId}`, `GET /KhachThue/HoaDon/ThanhToan/VnPay/KetQua`, view kết quả | ReturnUrl dựng từ host của request; trang kết quả nghe `HoaDonThanhToanCapNhat` |
| Panel JSON thêm `vnPayEnabled`; `khachthue-thanhtoan.js` thêm hai tab (QR mặc định) | ẩn tab khi tắt hoặc số còn lại = 0 |
| Bảng mô tả `vnp_ResponseCode` (spec §5.5) | lấy từ tài liệu VNPay lúc code |
| Màn chi tiết hóa đơn quản lý: badge "VNPay" và mã tham chiếu như badge SePay (`Areas/QuanLyNhaTro/Views/HoaDon/Index.cshtml`) | |
| Mục VNPay sandbox trong `docs/features/thanh-toan-truc-tuyen/huong-dan-thiet-lap.md` và kịch bản V01–V07 | từng bước, không script |
| Cập nhật `README.md` module, `docs/features/README.md`, mục Hiện trạng `AGENTS.md` | |
| Test: Application unit (tạo link, trang kết quả), Web (tạo link cần đăng nhập và antiforgery, khách khác 404, trang kết quả sai chữ ký không lộ hóa đơn), kiểm giao diện ở 375 px | |

**Xong khi:** như phần 1, cộng thêm danh sách việc người dùng cần làm để thử V01–V07. Spec §15 tick sau khi người dùng thử tay xong.

## 5. Thứ tự và phụ thuộc

Phần 1 trước: phần 2 dùng bộ ký, nhãn, ghi sổ của phần 1. Người dùng chỉ thử tay được trên sandbox sau phần 2 (cần nút tạo link). Nếu V07 cho thấy cách encode chữ ký sai, sửa trong bộ ký của phần 1 và chạy lại test liên quan.

## 6. Người dùng cần chuẩn bị trước khi thử tay

Theo spec §12: `TmnCode`, `HashSecret` sandbox; URL IPN khai phía VNPay; tunnel ngrok; thẻ test do VNPay cấp. Bật `VnPay:Enabled = true` trong `appsettings.json` máy phát triển.
