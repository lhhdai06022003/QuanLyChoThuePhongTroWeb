# Spec: Thanh toán trực tuyến cho giữ chỗ của khách vãng lai — đợt 3

- **Trạng thái:** **đã duyệt** (26/10/2026). Quyết định ở §2; người dùng đồng ý spec như hiện tại, nên P1–P12 ở §13 chốt theo cột "Đề xuất". Migration §7 tạo và áp vào DB trước khi chạy `/ship`. Chưa có plan.
- **Nhánh:** `feature/thanh-toan-truc-tuyen` (người dùng chọn, 26/10/2026).
- **Nối tiếp:** [đợt 1 SePay](../sepay/spec.md) (bộ phân loại theo tiền tố §4.2, §4.4; thông báo §6), [đợt 2 VNPay](../vnpay/spec.md) (bộ ký, IPN, trang kết quả). Spec VNPay §1.1 đã để giữ chỗ ngoài phạm vi; spec này mở phần đó.
- **Liên quan:** [cổng khách vãng lai](../../cong-khach-vang-lai/SPEC.md) (giữ chỗ, đặt cọc, minh chứng, đối soát, áp cọc, hoàn tiền). Cổng này do `thuan` phụ trách; người dùng chọn làm tiếp trên nhánh thanh toán trực tuyến.
- **Schema:** **có đổi**, một cột (§7, điểm S1, đã xác nhận).

## 1. Mục tiêu

Khách vãng lai (hoặc khách thuê) đã được nhân viên duyệt giữ chỗ có thể trả tiền giữ chỗ bằng hai kênh, và hệ thống tự ghi nhận, không cần nhân viên đối soát:

1. **Chuyển khoản QR** qua tài khoản nhận tiền đã nối SePay. SePay gọi webhook, hệ thống ghi giao dịch giữ chỗ.
2. **Thẻ ATM / Visa / MasterCard qua VNPay**. VNPay gọi IPN, hệ thống ghi giao dịch giữ chỗ.

Đủ số tiền giữ chỗ thì giữ chỗ chuyển sang **Đang giữ chỗ** như khi nhân viên xác nhận. Cách cũ vẫn giữ: khách nộp minh chứng, nhân viên xác nhận tay (có hoặc không có minh chứng).

### 1.1 Phạm vi

| Có | Không có |
| --- | --- |
| Tiền giữ chỗ của `YeuCauThanhToanGiuCho` đã duyệt (trang đặt cọc `/tai-khoan/yeu-cau-phong/giu-cho/{id}/dat-coc`) | Thu tiền cọc còn thiếu sau khi ký hợp đồng (chưa có kênh thu riêng) |
| Ghi nhận tự động từ webhook SePay và IPN VNPay | Tiền mặt (người dùng chọn: tại chỗ khách cũng chuyển khoản) |
| Thông báo cho nhân viên và khách khi ghi nhận | Nhân viên tạo giữ chỗ hộ khách (người dùng chọn: khách tự tạo) |
| Chặn ghi trùng giữa luồng tự động và luồng xác nhận tay | Hoàn tiền qua API SePay/VNPay; hoàn tiền vẫn làm tay qua quyết định hoàn hiện có |
| Một thay đổi schema (§7) | Đồng bộ bù SePay cho giữ chỗ (P10) |

## 2. Quyết định đã chốt (người dùng, 26/10/2026)

| # | Câu hỏi | Quyết định |
| --- | --- | --- |
| G1 | Nhánh | Làm trên `feature/thanh-toan-truc-tuyen`. |
| G2 | Kênh | Cả SePay (QR) và VNPay. |
| G3 | Ai là người xác nhận của dòng ghi tự động | Đổi schema: `GiaoDichGiuCho.NguoiXacNhanId` được để trống; dòng tự động để trống, giống `LichSuThanhToan` của hóa đơn (§7). |
| G4 | Khách đến xem rồi đặt cọc luôn | Khách tự tạo yêu cầu giữ chỗ trên điện thoại, nhân viên duyệt, khách chuyển khoản tại chỗ. Không thêm tiền mặt, không thêm chức năng nhân viên tạo hộ. Giữ luồng minh chứng và nhân viên xác nhận tay. |
| G5 | Trả thiếu | Ghi khoản thiếu, khách trả nốt phần còn lại trước hạn; chỉ khi đủ số tiền giữ chỗ mới sang "Đang giữ chỗ" (cùng cách cộng dồn của luồng xác nhận tay hiện có). |

## 3. Hiện trạng code liên quan (đối chiếu 26/10/2026)

- **Duyệt giữ chỗ** (`ReservationStore.ApproveAsync`): `YeuCauGiuCho` `MoiTao → ChoThanhToan`, tạo một `YeuCauThanhToanGiuCho` (chỉ duyệt từ `MoiTao` nên mỗi giữ chỗ có một yêu cầu; plan kiểm lại) với `MaYeuCau = NoiDungChuyenKhoan = "GC" + 12 ký tự hex ngẫu nhiên` (unique, tối đa 50 ký tự), `SoTien`, `HanThanhToan`.
- **Trang đặt cọc** (`PhongController.DatCoc`, `Views/Phong/DatCoc.cshtml`): hiện ngân hàng, số tài khoản, chủ tài khoản (từ `VietQRSettings`) và nội dung chuyển khoản dạng chữ; **chưa có ảnh QR**; form nộp minh chứng; số đã xác nhận, số còn lại.
- **Nhân viên xác nhận tiền** (`GiuChoController.XacNhanTien` → `ReservationPaymentStore.ConfirmReceivedAsync`): transaction `Serializable`; bắt buộc nhập mã giao dịch ngân hàng; luôn ghi `PhuongThuc = ChuyenKhoan`; `MinhChungThanhToanGiuChoId` có thể trống; chặn trùng theo `MaGiaoDich` (unique trong bảng `GiaoDichGiuCho`) hoặc minh chứng đã dùng.
  - Còn hiệu lực và đúng hạn: cộng dồn; đủ thì giữ chỗ `DangGiuCho`, yêu cầu thanh toán `DaHoanTat`; chưa đủ thì `ChoXacNhanTien` / `DangDoiChieu`.
  - Quá hạn hoặc giữ chỗ không còn hiệu lực: vẫn ghi, `GhiChu = LATE_REFUND_REQUIRED`; nếu giữ chỗ đang `ChoThanhToan`/`ChoXacNhanTien` thì chuyển `HetHan`.
- **Hết hạn tự động** (`ReservationStore.ExpireUnpaidAsync`, gọi khi đọc danh sách): chỉ xét giữ chỗ `ChoThanhToan` đã quá hạn và **chưa có minh chứng**.
- **Áp cọc, hoàn tiền** (`ReservationSettlement`): tính từ tổng `SoTienThucNhan` của các dòng `GiaoDichGiuCho`. Bảng này **không có xóa mềm**, không có chức năng hủy ghi nhận.
- **Lịch sử trạng thái** của giữ chỗ và yêu cầu thanh toán đã có `LoaiTacNhan.HeThong` và `NguoiThucHienId` cho phép trống: không cần đổi schema cho lịch sử.
- **Thanh toán trực tuyến:** `IncomingTransferClassifier` phân loại theo bảng "tiền tố → `IIncomingTransferHandler`"; đợt 1 chỉ đăng ký `TTPTHD` → hóa đơn (spec SePay §4.4 đã để sẵn điểm mở rộng cho giữ chỗ). `TransferCodeParser` chỉ nhận mẫu `TTPTHD{id}`, nên mã `GC…` hiện bị xếp `Unmatched`. IPN VNPay chỉ nhận `vnp_TxnRef` dạng `HD{id}_{ticks}`.

## 4. Thuật ngữ

- **Số tiền giữ chỗ:** `YeuCauGiuCho.SoTienGiuCho` (bằng `YeuCauThanhToanGiuCho.SoTien` lúc duyệt).
- **Đã nhận:** Σ `GiaoDichGiuCho.SoTienThucNhan` của yêu cầu thanh toán (mọi dòng; bảng không có xóa mềm).
- **Còn lại:** số tiền giữ chỗ − đã nhận, không âm.
- **Mã chuyển khoản giữ chỗ:** xem P1.

## 5. Trải nghiệm khách

### 5.1 Trang đặt cọc

Khi yêu cầu thanh toán đang nhận tiền (giữ chỗ `ChoThanhToan` hoặc `ChoXacNhanTien`, chưa quá `HanThanhToan`, còn lại > 0) và kênh tương ứng đang bật (P8), trang đặt cọc có hai tab, giống panel hóa đơn khách thuê:

1. **Chuyển khoản QR** (mặc định): ảnh VietQR theo **số còn lại** và mã chuyển khoản, thông tin ngân hàng như hiện nay, nút sao chép, deeplink ngân hàng như panel hóa đơn. Ghi chú: "Giữ nguyên nội dung chuyển khoản để hệ thống tự ghi nhận."
2. **Thẻ ATM / Visa / MasterCard (VNPay)**: số tiền = số còn lại (chỉ đọc), các ghi chú như spec VNPay §4.1, nút **Thanh toán qua VNPay**.

Dưới hai tab vẫn có form nộp minh chứng như hiện nay (G4), kèm câu: "Nếu đã chuyển khoản đúng nội dung, bạn không cần nộp minh chứng."

Kênh tắt thì không có tab đó; cả hai tắt thì trang giống hiện nay. Chưa cấu hình tài khoản VietQR thì không có tab QR, giữ câu "Vui lòng liên hệ nhân viên…" như hiện nay.

Sau khi hệ thống ghi nhận, trang tự tải lại qua SignalR (P6) và hiện số đã nhận, số còn lại hoặc trạng thái "Đang giữ chỗ".

### 5.2 Tạo link VNPay

- `POST /tai-khoan/yeu-cau-phong/giu-cho/{reservationId}/dat-coc/vnpay` (antiforgery qua header, vai trò `KhachVangLai,KhachThue`).
- Server kiểm lại lúc bấm: giữ chỗ thuộc người đăng nhập (không thuộc thì `NotFound`), đang nhận tiền như §5.1, số còn lại > 0 và là số nguyên.
- Số tiền = số còn lại (G5: trả thiếu bằng QR trước thì VNPay lấy phần còn lại).
- `vnp_ExpireDate` = sớm hơn giữa `now + VnPay:ExpireMinutes` và `HanThanhToan` (P2). Còn dưới 5 phút tới hạn thì từ chối: "Sắp hết hạn thanh toán, vui lòng liên hệ nhân viên."
- Không ghi DB khi tạo link.

### 5.3 Trang kết quả VNPay cho giữ chỗ

- `GET /tai-khoan/yeu-cau-phong/giu-cho/dat-coc/vnpay/ket-qua`, chỉ đọc, cùng bộ trạng thái với trang kết quả hóa đơn (spec VNPay §4.3): không xác thực, đã hủy, không thành công, đã ghi nhận (kèm số đã nhận và còn lại), đang ghi nhận (SignalR + nút "Kiểm tra lại"), không ghi nhận như mong đợi (tiền về khi giữ chỗ không còn hiệu lực, xem P4).
- Nút quay về trang đặt cọc của giữ chỗ đó.

## 6. Ghi nhận tự động

### 6.1 Nhận diện

- **SePay:** thêm bộ xử lý giữ chỗ vào bộ phân loại với tiền tố riêng (P1); `TransferCodeParser` thêm mẫu tương ứng. Phần ghi sổ hóa đơn không đổi.
- **VNPay:** một URL IPN duy nhất (khai ở cổng merchant), nên IPN `GET /api/webhooks/payment/vnpay-ipn` phân nhánh theo tiền tố `vnp_TxnRef`: `HD…` → hóa đơn (không đổi), tiền tố giữ chỗ (P2) → giữ chỗ. Các bước kiểm chữ ký, `vnp_TmnCode`, mã phản hồi, số tiền giữ nguyên spec VNPay §5.2.

### 6.2 Ghi một dòng `GiaoDichGiuCho`

Chạy cùng quy tắc với `ConfirmReceivedAsync` (§3), trong transaction `Serializable`, khác ở các điểm sau:

| Trường / bước | Ghi tự động |
| --- | --- |
| `MaGiaoDich` | Khóa chống trùng của kênh: `SEPAY-{referenceCode}` (hoặc `SEPAY-ID-{id}` khi thiếu mã tham chiếu), `VNPAY-{vnp_TransactionNo}` — cùng dạng với hóa đơn (P3) |
| `SoTienThucNhan` | Số tiền thực nhận của giao dịch (SePay `transferAmount`, VNPay `vnp_Amount / 100`), không cắt theo số còn lại (P5) |
| `PhuongThuc` | `ChuyenKhoan` |
| `NgayThucNhan` | Giờ giao dịch đổi sang UTC (SePay `transactionDate`, VNPay `vnp_PayDate`); thiếu thì giờ server |
| `MinhChungThanhToanGiuChoId` | trống |
| `NguoiXacNhanId` | **trống** (G3, §7) |
| `NgayXacNhan` | giờ server |
| `GhiChu` | Như luồng tay: `LATE_REFUND_REQUIRED` khi quá hạn hoặc giữ chỗ không còn hiệu lực (P4). Có thể thêm nhãn kênh (`[SEPAY]`/`[VNPAY]`, mã tham chiếu) nếu không làm hỏng chỗ đọc `GhiChu` hiện có — plan kiểm |
| Lịch sử trạng thái | `LoaiTacNhan = HeThong`, `NguoiThucHienId = null` |
| Chuyển trạng thái | Như luồng tay: đủ tiền → giữ chỗ `DangGiuCho`, yêu cầu `DaHoanTat`; thiếu → `ChoXacNhanTien` / `DangDoiChieu` (G5); quá hạn → `HetHan` |

Kết quả trả về cho kênh:

| Kết quả | SePay webhook | VNPay IPN |
| --- | --- | --- |
| Ghi được (kể cả ghi kèm `LATE_REFUND_REQUIRED`) | 200 như đợt 1 | `00` |
| Trùng khóa | 200, không ghi | `02` |
| Không tìm thấy yêu cầu thanh toán theo mã | `Unmatched` như đợt 1 | `01` |
| Lỗi hệ thống | lỗi như đợt 1 | `99` |

### 6.3 Chặn ghi trùng với luồng tay (P3, P12)

- Bảng `GiaoDichGiuCho` không có xóa mềm, nên mọi dòng đều tính khi kiểm trùng.
- Nhân viên xác nhận tiền: mã nhập bắt đầu bằng `SEPAY-`/`VNPAY-` bị từ chối (cùng quy tắc T4 của hóa đơn); mã gốc mà bảng giữ chỗ đã có `SEPAY-{mã}` hoặc `VNPAY-{mã}` thì báo "Mã giao dịch này đã được hệ thống ghi nhận tự động."
- Khi hệ thống ghi tự động mà yêu cầu còn minh chứng `ChoXacNhan`: thông báo nhân viên kiểm trùng trước khi xác nhận minh chứng đó (giống câu cảnh báo của hóa đơn).

## 7. Thay đổi schema (S1, đã duyệt)

| Mục | Nội dung |
| --- | --- |
| Entity | `GiaoDichGiuCho.NguoiXacNhanId`: `int` → `int?`; navigation `NguoiXacNhan` → `NguoiDung?` |
| Configuration | `GiaoDichGiuChoConfiguration`: quan hệ `NguoiXacNhan` không bắt buộc; giữ `OnDelete(Restrict)`, giữ các index hiện có |
| Migration | Một migration mới (tên dự kiến `AllowSystemReservationTransaction`), chỉ một lệnh `AlterColumn` bỏ NOT NULL của cột người xác nhận trên bảng giao dịch giữ chỗ. Không đổi dữ liệu, không đổi bảng khác, không sửa migration cũ |
| `Down` | Chỉ chạy được khi chưa có dòng trống người xác nhận; nếu đã có thì phải xử lý dữ liệu trước. Ghi rõ trong migration và trong plan |
| Code đọc cột | Hiện chỉ có chỗ ghi (`ConfirmReceivedAsync`), không có chỗ đọc tên người xác nhận của dòng giữ chỗ; nếu thêm hiển thị thì dòng trống hiện "Hệ thống (SePay/VNPay)" |
| Kiểm chứng | `has-pending-model-changes` sau migration báo không còn thay đổi; chạy migration trên DB test (fixture tự migrate); test Infrastructure ghi được dòng trống người xác nhận; skill `kiem-tra-migration` |

| Điểm | Bảng.cột | Kiểu | Nullable | Default | CHECK / index / FK | `Down` | Dữ liệu cũ |
| --- | --- | --- | --- | --- | --- | --- | --- |
| S1 | `giao_dich_giu_cho."NguoiXacNhanId"` | `integer` (không đổi) | `NOT NULL` → cho phép `NULL` | không có | Giữ FK tới `nguoi_dung` (`Restrict`), giữ index `NguoiXacNhanId` và các index, CHECK khác của bảng | Đặt lại `NOT NULL`; lỗi nếu đã có dòng `NULL` | Không đổi; mọi dòng hiện có giữ người xác nhận |

ĐÃ XÁC NHẬN: S1 — cho phép `giao_dich_giu_cho."NguoiXacNhanId"` để trống, thêm migration và cập nhật DB trước khi chạy `/ship` (người dùng, 26/10/2026).

**Đã tạo 26/10/2026, chưa commit:** `20261026140701_AllowSystemReservationTransaction` (sinh bằng `dotnet ef migrations add`). `Up` đúng một câu `ALTER TABLE giao_dich_giu_cho ALTER COLUMN "NguoiXacNhanId" DROP NOT NULL`; snapshot chỉ đổi kiểu `int?` và bỏ `IsRequired()` của quan hệ; không đổi khóa ngoại, index. `Down` do EF sinh: `UPDATE … SET "NguoiXacNhanId" = 0 WHERE … IS NULL`, `SET NOT NULL`, `SET DEFAULT 0` — có dòng để trống thì lỗi khóa ngoại (không có người dùng id 0) và cả transaction rollback; không có dòng để trống thì cột còn thừa `DEFAULT 0` so với trước. Đã áp vào DB test `quanlyphongtro_test` (fixture), `has-pending-model-changes` sạch, test `ReservationSystemTransactionSchemaTests` PASS.

## 8. Thông báo

Đề xuất P6:

- **Trong app:** nhân viên chi nhánh của phòng và Admin, như thông báo hóa đơn: "Đã nhận tiền giữ chỗ {SoTien}đ qua {SePay|VNPay} — phòng {SoPhong}". Đủ tiền thì thêm "Giữ chỗ đã có hiệu lực".
- **Telegram:** chat chung + chat chi nhánh như đợt 1; dòng "Kênh", "Mã GD", "Còn lại". Tiền về khi không còn hiệu lực hoặc trả thừa: tiêu đề "TIỀN GIỮ CHỖ CẦN HOÀN".
- **Email cho khách:** "Đã nhận {SoTien}đ cho giữ chỗ phòng {SoPhong}; còn lại {ConLai}đ" hoặc "Giữ chỗ đã có hiệu lực tới {HanKyHopDong}".
- **SignalR:** sự kiện cho người dùng sở hữu giữ chỗ để trang đặt cọc và trang kết quả tự tải lại.
- Dùng chung hàng đợi và `PaymentNotificationWorker`; lỗi gửi không hoàn tác ghi nhận.

## 9. Cổng quản lý

- Màn chi tiết giữ chỗ (`GiuChoController.ChiTiet`): danh sách giao dịch hiện badge **SePay** / **VNPay** và mã tham chiếu cho dòng tự động, "Hệ thống" ở cột người xác nhận.
- Không thêm hủy ghi nhận (P7).
- Màn Giao dịch SePay (Admin) hiện giao dịch giữ chỗ để đối chiếu, không ghi bù (P10).

## 10. Kiến trúc (định hướng, plan chốt)

- **Application:** bộ xử lý giữ chỗ cài `IIncomingTransferHandler` (tiền tố P1); nhánh giữ chỗ trong `VnPayPaymentService` hoặc service VNPay riêng cho giữ chỗ; tạo link và trang kết quả chỉ đọc. Phương thức ghi tự động không có `actorId`, phải vào danh sách ngoại lệ của `BranchScopeArchitectureTests` kèm lý do "tin cậy nhờ chữ ký/HMAC".
- **Store:** thêm phương thức ghi tự động trong `IReservationPaymentStore`, dùng chung phần chuyển trạng thái với `ConfirmReceivedAsync` để hai luồng không lệch nhau.
- **Kết quả và thông báo:** `TransferOutcome` hiện gắn với hóa đơn (`HoaDonId`); plan chọn mở rộng hay thêm kiểu kết quả riêng cho giữ chỗ.
- **Web:** route §5.2, §5.3; trang đặt cọc thêm tab; không sửa `Program.cs`.

## 11. Cấu hình

Đề xuất P8: thêm cờ `OnlinePayment:ReservationEnabled` (mặc định `false`) vào `appsettings.example.json`. Tab QR tự động cần `SePay:Enabled` và cờ này; tab VNPay cần `VnPay:Enabled` và cờ này. Không ghi đè `appsettings.json` của người dùng.

## 12. Kiểm thử

- **Unit (Application):** nhận diện mã giữ chỗ (đúng, sai tiền tố, có dấu cách, không lẫn với `TTPTHD`); tiền tố `vnp_TxnRef` giữ chỗ; tạo link (không thuộc khách, chưa duyệt, quá hạn, sắp hết hạn, đã đủ, số lẻ, số tiền = còn lại); ghi tự động: đủ, thiếu rồi trả nốt (G5), thừa, quá hạn, giữ chỗ đã hủy/từ chối/hết hạn, trùng khóa, có minh chứng chờ; chặn mã nhập tay có tiền tố và mã gốc đã tự ghi; thông báo; test hóa đơn hiện có PASS không đổi kỳ vọng.
- **Infrastructure:** ghi dòng có `NguoiXacNhanId` trống; hai webhook song song cùng mã tham chiếu chỉ ra một dòng; ghi tự động và nhân viên xác nhận cùng lúc; áp cọc/hoàn tiền vẫn tính đúng với dòng tự động.
- **Web:** webhook SePay mã giữ chỗ → ghi; IPN giữ chỗ (chữ ký sai `97`, hợp lệ `00`, gọi lại `02`); tạo link cần đăng nhập và antiforgery, khách khác 404; trang kết quả sai chữ ký không lộ thông tin; trang đặt cọc có/không có tab theo cờ.
- **Thử tay (người dùng):** G01 QR đủ tiền; G02 QR thiếu rồi VNPay phần còn lại; G03 VNPay đủ tiền; G04 hủy trên VNPay; G05 trả sau hạn → ghi kèm "cần hoàn" và Telegram; G06 khách vừa nộp minh chứng vừa chuyển đúng nội dung → nhân viên được cảnh báo kiểm trùng; G07 tắt cờ → trang như cũ.

## 13. Đề xuất (đã chốt theo cột "Đề xuất", 26/10/2026)

| Mã | Điểm | Đề xuất | Cách khác |
| --- | --- | --- | --- |
| P1 | Mã chuyển khoản giữ chỗ | Yêu cầu duyệt mới dùng `TTPTGC{YeuCauGiuChoId}` (mỗi giữ chỗ chỉ có một yêu cầu thanh toán nên vẫn unique; cùng kiểu với `TTPTHD{id}`, dễ gõ). Yêu cầu đã duyệt trước đó giữ mã `GC…` và chỉ đối soát tay | Giữ mã `GC` + 12 hex ngẫu nhiên và nhận diện mẫu đó (không đổi cách sinh mã, nhưng mẫu ngắn, dễ khớp nhầm chữ khác) |
| P1' | Cấu hình SePay | Giữ tiền tố SePay là `TTPTHD` hoặc để trống: khi trường `code` của SePay trống, hệ thống tự tìm mã trong nội dung. **Người dùng cần thử** cách SePay tách `code` với mã `TTPTGC…` trên Test mode | — |
| P2 | `vnp_TxnRef` giữ chỗ | `GC{YeuCauGiuChoId}_{Ticks}`; `vnp_ExpireDate` không vượt `HanThanhToan`; từ chối tạo link khi còn dưới 5 phút | Không giới hạn theo `HanThanhToan` (tiền về sau hạn xử lý theo P4) |
| P3 | Khóa chống trùng | Dùng `SEPAY-…`/`VNPAY-…` như hóa đơn, kiểm trong bảng giữ chỗ; mã nhập tay có tiền tố bị từ chối; mã gốc trùng dòng tự động bị báo trùng | — |
| P4 | Tiền về khi quá hạn hoặc giữ chỗ không còn hiệu lực (`TuChoi`, `DaHuy`, `HetHan`, `DaChuyenHopDong`) | Ghi như luồng tay (`LATE_REFUND_REQUIRED`, giữ chỗ còn chờ thì sang `HetHan`), IPN trả `00`, Telegram "cần hoàn". Lý do: tiền đã vào tài khoản, quyết định hoàn hiện có tính theo các dòng đã ghi | Không ghi, chỉ cảnh báo (như hóa đơn trả `02`); khi đó việc hoàn tiền không đi qua quyết định hoàn hiện có |
| P5 | Trả thừa | Ghi đủ số thực nhận (như luồng tay); phần vượt xử lý bằng quyết định hoàn hiện có; Telegram ghi số thừa | Chỉ ghi tới số còn lại như hóa đơn (khi đó phần thừa không có trong sổ giữ chỗ) |
| P6 | Thông báo | Như §8 | Bớt kênh (ví dụ không email) |
| P7 | Hủy ghi nhận dòng tự động | Không làm (bảng không có xóa mềm; ghi sai thì xử lý bằng quyết định hoàn) | Thêm hủy ghi nhận: cần thêm cột xóa mềm, đổi schema thêm |
| P8 | Cờ bật | `OnlinePayment:ReservationEnabled` riêng, mặc định tắt | Dùng thẳng `SePay:Enabled`/`VnPay:Enabled` (bật hóa đơn là bật luôn giữ chỗ) |
| P9 | Biên nhận cho khách | Không làm ở đợt này; email và trang đặt cọc hiện số đã nhận | Thêm biên nhận web như biên lai hóa đơn |
| P10 | Đồng bộ bù SePay | Màn Giao dịch SePay chỉ đối chiếu giao dịch giữ chỗ, không ghi bù (như hóa đơn khi `SyncRecordEnabled` tắt) | Ghi bù cho giữ chỗ |
| P11 | Giữ chỗ trả một phần rồi quá hạn | Giữ như hiện tại: `ExpireUnpaidAsync` không tự hết hạn giữ chỗ đã có tiền; nhân viên xử lý; khoản về sau hạn vẫn theo P4 | Mở rộng hết hạn tự động cho giữ chỗ trả một phần (đổi hành vi cổng khách) |
| P12 | Minh chứng chờ khi tiền tự về | Cảnh báo nhân viên kiểm trùng (§6.3); không tự đóng minh chứng | Tự chuyển minh chứng chờ sang "đã xác nhận" khi số khai bằng số tự ghi |

## 14. Việc người dùng tự làm

- Bật cờ (P8) trong `appsettings.json` máy phát triển.
- Thử trên SePay Test mode cách SePay tách mã `TTPTGC…` (P1').
- Không cần khai thêm URL IPN VNPay (dùng chung URL hiện có).
- Thử tay G01–G07.

## 15. Điều kiện nghiệm thu

- [x] Spec được duyệt, P1–P12 chốt theo đề xuất (26/10/2026).
- [ ] Migration §7 được review, chạy trên DB test; `has-pending-model-changes` sạch.
- [ ] Test §12 PASS; test hóa đơn hiện có PASS không đổi kỳ vọng.
- [ ] Thử tay G01–G07.
- [ ] Cập nhật README module, `docs/features/README.md`, hướng dẫn thiết lập, mục Hiện trạng `AGENTS.md`.

## 16. Lịch sử thay đổi

| Ngày | Thay đổi |
| --- | --- |
| 26/10/2026 | Bản nháp đầu theo 5 quyết định của người dùng (G1–G5); các điểm còn lại ghi là đề xuất P1–P12 chờ duyệt. |
| 26/10/2026 | Người dùng duyệt spec như hiện tại: P1–P12 theo đề xuất; S1 (§7) đã xác nhận, yêu cầu thêm migration và cập nhật DB trước khi chạy `/ship`. |
