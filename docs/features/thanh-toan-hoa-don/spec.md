# Đặc tả module 2: Thanh toán hóa đơn

- Chủ sở hữu: Người A.
- Phiên bản: 2.1, ngày 03/10/2026. Bản 2.0 (02/10/2026) hợp nhất bản đầu với bản "spec1" về nghiệp vụ vận hành; bản 2.1 cập nhật theo các quyết định chốt khi triển khai (§36, mục 5–8) và tên service thực tế.
- Phạm vi: khách thuê thanh toán hóa đơn đã gửi bằng chuyển khoản VietQR theo từng **lượt thanh toán** và tải minh chứng; nhân viên/Admin đối chiếu với sao kê rồi xác nhận, ghi nhận số thực nhận, xử lý tiền thừa hoặc từ chối; Admin bật thanh toán một phần; thu tiền thủ công theo số thực nhận; hủy ghi nhận thanh toán; tra cứu lượt cũ theo mã; thông báo realtime trong app. Người A sở hữu trọn luồng, gồm `Areas/QuanLyNhaTro`, `Areas/KhachThue` và các sửa đổi nhỏ ở module 1 nêu tại §31.
- Ngoài phạm vi: lập, chốt, công bố hóa đơn (module 1, trừ §31); thanh toán giữ chỗ, cọc và hoàn tiền (người B, bảng và service riêng); đọc sao kê hoặc webhook ngân hàng; ví hoặc số dư khách; tự phân bổ một giao dịch cho nhiều hóa đơn; email thông báo thanh toán.
- **Không đổi schema.** Mọi bảng, cột, enum và unique index dùng tới đã có từ Migration 1. Yêu cầu nào không làm đúng được với schema hiện tại thì ghi là giới hạn (§34), không làm sai dữ liệu để "vừa schema".

---

## 1. Mục tiêu nghiệp vụ

Tại mọi thời điểm, hệ thống trả lời đúng:

1. Khách đang nợ bao nhiêu?
2. Khách khai đã chuyển bao nhiêu?
3. Nhân viên/Admin đã xác nhận thực nhận bao nhiêu?
4. Khoản nào đang chờ xử lý hoặc bất thường?

Mục tiêu quan trọng nhất: **ứng dụng không làm sai sự thật tài chính.** Lượt thanh toán, QR và minh chứng chỉ là dữ liệu hỗ trợ; chỉ khoản đã được xác nhận mới làm giảm công nợ.

### 1.1 Bốn khái niệm tách biệt

| Khái niệm | Entity | Ý nghĩa |
| --- | --- | --- |
| Hóa đơn | `HoaDon` | Nghĩa vụ thanh toán của khách |
| Lượt thanh toán | `YeuCauThanhToanHoaDon` | Ý định trả một số tiền cho một hóa đơn, kèm mã chuyển khoản và QR |
| Minh chứng | `MinhChungThanhToanHoaDon` | Khách khai đã chuyển tiền; không tự làm giảm công nợ |
| Ghi nhận thanh toán (ledger) | `LichSuThanhToan` | Khoản tiền nhân viên/Admin xác nhận là thực nhận; nguồn duy nhất làm giảm công nợ |

Trên giao diện dùng từ "lượt thanh toán"; trong code giữ tên entity `YeuCauThanhToanHoaDon`.

### 1.2 Hai loại số tiền

| | Ai quyết định | Chính sách "không cho trả một phần" có chặn không |
| --- | --- | --- |
| Số tiền của lượt và QR | Hệ thống | **Có**: lượt luôn bằng đúng số còn lại |
| Số tiền thực nhận trên tài khoản | Ngân hàng, khách | **Không**: hệ thống chỉ ghi đúng sự thật |

QR nhúng sẵn số tiền nhưng không bảo đảm số tiền thật vào tài khoản: tùy app ngân hàng có cho sửa số tiền hay không; khách có thể tự gõ số tài khoản thay vì quét QR; có thể chuyển hai lần; người thân có thể chuyển hộ. Vì vậy luồng chính luôn là "đúng số tiền lượt", còn các luồng chênh lệch (§15, §16) là lối thoát hiếm dùng do người đối chiếu thực hiện sau khi xem sao kê.

---

## 2. Hiện trạng (đã kiểm tra trong code ngày 02/10/2026)

- `InvoicePaymentConfirmationService` có tạo yêu cầu, nộp minh chứng, xác nhận, từ chối và unit test, nhưng Web chưa gọi. Service ném exception, không kiểm tra quyền khách hoặc chi nhánh, không khóa dòng `HoaDon` khi xác nhận, cho nhiều yêu cầu cùng hoạt động và sinh `MaYeuCau` theo giây.
- `KhachThue/HoaDon/GetVietQR` sinh QR luôn theo `TongTien` với nội dung `THANH TOAN {MaHoaDon}`; khách không có chỗ nộp minh chứng.
- `HoaDonService.ThuTienAsync` chỉ thu đủ `TongTien` một lần khi hóa đơn `ChuaThanhToan`; mã giao dịch `GD-{yyyyMMddHHmmss}-{id}` có thể trùng khi thu hai lần trong cùng giây.
- `LichSuThanhToanService.HuyGiaoDichAsync` luôn đưa hóa đơn về `ChuaThanhToan` (sai khi còn khoản khác), không kiểm tra quyền trong service, không khóa dòng, không có lý do, không ghi lịch sử trạng thái.
- Trạng thái `HetHan` của lượt chưa được dùng.
- `HoaDonCalculatorService` làm tròn tiền tới 2 chữ số thập phân, nên `TongTien` có thể lẻ (ví dụ 1.233.333,33), trong khi `VietQRService` ghi số tiền bằng `ToString("0")`. Khách chuyển đúng số trên QR vẫn thiếu phần lẻ và hóa đơn kẹt ở `ThanhToanMotPhan`. Xử lý ở §31.1.
- `HoaDon.HuyHoaDon` chỉ kiểm tra `TrangThaiHoaDon`, nhưng `HoaDonService.DeleteHoaDonAsync` đã chặn hủy khi có bất kỳ yêu cầu hoặc minh chứng chờ xử lý (`GetCancellationBlockersAsync`), kể cả lượt `ChoThanhToan` mà khách bỏ dở. §31.2 nới quy tắc này: chỉ chặn `DangDoiChieu`, tự hủy lượt `ChoThanhToan`.
- `TongTien` chỉ sửa được khi hóa đơn còn `Nhap` (`HoaDonService` sửa chi tiết, `YeuCauSuCoService`, `MeterReadingWorkflowService` đều chặn), còn thanh toán chỉ có sau `DaGui`. Quy tắc "không sửa tổng khi đã có thanh toán" (§8.1) vì vậy đã được bảo đảm, chỉ cần test giữ lại.

Schema liên quan:

- `LichSuThanhToan`: unique `MaGiaoDich` lọc `MaGiaoDich <> '' AND IsDeleted = false` (sau khi hủy ghi nhận, cùng mã được ghi lại); unique `MinhChungThanhToanHoaDonId` lọc `IS NOT NULL` (không lọc `IsDeleted`, nên một minh chứng chỉ gắn với một bản ghi ledger trong suốt vòng đời).
- `YeuCauThanhToanHoaDon`: unique `MaYeuCau`.
- `TrangThaiYeuCauThanhToan`: `ChoThanhToan`, `DaBaoChuyen`, `DangDoiChieu`, `DaHoanTat`, `TuChoi`, `HetHan`, `DaHuy`. Module này không dùng `DaBaoChuyen` và `TuChoi` cho lượt; từ chối được ghi ở minh chứng.
- Không có cột cho: số tiền người đối chiếu xác minh, lý do/người/thời điểm hủy ghi nhận, cờ "chờ Admin". Cách thay thế ở §15–§20.

---

## 3. Nguyên tắc bất biến

1. Hóa đơn là công nợ; lượt thanh toán không phải tiền.
2. Minh chứng không phải tiền; chỉ tiền thực nhận đã xác nhận mới là tiền.
3. Chỉ `LichSuThanhToan` chưa xóa mềm mới làm giảm công nợ.
4. Không ghi nhận số tiền lớn hơn số thực nhận đã xác minh, và không ghi tổng vượt `TongTien`.
5. Không ghi cùng một giao dịch ngân hàng hai lần.
6. Chính sách không cho trả một phần chỉ giới hạn luồng tự phục vụ của khách; không được phủ nhận khoản tiền đã thực nhận.
7. Khoản thanh toán đã xác nhận không sửa trực tiếp; sai thì hủy ghi nhận rồi tạo ghi nhận mới.
8. Hóa đơn đã có thanh toán không được đổi `TongTien`.
9. Lượt đã hủy hoặc hết hạn vẫn tra cứu được bằng mã vì tiền có thể tới sau.
10. Chênh lệch giữa thực nhận và lượt phải được ghi rõ, không tự sửa cho khớp.
11. Khách đã rời phòng vẫn thanh toán được hóa đơn của mình.
12. Tên người chuyển tiền không bắt buộc trùng tên người thuê.
13. Không hard-delete dữ liệu tài chính (lượt, minh chứng, ledger, lịch sử trạng thái).

---

## 4. Vai trò và quyền

Không thêm bảng quyền hay màn hình cấp quyền. Thêm hai hằng vào `EmployeeActionCodes` (và `ValidActionsSet`) để gọi `IEmployeeAccessService.CanPerformAsync`:

- `Payment.Review`: xem hàng đợi, xem ảnh minh chứng, xác nhận, ghi nhận số thực nhận, từ chối, tra cứu lượt. Nhân viên đang được phân công (`NhanVienChiNhanh.IsActive`) tại chi nhánh của hóa đơn, hoặc Admin.
- `Payment.ConfigurePartial`: bật/tắt thanh toán một phần. Thêm vào `AdminOnlySet`.

| Hành động | Khách đứng tên hợp đồng | Nhân viên đúng chi nhánh | Admin |
| --- | --- | --- | --- |
| Xem khung thanh toán, tạo/hủy lượt, nộp minh chứng | Có, hóa đơn của mình | Không | Không |
| Xem ảnh minh chứng | Có, hóa đơn của mình | Có | Có |
| Xem hàng đợi, xác nhận đúng số, từ chối | Không | Có | Có |
| Ghi nhận số thực nhận khác số lượt nhưng ≤ còn nợ (§15) | Không | Có | Có |
| Xử lý tiền thừa > còn nợ (§16) | Không | Không, chỉ chuyển Admin | Có |
| Bật/tắt thanh toán một phần | Không | Không | Có |
| Thu tiền thủ công ≤ còn nợ | Không | Có (`Invoice.Send`, như hiện tại) | Có |
| Thu tiền thủ công kèm tiền thừa | Không | Không | Có |
| Hủy ghi nhận thanh toán | Không | Không | Có |
| Tra cứu lượt theo mã (mọi trạng thái) | Không | Có, theo chi nhánh | Có |

### 4.1 Quyền khách

Dựa trên `HopDong.NguoiThueId` của hóa đơn, cùng quy tắc với `IHoaDonStore.CheckHoaDonOwnershipAsync`. Không yêu cầu hợp đồng còn hiệu lực. Để thao tác ghi, hóa đơn phải `DaGui` và chưa xóa. Chỉ người đứng tên hợp đồng có quyền; hệ thống không có khái niệm người ở cùng thanh toán thay.

Application tự kiểm tra quyền; không tin `ChiNhanhId`, role hay id người dùng từ client, không chỉ dựa vào ẩn nút.

---

## 5. Lượt thanh toán

Khi khách bấm **"Thanh toán"** trên hóa đơn đã gửi, hệ thống tạo một lượt gồm: hóa đơn, số tiền của lượt, mã lượt (`MaYeuCau`) dùng làm nội dung chuyển khoản, QR VietQR theo đúng số tiền và nội dung đó, hạn của lượt, và các minh chứng khách nộp. Khách không cần ai duyệt để tạo lượt.

Cần lượt riêng vì: một hóa đơn có thể trả qua nhiều lượt khi được trả một phần; mỗi lượt có mã riêng để khớp sao kê; minh chứng nộp lại sau khi bị từ chối gắn vào cùng lượt; QR cũ không treo mãi; giữ được dấu vết khi lượt bị hủy hoặc hết hạn nhưng tiền tới sau.

### 5.1 Một lượt đang hoạt động cho mỗi hóa đơn

Lượt **đang hoạt động** (effective active) khi:

```text
TrangThai = DangDoiChieu
OR (TrangThai = ChoThanhToan AND HanThanhToan >= nowUtc)
```

Mỗi hóa đơn có tối đa một lượt đang hoạt động. Điều kiện này được viết **một lần** trong store (`IInvoicePaymentStore`) và dùng chung cho mọi truy vấn, không lặp lại ở nhiều nơi.

---

## 6. Vòng đời lượt thanh toán

### 6.1 Sơ đồ

```text
                 khách tạo
                     │
                     ▼
   ┌──────────► ChoThanhToan ──── khách hủy ──────────────────► DaHuy
   │                 │      ├──── quá hạn ────────────────────► HetHan
   │                 │      ├──── thu thủ công (§18) ─────────► DaHuy
   │                 │      └──── hủy hóa đơn (§31.2) ────────► DaHuy
   │                 │ khách nộp minh chứng
   │                 ▼
   └─ NV từ chối ─ DangDoiChieu ── NV xác nhận / ghi nhận ───► DaHoanTat
                     │   ▲            số thực nhận / Admin xử lý tiền thừa
                     └───┘ NV chuyển Admin (§16, dòng lịch sử, trạng thái không đổi)
```

- `ChoThanhToan` có hạn; `DangDoiChieu` không tự hết hạn.
- Từ chối minh chứng đưa lượt về `ChoThanhToan` với hạn mới = thời điểm từ chối + `YeuCauHetHanSauGio`.
- Mỗi lần chuyển trạng thái ghi một dòng `LichSuTrangThaiYeuCauThanhToanHoaDon` có người thực hiện (null nếu hệ thống tự hết hạn) và lý do.
- Không xóa lượt `DaHuy`, `HetHan`, `DaHoanTat`.

### 6.2 Hạn hóa đơn khác hạn lượt

- **Hạn hóa đơn** (`HoaDon.HanThanhToan`): hạn nghĩa vụ. Hóa đơn quá hạn vẫn thanh toán được.
- **Hạn lượt** (`YeuCauThanhToanHoaDon.HanThanhToan`) = thời điểm tạo + `InvoicePaymentOptions.YeuCauHetHanSauGio`, **mặc định 24 giờ**, cấu hình được. Chọn 24 giờ vì chỉ người đứng tên hợp đồng thanh toán (không có tranh chấp giữa người ở ghép, khách tự hủy được lượt của mình), và để giảm trường hợp khách chuyển tối nay, sáng mai mới nộp minh chứng thì lượt đã hết hạn.

### 6.3 Hết hạn không cần job nền

Mọi thao tác ghi liên quan đến một hóa đơn (tạo lượt, hủy lượt, nộp minh chứng, thu thủ công, cấu hình một phần, hủy hóa đơn) khóa dòng `HoaDon` trước. Nếu gặp lượt `ChoThanhToan` có `HanThanhToan < nowUtc`, chuyển lượt đó sang `HetHan` (ghi lịch sử, lý do "Quá hạn lượt thanh toán") rồi xử lý tiếp.

Truy vấn chỉ đọc hiển thị lượt quá hạn là "Hết hạn" (effective status) mà không ghi.

### 6.4 Lỗi nghiệp vụ vẫn phải lưu trạng thái hết hạn

Khi thao tác (ví dụ nộp minh chứng) gặp đúng lượt vừa quá hạn: chuyển lượt sang `HetHan`, `SaveChanges`, **commit**, rồi mới trả lỗi nghiệp vụ "Lượt thanh toán đã hết hạn". Service không áp quy ước "Fail ⇒ rollback tất cả"; mỗi nhánh trả lỗi tự quyết định commit hay rollback, và nhánh này được test riêng.

---

## 7. Trạng thái minh chứng

`MinhChungThanhToanHoaDon.TrangThaiDoiChieu`: `ChoXacNhan` → `DaXacNhan` hoặc `TuChoi`.

- Mỗi lượt có tối đa một minh chứng `ChoXacNhan`.
- `TuChoi` bắt buộc lý do (tối đa 500 ký tự), lưu `LyDoTuChoi`, `NguoiDoiChieuId`, `NgayDoiChieu`.
- Minh chứng bị từ chối được giữ làm lịch sử, khách thấy lý do.
- Minh chứng không tự tạo `LichSuThanhToan`; chỉ hành động của người đối chiếu mới tạo.
- `NgayTao` của minh chứng là thời điểm nộp theo đồng hồ server.

---

## 8. Trạng thái thanh toán của hóa đơn

`TrangThaiHoaDon` luôn được **tính lại** từ tổng `SoTienThanhToan` của các `LichSuThanhToan` chưa xóa mềm, không cộng dồn:

- tổng = 0 → `ChuaThanhToan`;
- 0 < tổng < `TongTien` → `ThanhToanMotPhan`;
- tổng = `TongTien` → `DaThanhToan`;
- tổng > `TongTien` hoặc tổng < 0 → ném lỗi (dữ liệu hỏng hoặc bug), service rollback và log Error.

Quy tắc đặt trong method mới của entity `HoaDon`: `CapNhatTrangThaiThanhToan(decimal tongDaThanhToan, int? nguoiThucHienId, string? lyDo, DateTime nowUtc)`, dùng chung cho mọi luồng ghi ledger. Khi trạng thái đổi, ghi `LichSuTrangThaiHoaDon` với trạng thái phát hành giữ nguyên. Riêng hủy ghi nhận (§20) luôn ghi một dòng kể cả khi trạng thái không đổi.

### 8.1 Không đổi tổng tiền khi đã có thanh toán

Đã bảo đảm bởi module 1 (xem §2): `TongTien` chỉ đổi khi hóa đơn `Nhap`, thanh toán chỉ có khi `DaGui`. Thêm test giữ quy tắc này. Hóa đơn sai sau khi đã gửi xử lý bằng nghiệp vụ hủy/lập lại của module 1, và chỉ hủy được khi chưa có thanh toán.

---

## 9. Quy tắc số tiền

### 9.1 VND nguyên

- Mọi số tiền trong module là **số nguyên VND > 0**. Cột vẫn là `decimal`; Application kiểm tra `x == decimal.Truncate(x)`.
- Áp dụng cho: số tiền lượt, số thực nhận, số thu thủ công, mức tối thiểu trả một phần.
- `TongTien` của hóa đơn mới là số nguyên nhờ §31.1. Hóa đơn đã gửi có `TongTien` lẻ (chỉ có trong dữ liệu dev) bị chặn tạo lượt với thông báo "Hóa đơn có số tiền lẻ, cần hủy và lập lại"; không viết script sửa dữ liệu.

### 9.2 Số còn lại

```text
SoTienConLai = TongTien − tổng SoTienThanhToan của LichSuThanhToan chưa xóa mềm
```

Luôn tính từ ledger trong transaction, không lấy từ `TrangThaiHoaDon`.

### 9.3 Chính sách trả một phần

`ChoPhepThanhToanMotPhan` chỉ quyết định khách có được **tự tạo lượt nhỏ hơn số còn lại** hay không. Nó không được dùng để từ chối ghi nhận tiền đã thực nhận (§15, §18).

---

## 10. Tạo lượt

Trong transaction, khóa dòng `HoaDon`, áp dụng §6.3, rồi kiểm tra:

1. Hóa đơn thuộc khách, `DaGui`, chưa xóa, chưa `DaThanhToan`, `TongTien` nguyên.
2. Không có lượt đang hoạt động.
3. Số còn lại > 0.
4. Khách không nhập số tiền → lượt lấy đúng số còn lại.
5. Số tiền nhỏ hơn số còn lại chỉ hợp lệ khi `ChoPhepThanhToanMotPhan = true` và số tiền ≥ `SoTienThanhToanToiThieu`. Nếu số còn lại < mức tối thiểu thì phải trả đúng số còn lại.
6. Số tiền nguyên, > 0, ≤ số còn lại.

### 10.1 Mã lượt

- Dạng `TT{HoaDonId}{6 ký tự ngẫu nhiên}`, bảng ký tự `A–Z` và `2–9` bỏ `O`, `I`, `0`, `1` để dễ đọc, sinh bằng bộ sinh số ngẫu nhiên an toàn.
- Vì mã chứa `HoaDonId` nên chỉ có thể trùng trong cùng một hóa đơn, mà các thao tác trên một hóa đơn đã tuần tự nhờ khóa dòng. Sinh mã, kiểm tra tồn tại bằng truy vấn trong transaction, trùng thì sinh lại (tối đa 3 lần) **trước khi** insert. Không bắt `DbUpdateException` rồi `SaveChanges` lại trong cùng transaction (PostgreSQL đã aborted).
- Unique index `MaYeuCau` là lớp chặn cuối; nếu vẫn vi phạm thì rollback cả thao tác và trả lỗi chung "Vui lòng thử lại".
- `NoiDungChuyenKhoan = MaYeuCau`.

---

## 11. Hủy lượt

Chỉ khách đứng tên hợp đồng, chỉ lượt `ChoThanhToan` (sau §6.3). Không hủy được `DangDoiChieu`. Lượt chuyển `DaHuy`, ghi lịch sử, không xóa.

UI xác nhận trước khi hủy:

> Chỉ hủy nếu bạn **chưa** chuyển khoản. Nếu đã chuyển, hãy nộp minh chứng hoặc liên hệ quản lý.

`DaHuy` chỉ đóng luồng tự phục vụ; tiền vẫn có thể tới sau, nhân viên tra cứu được mã lượt (§21).

---

## 12. Nộp minh chứng

1. Lượt thuộc hóa đơn của khách, đang `ChoThanhToan` và còn hạn (sau §6.3; nếu vừa hết hạn thì áp dụng §6.4).
2. Ảnh jpg, png hoặc webp, tối đa `MinhChungToiDaBytes` (5 MB). Kiểm tra ở Web và lại ở Application: extension, content-type **và magic bytes**.
3. `SoTienKhaiBao` = `SoTien` của lượt; khách không nhập số tiền.
4. `NgayChuyenKhaiBao` bắt buộc (cột không null), là thời điểm khách khai đã chuyển; không được sau `nowUtc + 5 phút`. Web đổi giờ Việt Nam sang UTC; Application vẫn kiểm tra. Đây là dữ liệu khai báo, chỉ để hỗ trợ đối chiếu.
5. `MaGiaoDichNganHang` tùy chọn, cắt khoảng trắng. Nếu có: không trùng `MaGiaoDich` của `LichSuThanhToan` chưa xóa, không trùng mã của minh chứng khác đang `ChoXacNhan`.
6. Kiểm tra trước dưới khóa (lượt, hạn, mã giao dịch), rồi nhả khóa và tải ảnh lên Cloudinary qua `IMeterImageStorageService`, thư mục `payment-proofs`. `FileName` là `MaYeuCau`, nên `PublicId` có dạng `payment-proofs/{MaYeuCau}_{hậu tố Cloudinary}` để dễ truy vết (không đổi interface lưu ảnh). Sau đó mở transaction mới, khóa dòng và kiểm tra lại toàn bộ quy tắc trước khi ghi. Nếu lưu DB thất bại thì xóa ảnh theo `PublicId`; lỗi khi xóa chỉ log Warning kèm `PublicId`.
7. Lượt chuyển `DangDoiChieu`.

UI khi tạo lượt và trên khung QR nhắc: "Quét QR để chuyển, không tự sửa số tiền hoặc nội dung. QR chỉ dùng cho hóa đơn này và không dùng lại sau khi hết hạn."

---

## 13. Nguyên tắc đối chiếu

Người đối chiếu không xác nhận "ảnh hợp lệ" hay "khách khai đúng"; họ xác nhận **khoản tiền thực nhận trên tài khoản**, dựa trên sao kê. Ảnh chỉ giúp tìm giao dịch.

Tiêu chí tối thiểu: mã lượt/nội dung chuyển khoản, số tiền thực nhận, thời gian giao dịch, mã giao dịch nếu có. Tên tài khoản chuyển chỉ là thông tin hỗ trợ; không từ chối chỉ vì tên khác người thuê (người thân, bạn cùng phòng, công ty có thể chuyển hộ).

### 13.1 Form đối chiếu

Modal đối chiếu có các ô người đối chiếu **tự nhập lại** từ sao kê (điền sẵn thông tin khách khai, riêng số tiền để trống):

- `SoTienThucNhan` (bắt buộc, nguyên VND): người đối chiếu gõ lại, không điền sẵn, để buộc phải nhìn sao kê.
- `MaGiaoDich` ngân hàng (điền sẵn mã khách khai, sửa được, có thể để trống).
- `NgayGiaoDich` (điền sẵn `NgayChuyenKhaiBao`, sửa được, giờ Việt Nam).
- `GhiChu` (tùy chọn); trở thành **bắt buộc** khi số thực nhận khác số lượt.

Hai nút: **"Xác nhận"** và **"Từ chối"** (lý do bắt buộc).

### 13.2 Rẽ nhánh khi bấm "Xác nhận"

Service `ConfirmAsync` khóa dòng `HoaDon`, đọc lại số còn lại rồi rẽ nhánh theo dữ liệu trong transaction (không tin số còn lại do client gửi):

| Điều kiện | Kết quả (`KetQuaDoiChieu`) | Ai được làm |
| --- | --- | --- |
| `SoTienThucNhan == SoTien` của lượt | `DaXacNhan` (§14) | NV, Admin |
| `SoTienThucNhan ≠ SoTien` của lượt và `≤ SoTienConLai` | `DaGhiNhanSoThucNhan` (§15) | NV, Admin |
| `SoTienThucNhan > SoTienConLai`, actor là nhân viên | `DaChuyenAdmin` (§16.1) | NV |
| `SoTienThucNhan > SoTienConLai`, actor là Admin | `DaXacNhanCoTienThua` (§16.2) | Admin |

Giao diện hiện trước kết quả dự kiến khi người đối chiếu nhập số (ví dụ "Thực nhận thấp hơn số lượt 100.000đ, hệ thống sẽ ghi nhận đúng 1.900.000đ, hóa đơn còn nợ 100.000đ"), nhưng quyết định cuối nằm ở service.

---

## 14. Xác nhận đúng số tiền

Trong một transaction có khóa dòng `HoaDon`:

1. Kiểm tra `Payment.Review` theo chi nhánh thật của hóa đơn.
2. Minh chứng vẫn `ChoXacNhan`, lượt vẫn `DangDoiChieu`.
3. Đọc lại tổng đã thu; tổng + số tiền ≤ `TongTien`.
4. Mã giao dịch (nếu có) chưa được ghi nhận.
5. Ghi `LichSuThanhToan`:
   - `SoTienThanhToan` = `SoTienThucNhan` (= số lượt);
   - `MaGiaoDich` = mã ngân hàng người đối chiếu nhập; nếu trống thì `MaYeuCau`;
   - `PhuongThucThanhToan` = `ChuyenKhoan`;
   - `NgayThanhToan` = `NgayGiaoDich` người đối chiếu nhập (đổi sang UTC);
   - `NgayXacNhan` = `nowUtc`; `NguoiXacNhanId` = actor;
   - `MinhChungThanhToanHoaDonId`; `GhiChu` = ghi chú người đối chiếu.
6. Minh chứng → `DaXacNhan` (`NguoiDoiChieuId`, `NgayDoiChieu`); lượt → `DaHoanTat` (ghi lịch sử).
7. Hóa đơn tính lại theo §8.

Unique index `MaGiaoDich` và `MinhChungThanhToanHoaDonId` là lớp chặn cuối; vi phạm được Infrastructure đổi thành lỗi nghiệp vụ "Mã giao dịch đã được ghi nhận", transaction rollback.

---

## 15. Ghi nhận số thực nhận (chênh lệch, không vượt nợ)

Dùng khi khách chuyển khác số lượt nhưng không vượt số còn nợ. Ví dụ: lượt 2.000.000, thực nhận 1.900.000; hoặc lượt một phần 1.000.000 mà thực nhận 1.500.000 trong khi còn nợ 2.000.000. Áp dụng **cả khi hóa đơn không cho trả một phần** (§9.3).

Cùng transaction và kiểm tra như §14, khác ở:

- `GhiChu` của người đối chiếu bắt buộc (lý do chênh lệch).
- `LichSuThanhToan.SoTienThanhToan` = `SoTienThucNhan` (không phải số lượt). `GhiChu` ledger có tiền tố cố định:
  `[CHENH-LECH:{thựcNhận − sốLượt}] {ghi chú}` (số có dấu, ví dụ `[CHENH-LECH:-100000]`).
- Minh chứng → `DaXacNhan`; lượt → `DaHoanTat` với lý do lịch sử "Thực nhận {X}, khác số lượt {Y}: {ghi chú}". QR cũ không mở lại.
- Hóa đơn tính lại theo §8 (thường thành `ThanhToanMotPhan`; khách tạo lượt mới cho phần còn lại).
- Nếu hóa đơn **không** cho trả một phần và kết quả còn nợ: gửi thông báo cho các Admin để có người thứ hai biết về khoản lệch.

Không dùng luồng "từ chối rồi thu thủ công" cho trường hợp này, vì từ chối sẽ mở lại QR với số tiền cũ và khách có thể chuyển thêm.

---

## 16. Tiền thừa (thực nhận vượt số còn nợ)

Ví dụ: còn nợ 2.000.000, thực nhận 2.500.000. Hệ thống không có ví, số dư hay hoàn tiền, nên không được ghi 2.000.000 rồi bỏ mất 500.000 khỏi dấu vết.

### 16.1 Nhân viên chuyển Admin

Nhân viên không xử lý được. Khi nhân viên bấm "Xác nhận" với số vượt nợ, service:

- giữ minh chứng `ChoXacNhan` và lượt `DangDoiChieu`;
- ghi một dòng `LichSuTrangThaiYeuCauThanhToanHoaDon` với `TrangThaiCu = TrangThaiMoi = DangDoiChieu`, `NguoiThucHienId` = nhân viên, `LyDo` = `[CHO-ADMIN] Thực nhận {X}, vượt số còn nợ {Y}. {ghi chú}`;
- commit, trả `DaChuyenAdmin`, gửi thông báo cho các Admin.

Một lượt `DangDoiChieu` được coi là "chờ Admin" khi dòng lịch sử mới nhất của nó có `LyDo` bắt đầu bằng `[CHO-ADMIN]`. Hàng đợi hiển thị huy hiệu "Chờ Admin" và có bộ lọc này. Sau khi chuyển, nhân viên không xác nhận hay từ chối được lượt đó nữa; Admin xử lý. Đây là cách thay thế cờ trạng thái mà schema không có.

### 16.2 Admin xác nhận có tiền thừa

Admin nhập số thực nhận vượt nợ, ghi chú bắt buộc. Trong transaction:

- `LichSuThanhToan.SoTienThanhToan` = `SoTienConLai` (đủ nợ, không vượt `TongTien`); `GhiChu` = `[TIEN-THUA:{thựcNhận − cònNợ}] {ghi chú}`.
- Minh chứng → `DaXacNhan`; lượt → `DaHoanTat` với lý do ghi rõ số tiền thừa.
- Hóa đơn → `DaThanhToan`.
- Thông báo cho các Admin.

Phần thừa được Admin xử lý ngoài hệ thống (hoàn lại, trừ vào kỳ sau theo thỏa thuận). Màn "Lịch sử thanh toán" có bộ lọc **"Có tiền thừa"** liệt kê các bản ghi chưa xóa có `GhiChu` bắt đầu bằng `[TIEN-THUA:`, hiển thị số tiền thừa tách từ thẻ.

Admin cũng có thể **từ chối** minh chứng nếu không xác định được giao dịch.

### 16.3 Tiền thừa khi hóa đơn đã trả đủ

Ví dụ khách chuyển hai lần cho một hóa đơn đã `DaThanhToan`: không còn lượt và số còn nợ = 0, nên hệ thống không ghi nhận được. Admin xử lý ngoài hệ thống. Đây là giới hạn (§34).

---

## 17. Từ chối minh chứng

Kiểm tra `Payment.Review` (nhân viên không từ chối được lượt "chờ Admin"), khóa dòng `HoaDon`, minh chứng `ChoXacNhan`, lý do bắt buộc (tối đa 500 ký tự).

- Minh chứng → `TuChoi`; lượt → `ChoThanhToan` với hạn mới (§6.1).
- Lý do thường gặp: không thấy giao dịch trên sao kê; minh chứng không rõ hoặc không đúng giao dịch; mã giao dịch đã được ghi nhận; giao dịch thất bại.
- "Số tiền không khớp" **không** phải lý do từ chối nếu tiền đã vào tài khoản; dùng §15 hoặc §16.

---

## 18. Thu tiền thủ công (`InvoiceLedgerService.CollectManuallyAsync`)

Ghi **số tiền thực nhận**, không mặc định thu số còn lại. Dùng cho tiền mặt, chuyển khoản ngoài luồng (không nộp minh chứng, sai nội dung, dùng QR hết hạn, chuyển sau khi hủy lượt).

Hợp đồng service dùng request DTO `ManualCollectionRequest`:

| Trường | Ghi chú |
| --- | --- |
| `HoaDonId` | |
| `SoTienThucNhan` | Nguyên VND > 0; mặc định trên UI = số còn lại |
| `PhuongThucThanhToan` | `TienMat` hoặc `ChuyenKhoan` |
| `NgayThanhToan` | Giờ Việt Nam trên UI, đổi sang UTC ở Web; không ở tương lai quá 5 phút |
| `MaGiaoDich` | Tùy chọn với `ChuyenKhoan` (nên nhập mã trên sao kê để chống ghi trùng); bỏ trống với `TienMat` |
| `GhiChu` | Tùy chọn; bắt buộc khi chuyển khoản không có mã giao dịch và khi có tiền thừa |

Quy tắc, trong transaction có khóa dòng `HoaDon` và sau §6.3:

1. Quyền `Invoice.Send` theo chi nhánh (như hiện tại); hóa đơn `DaGui`, chưa xóa, chưa `DaThanhToan`, `TongTien` nguyên.
2. Có lượt `DangDoiChieu` → chặn: "Khách đã nộp minh chứng, hãy đối chiếu trước để tránh ghi trùng".
3. Có lượt `ChoThanhToan` còn hạn → chuyển lượt sang `DaHuy`, lý do "Đã thu bằng phương thức khác", người thực hiện = actor.
4. `SoTienThucNhan ≤ SoTienConLai`: ghi đúng số đó. Không phụ thuộc `ChoPhepThanhToanMotPhan`.
5. `SoTienThucNhan > SoTienConLai`: chỉ Admin; ghi `SoTienConLai`, `GhiChu` có thẻ `[TIEN-THUA:{chênh}]` như §16.2. Nhân viên nhận lỗi "Số tiền vượt số còn nợ, cần Admin xử lý".
6. `MaGiaoDich`: chuyển khoản có mã thì dùng mã người nhập (kiểm tra chưa ghi nhận ở ledger và minh chứng chờ); chuyển khoản không có mã thì `GhiChu` bắt buộc và hệ thống sinh `CK-{yyyyMMdd}-{8 ký tự ngẫu nhiên}`; tiền mặt sinh `TM-{yyyyMMdd}-{8 ký tự ngẫu nhiên}`. Ngày trong mã theo giờ Việt Nam.
7. Ghi `LichSuThanhToan` (`MinhChungThanhToanHoaDonId = null`, `NgayXacNhan = nowUtc`, `NguoiXacNhanId` = actor); tính lại hóa đơn theo §8.

UI nút "Thu tiền": hiển thị số còn lại; khóa kèm giải thích khi có lượt `DangDoiChieu`; cảnh báo khi có lượt `ChoThanhToan` rằng lượt sẽ bị hủy.

---

## 19. Cấu hình thanh toán một phần

Chỉ Admin (`Payment.ConfigurePartial`). Khóa dòng `HoaDon`, áp dụng §6.3; hóa đơn `DaGui`, chưa `DaThanhToan`, không có lượt đang hoạt động. Gọi `HoaDon.CauHinhThanhToanMotPhan` có sẵn (bổ sung kiểm tra mức tối thiểu là số nguyên). Tắt thì xóa mức tối thiểu. Ghi `LichSuTrangThaiHoaDon` (trạng thái giữ nguyên) với lý do "Bật thanh toán một phần, tối thiểu {X}" hoặc "Tắt thanh toán một phần" làm dấu vết.

---

## 20. Hủy ghi nhận thanh toán (`InvoiceLedgerService.VoidPaymentAsync`)

Đổi tên trên UI từ "Hủy giao dịch" thành **"Hủy ghi nhận thanh toán"**. Hộp xác nhận cảnh báo: "Thao tác này chỉ thay đổi ghi nhận trong hệ thống, không hoàn tiền qua ngân hàng."

Chỉ Admin; **lý do bắt buộc** (tối đa 500 ký tự). Hợp đồng service: `VoidPaymentAsync(lichSuThanhToanId, actorId, lyDo)`; Web nhận `lyDo` trong body.

Trong transaction:

1. Service tự kiểm tra actor là Admin.
2. Khóa dòng `HoaDon`.
3. Bản ghi chưa xóa mềm; xóa mềm (`IsDeleted = true`) và nối vào `GhiChu`: ` | [DA-HUY {dd/MM/yyyy HH:mm} bởi #{adminId}] {lý do}`.
4. Tính lại tổng các khoản còn lại và trạng thái hóa đơn theo §8.
5. **Luôn** ghi một dòng `LichSuTrangThaiHoaDon` (kể cả khi trạng thái không đổi): `NguoiThucHienId` = Admin, `NgayThucHien` = `nowUtc`, `LyDo` = `Hủy ghi nhận thanh toán {MaGiaoDich} ({SoTien}): {lý do}`. Đây là nơi lưu chính thức người, thời điểm, lý do hủy, thay cho cột mà `LichSuThanhToan` không có.

Minh chứng và lượt liên quan giữ `DaXacNhan`/`DaHoanTat` làm dấu vết, không mở lại. UI suy ra nhãn "Đã xác nhận, sau đó bị hủy ghi nhận" khi ledger gắn minh chứng đã xóa mềm. Khách muốn trả lại phần bị hủy thì tạo lượt mới. Vì unique `MinhChungThanhToanHoaDonId` không lọc `IsDeleted`, minh chứng cũ không gắn lại được với ledger mới; ghi nhận đúng làm qua lượt mới hoặc thu thủ công.

Payment đã xác nhận không sửa trực tiếp: sai thì hủy ghi nhận (kèm lý do) rồi tạo ghi nhận mới đúng.

---

## 21. Tra cứu lượt theo mã

Màn "Đối chiếu thanh toán" có ô tìm `MaYeuCau` (khớp chính xác, không phân biệt hoa thường, bỏ khoảng trắng). Trả lượt ở **mọi** trạng thái (`ChoThanhToan`, `DangDoiChieu`, `HetHan`, `DaHuy`, `DaHoanTat`), kèm hóa đơn, phòng, khách, số còn nợ hiện tại, lịch sử trạng thái lượt và các minh chứng. Nhân viên chỉ thấy lượt thuộc chi nhánh được phân công; ngoài phạm vi thì trả "Không tìm thấy".

Từ kết quả, người dùng mở được: đối chiếu minh chứng (nếu đang chờ) hoặc thu tiền thủ công cho hóa đơn đó (§18), dùng cho tiền về trễ, QR cũ, chuyển sau khi hủy lượt.

---

## 22. Bảng tình huống vận hành

| Tình huống | Xử lý |
| --- | --- |
| Trả đúng QR, đúng tiền | Xác nhận (§14) |
| Người thân chuyển hộ | Xác nhận nếu khớp sao kê |
| Trả một phần đúng chính sách | Xác nhận; hóa đơn `ThanhToanMotPhan` |
| Chuyển thiếu so với lượt | Ghi nhận số thực nhận (§15) |
| Không cho trả một phần nhưng khách chuyển thiếu | Ghi nhận số thực nhận (§15) + thông báo Admin |
| Chuyển nhiều hơn lượt nhưng không vượt nợ | Ghi nhận số thực nhận (§15) |
| Chuyển vượt số còn nợ | NV chuyển Admin; Admin ghi đủ nợ + thẻ `[TIEN-THUA]` (§16) |
| Chuyển hai lần khi hóa đơn đã trả đủ | Ngoài hệ thống (§16.3) |
| Chuyển sai nội dung, xác minh chắc chắn | Thu thủ công với mã giao dịch thật và ghi chú (§18) |
| Chuyển khoản nhưng không có mã trên sao kê | Thu thủ công, để trống mã, ghi chú bắt buộc; hệ thống sinh `CK-...` (§18) |
| Chuyển nhưng không nộp minh chứng | Thu thủ công (§18) |
| Nộp minh chứng nhưng không thấy tiền | Từ chối (§17) |
| Mã giao dịch hoặc minh chứng dùng lại | Chặn (§12, §14) |
| Dùng QR hết hạn, hoặc chuyển rồi hủy lượt | Tra mã lượt (§21) + thu thủ công |
| Nộp tiền mặt khi có lượt `ChoThanhToan` | Thu thủ công, lượt tự `DaHuy` (§18) |
| Muốn thu thủ công khi có lượt `DangDoiChieu` | Chặn; đối chiếu trước |
| Nhân viên xác nhận nhầm, hoặc ngân hàng hoàn giao dịch | Admin hủy ghi nhận + lý do, tạo ghi nhận đúng nếu cần (§20) |
| Khách đã trả phòng còn hóa đơn | Vẫn thanh toán được (§4.1) |
| Hóa đơn đã có thanh toán muốn sửa tổng | Không thể (§8.1) |
| Một lần chuyển trả nhiều hóa đơn | Thu thủ công từng hóa đơn; chỉ một bản ghi mang mã ngân hàng thật, các bản ghi còn lại để trống mã (sinh `CK-...`) và ghi chú tham chiếu mã ngân hàng |
| Hủy hóa đơn khi có lượt | §31.2 |

---

## 23. Application

Service trả `ServiceResult` / `ServiceResult<T>` như `InvoiceIssuanceService`; lỗi nghiệp vụ không log Error. DTO, enum kết quả và nhãn trạng thái tiếng Việt nằm ở Application để Web không tham chiếu Domain. Thời gian lấy qua `TimeProvider` (inject) để test được hết hạn.

### 23.1 `InvoicePaymentRequestService` (mới, phía khách)

| Hàm | Mô tả |
| --- | --- |
| `GetPaymentPanelAsync(hoaDonId, nguoiThueId)` | Tổng, đã thu, còn lại, cấu hình một phần, lượt đang hoạt động hoặc lượt gần nhất với effective status, minh chứng kèm lý do từ chối. Chỉ đọc |
| `CreateAsync(hoaDonId, nguoiThueId, soTien?)` | §10 |
| `CancelAsync(yeuCauId, nguoiThueId)` | §11 |
| `SubmitProofAsync(request, nguoiThueId)` | §12; request gồm `YeuCauId`, `UploadFile`, `MaGiaoDich?`, `NgayChuyenUtc` |
| `GetQrAsync(yeuCauId, nguoiThueId)` | PNG VietQR theo số tiền và nội dung của lượt `ChoThanhToan` còn hạn, dùng `IVietQRService` và cấu hình VietQR có sẵn |
| `GetProofImageAsync(minhChungId, nguoiThueId)` | Ảnh qua `IMeterImageStorageService.ReadAsync` |

### 23.2 `InvoicePaymentConfirmationService` (sửa, phía quản lý)

| Hàm | Mô tả |
| --- | --- |
| `GetReviewQueueAsync(actorId, filter)` | Lọc trạng thái minh chứng (mặc định `ChoXacNhan`), chi nhánh, "chờ Admin"; phân trang DataTable; nhân viên chỉ thấy chi nhánh trong `EmployeeAccessScope` |
| `GetProofDetailAsync(minhChungId, actorId)` | Lượt, hóa đơn (tổng, đã thu, còn lại), phòng, khách, lịch sử lượt |
| `GetProofImageAsync(minhChungId, actorId)` | Ảnh qua server, kiểm tra `Payment.Review` |
| `ConfirmAsync(request, actorId)` | §13.2, §14–§16; request gồm `MinhChungId`, `SoTienThucNhan`, `MaGiaoDich?`, `NgayGiaoDichUtc`, `GhiChu?`; trả `KetQuaDoiChieu` |
| `RejectAsync(minhChungId, actorId, lyDo)` | §17 |
| `ConfigurePartialPaymentAsync(hoaDonId, actorId, choPhep, soTienToiThieu?)` | §19 |
| `SearchPaymentRequestAsync(maYeuCau, actorId)` | §21 |

Bỏ `TaoYeuCauThanhToanAsync` và `NopMinhChungAsync` cũ; test cũ viết lại theo hợp đồng mới.

### 23.3 `InvoiceLedgerService` (mới) và service hiện có

- `InvoiceLedgerService.CollectManuallyAsync`: §18 (thay `HoaDonService.ThuTienAsync`, đã bỏ).
- `InvoiceLedgerService.VoidPaymentAsync(id, adminUserId, lyDo)`: §20 (thay `LichSuThanhToanService.HuyGiaoDichAsync`, đã bỏ).
- `InvoiceLedgerService.CancelInvoiceAsync`: §31.2 (thay `HoaDonService.DeleteHoaDonAsync`, đã bỏ).
- `HoaDonCalculatorService` và các chỗ tính dòng chi tiết: §31.1.
- Mọi luồng ghi đi qua `InvoicePaymentTransaction` (khóa dòng, chuyển `HetHan`, đọc lại tổng ledger, commit, gửi thông báo sau commit; §28).

### 23.4 Thành phần dùng chung

- `InvoicePaymentOptions` (`Application/Common/Configurations`, mới): `YeuCauHetHanSauGio = 24`, `MinhChungToiDaBytes = 5 MB`, `DungSaiNgayTuongLaiPhut = 5`. Bind section `InvoicePayment`, có giá trị mặc định khi thiếu.
- `IInvoicePaymentStore` bổ sung: `GetHoaDonForUpdateAsync` (`FOR UPDATE`, theo mẫu `HoaDonStore`), `GetActiveYeuCauAsync` (điều kiện §5.1), `GetYeuCauByCodeAsync` (mọi trạng thái), `ExistsMaYeuCauAsync`, `GetTongTienDaThanhToanHoaDonAsync` (chỉ bản chưa xóa), `ExistsMaGiaoDichAsync`, `ExistsPendingProofMaGiaoDichAsync`, `GetMinhChungForReviewAsync`, `GetReviewQueueAsync`, `GetHoaDonBranchIdByMinhChungAsync`, `GetPaymentPanelAsync`.
- Infrastructure bắt `DbUpdateException` do unique violation (`PostgresException.SqlState == 23505`) và trả lỗi nghiệp vụ theo tên index; transaction được rollback, không `SaveChanges` lại.

---

## 24. Thông báo

Qua `IThongBaoService`/`IThongBaoNotifier` có sẵn, gửi **sau khi commit**; lỗi gửi chỉ log Warning, không rollback. DB và UI là nguồn sự thật.

| Sự kiện | Người nhận |
| --- | --- |
| Khách nộp minh chứng | Nhân viên đang được phân công chi nhánh của hóa đơn + Admin |
| Xác nhận, ghi nhận số thực nhận, xác nhận có tiền thừa, từ chối | Tài khoản người thuê đứng tên hợp đồng |
| Nhân viên chuyển Admin (§16.1) | Admin |
| Ghi nhận số thực nhận khi hóa đơn không cho trả một phần (§15) | Admin |
| Ghi nhận có tiền thừa (§16.2, §18) | Admin |

---

## 25. Thời gian và múi giờ

- Lưu UTC; hiển thị giờ Việt Nam; Web đổi giờ nhập vào sang UTC.
- Thời điểm hệ thống (`NgayTao` minh chứng, `NgayXacNhan`, lịch sử, hạn lượt) dùng đồng hồ server qua `TimeProvider`.
- `NgayChuyenKhaiBao` là dữ liệu khách khai; `NgayThanhToan` của ledger là ngày giao dịch người đối chiếu xác định từ sao kê.
- Hết hạn luôn tính bằng `nowUtc` của server, không dùng đồng hồ client.

---

## 26. Lưu ảnh

- jpg/png/webp, tối đa 5 MB, kiểm tra extension, content-type và magic bytes ở cả Web và Application.
- Tải Cloudinary sau bước kiểm tra trước và ngoài khóa dòng; DB lỗi hoặc kiểm tra lại thất bại thì xóa theo `PublicId`, xóa lỗi chỉ log Warning kèm `PublicId`.
- `PublicId` có dạng `payment-proofs/{MaYeuCau}_{hậu tố}` (Cloudinary sinh hậu tố từ `FileName` = `MaYeuCau`).
- Ảnh mồ côi có thể tồn tại do dọn dẹp chỉ là best-effort; không làm job dọn trong phạm vi này. Thư mục riêng `payment-proofs` và `PublicId` có mã lượt giúp dọn thủ công.
- `IMeterImageStorageService` tạm dùng cho ảnh minh chứng; đổi thành interface lưu ảnh chung là việc sau.

---

## 27. Giao diện MVC

Controller chỉ đọc id người dùng từ claim, gọi service và map `ServiceResult` sang JSON/HTTP (400 lỗi nghiệp vụ và validation, 403 không có quyền, 404 không tìm thấy). POST/AJAX dùng antiforgery header `RequestVerificationToken`. Giao diện theo Tabler như module 1.

### 27.1 Cổng khách thuê (`Areas/KhachThue`)

Trong modal chi tiết hóa đơn (`Views/HoaDon/Index.cshtml`), thay khối QR cũ bằng khung **"Thanh toán"**:

| Tình huống | Hiển thị |
| --- | --- |
| Chưa có lượt hoạt động, còn nợ | Đã thu, còn lại, nút "Thanh toán". Nếu được trả một phần: ô số tiền mặc định bằng số còn lại, ghi rõ mức tối thiểu |
| Lượt `ChoThanhToan` | QR, số tiền, nội dung chuyển khoản (nút sao chép), tài khoản nhận, đếm ngược hạn, nhắc "quét QR, không sửa số tiền"; form nộp minh chứng (ảnh, mã giao dịch tùy chọn, thời điểm chuyển); nút "Hủy lượt này" với cảnh báo §11; lý do từ chối của minh chứng trước nếu có |
| Lượt `DangDoiChieu` | "Đang chờ nhân viên đối chiếu", ảnh minh chứng đã nộp; không có nút thao tác |
| Lượt vừa hết hạn | "Lượt đã hết hạn, đừng dùng lại QR cũ", nút "Thanh toán" tạo lượt mới |
| Đã thanh toán đủ | Không có khung thanh toán; bảng lịch sử thanh toán |

Endpoint trong `KhachThue/HoaDonController`:

| Endpoint | Mục đích |
| --- | --- |
| `GET ThanhToan/{hoaDonId}` | Dữ liệu khung thanh toán |
| `POST ThanhToan/Tao` | Tạo lượt |
| `POST ThanhToan/Huy/{yeuCauId}` | Hủy lượt |
| `POST ThanhToan/NopMinhChung` | Multipart; map `IFormFile` sang `UploadFile` |
| `GET ThanhToan/QR/{yeuCauId}` | Ảnh QR của lượt |
| `GET ThanhToan/AnhMinhChung/{minhChungId}` | Ảnh minh chứng |

Bỏ endpoint cũ `GET /KhachThue/HoaDon/GetVietQR`.

### 27.2 Cổng quản lý (`Areas/QuanLyNhaTro`)

- Màn mới **"Đối chiếu thanh toán"** (`DoiChieuThanhToanController`, mục menu cạnh "Lịch sử thanh toán", nhân viên và Admin):
  - Ô tra cứu mã lượt (§21).
  - DataTable: mã lượt, mã hóa đơn, phòng, chi nhánh, khách, số tiền lượt, mã giao dịch khai báo, ngày khách khai chuyển, ngày nộp, huy hiệu "Chờ Admin". Lọc chi nhánh và "Chờ Admin"; tab "Chờ đối chiếu" (mặc định) và "Đã xử lý".
  - Modal đối chiếu: ảnh minh chứng bên trái (phóng to bằng `anh-chi-so-viewer.js`); bên phải là thông tin lượt, hóa đơn (tổng, đã thu, còn lại), thông tin khách khai, form §13.1 với dòng xem trước kết quả; nút "Xác nhận" và "Từ chối".
  - Endpoint: `GET Index`, `GET DanhSach`, `GET ChiTiet/{minhChungId}`, `GET AnhMinhChung/{minhChungId}`, `POST XacNhan`, `POST TuChoi`, `GET TraCuu?ma=`.
- Modal chi tiết hóa đơn ở `HoaDon`: với Admin, công tắc "Cho phép trả một phần" và ô mức tối thiểu khi hóa đơn đã gửi và chưa trả đủ; khóa kèm giải thích khi có lượt hoạt động. Endpoint `POST /HoaDon/CauHinhThanhToanMotPhan/{id}`.
- Nút "Thu tiền": form §18 (số tiền, phương thức, ngày, mã giao dịch, ghi chú). Endpoint `POST /HoaDon/ThuTien` nhận DTO mới. Endpoint `GET /HoaDon/GetVietQR` của quản lý giữ nguyên nhưng dùng số còn lại.
- `LichSuThanhToan`: đổi nhãn thành "Hủy ghi nhận thanh toán", thêm ô lý do bắt buộc và cảnh báo §20; thêm bộ lọc "Có tiền thừa" (§16.2) và hiển thị thẻ `[CHENH-LECH]`, `[TIEN-THUA]`, `[DA-HUY]` thành nhãn dễ đọc.

---

## 28. Transaction và đồng thời

Mọi thao tác ghi có thể đổi lượt, minh chứng hoặc công nợ của một hóa đơn đều: bắt đầu transaction → `SELECT ... FOR UPDATE` dòng `HoaDon` → đọc lại ledger và lượt trong transaction → kiểm tra quy tắc với dữ liệu mới nhất → ghi → commit.

Áp dụng cho: tạo lượt, hủy lượt, nộp minh chứng, xác nhận/ghi nhận số thực nhận/chuyển Admin, từ chối, cấu hình một phần, thu thủ công, hủy ghi nhận, hủy hóa đơn.

Riêng nộp minh chứng: tải ảnh trước, rồi mới mở transaction (không giữ khóa dòng trong lúc gọi Cloudinary).

---

## 29. Xử lý lỗi

| Loại | Xử lý |
| --- | --- |
| Lỗi nghiệp vụ, validation | `ServiceResult` thất bại, thông báo tiếng Việt, 400; không log Error |
| Không có quyền | 403 |
| Không tìm thấy hoặc ngoài phạm vi | 404 |
| Thao tác đồng thời | Khóa dòng tuần tự hóa; thao tác sau thấy trạng thái mới và nhận lỗi nghiệp vụ |
| Vi phạm unique index | Infrastructure đổi thành lỗi nghiệp vụ; rollback; không retry trong cùng transaction |
| Lỗi nghiệp vụ có thay đổi trạng thái cần lưu (§6.4, §16.1) | Commit thay đổi đó rồi mới trả kết quả |
| Tải ảnh lỗi | Trả lỗi |
| DB lỗi sau khi tải ảnh | Rollback, xóa ảnh best-effort, Warning nếu xóa lỗi |
| Gửi thông báo lỗi | Warning, không rollback |
| Tổng ledger vượt `TongTien` khi tính lại | Rollback, log Error |
| Lỗi bất ngờ | Rollback, log Error, thông báo chung |

---

## 30. Dấu vết

| Hành động | Nơi ghi |
| --- | --- |
| Tạo, hủy, hết hạn, nộp minh chứng, từ chối, hoàn tất lượt | `LichSuTrangThaiYeuCauThanhToanHoaDon` |
| Chuyển Admin | `LichSuTrangThaiYeuCauThanhToanHoaDon` (dòng tự chuyển, tiền tố `[CHO-ADMIN]`) |
| Từ chối minh chứng | `MinhChungThanhToanHoaDon.LyDoTuChoi`, `NguoiDoiChieuId`, `NgayDoiChieu` |
| Xác nhận, ghi nhận chênh lệch, tiền thừa, thu thủ công | `LichSuThanhToan` (+ thẻ trong `GhiChu`) và `LichSuTrangThaiHoaDon` khi trạng thái đổi |
| Lượt tự hủy do thu thủ công hoặc hủy hóa đơn | `LichSuTrangThaiYeuCauThanhToanHoaDon` với người thực hiện |
| Hủy ghi nhận thanh toán | `LichSuTrangThaiHoaDon` (luôn ghi) + `GhiChu` của ledger |
| Đổi cấu hình trả một phần | `LichSuTrangThaiHoaDon` (trạng thái giữ nguyên) |

---

## 31. Sửa đổi ở module 1

### 31.1 Làm tròn tiền hóa đơn về đồng nguyên

- `HoaDonCalculatorService`: tiền phòng, tiền điện nước, tiền dịch vụ cố định làm tròn `decimal.Round(x, 0, MidpointRounding.AwayFromZero)` thay cho 2 chữ số. Số lượng tiêu thụ giữ 3 chữ số như hiện tại.
- `HoaDonService` (sửa chi tiết hóa đơn nháp): `TongTien` dòng = làm tròn `DonGia × SoLuong` về 0 chữ số; đơn giá nhập phải nguyên.
- `InvoiceIncidentLines` và nơi nhập `ChiPhiSuaChua`: chi phí sự cố phải nguyên.
- `HoaDon.TongTien` = tổng các dòng, vì vậy luôn nguyên.
- Chỉ áp dụng cho hóa đơn tạo hoặc tính lại sau thay đổi; dữ liệu cũ là dữ liệu dev, không viết script sửa (§9.1).
- Cập nhật test calculator hiện có theo quy tắc mới.

### 31.2 Hủy hóa đơn khi có lượt

Trong service hủy hóa đơn (`HoaDonService`, gọi `HoaDon.HuyHoaDon`), trong transaction có khóa dòng và sau §6.3:

- Có lượt `DangDoiChieu` → chặn: "Khách đã nộp minh chứng thanh toán, cần đối chiếu trước khi hủy hóa đơn".
- Có lượt `ChoThanhToan` còn hạn → chuyển lượt sang `DaHuy`, lý do "Hóa đơn bị hủy: {lý do}", rồi hủy hóa đơn.
- Quy tắc cũ giữ nguyên: hóa đơn có ghi nhận thanh toán chưa xóa thì không hủy được.

---

## 32. Kiểm thử

- **Domain.UnitTests**: `HoaDon.CapNhatTrangThaiThanhToan` cho tổng 0, giữa, bằng, vượt `TongTien`, âm, và việc ghi `LichSuTrangThaiHoaDon`; `CauHinhThanhToanMotPhan` với mức tối thiểu lẻ, ≤ 0, > tổng; `HuyHoaDon` giữ hành vi cũ.
- **Application.UnitTests** (fake store/UoW trong `Fakes/`, `FakeTimeProvider`):
  - `HoaDonCalculatorService`: kết quả là số nguyên ở mọi trường hợp chia theo ngày.
  - `InvoicePaymentRequestServiceTests`: tạo lượt đủ; một phần hợp lệ; dưới mức tối thiểu; số còn lại nhỏ hơn mức tối thiểu; một phần chưa bật; số tiền lẻ; `TongTien` lẻ bị chặn; đã có lượt hoạt động; hóa đơn chưa gửi; không thuộc khách; khách đã thanh lý hợp đồng vẫn tạo được; lượt quá hạn chuyển `HetHan` khi tạo lượt mới; mã lượt trùng thì sinh lại; hủy đúng/sai trạng thái; nộp minh chứng hợp lệ; trùng mã giao dịch (ledger và minh chứng chờ); lượt vừa hết hạn trả lỗi **và** `HetHan` được commit; ngày khai ở tương lai; ảnh sai định dạng hoặc magic bytes; ảnh bị xóa khi lưu thất bại.
  - `InvoicePaymentConfirmationTests` (viết lại): xác nhận đúng số ra `ThanhToanMotPhan`/`DaThanhToan`; ledger dùng số, mã, ngày người đối chiếu nhập; mã trống thì dùng `MaYeuCau`; chặn xác nhận lần hai; ghi nhận số thực nhận thấp hơn và cao hơn số lượt (≤ nợ) kể cả khi tắt trả một phần; ghi chú bắt buộc khi chênh lệch; thông báo Admin khi tắt trả một phần; nhân viên nhập vượt nợ thì trả `DaChuyenAdmin`, lượt giữ `DangDoiChieu`, có dòng `[CHO-ADMIN]` được commit; nhân viên không xác nhận hay từ chối được lượt chờ Admin; Admin xác nhận có tiền thừa ghi đủ nợ kèm thẻ; từ chối đưa lượt về chờ với hạn mới; lý do rỗng hoặc quá 500 ký tự; quyền đúng chi nhánh, sai chi nhánh, Admin; cấu hình một phần với Admin, nhân viên, khi có lượt hoạt động; tra cứu mã trả lượt `HetHan`/`DaHuy`, nhân viên khác chi nhánh không thấy.
  - `InvoiceLedgerServiceTests` (thu thủ công): thu đủ số còn lại; thu một phần khi tắt trả một phần; có lượt `ChoThanhToan` thì lượt thành `DaHuy` rồi thu; có lượt `DangDoiChieu` thì chặn; chuyển khoản thiếu mã và thiếu ghi chú thì chặn; chuyển khoản thiếu mã có ghi chú thì sinh `CK-...`; mã trùng thì chặn; nhân viên vượt nợ bị chặn; Admin vượt nợ ghi đủ nợ kèm thẻ; hai lần thu tiền mặt liền nhau không trùng mã.
  - `InvoiceLedgerServiceTests` (hủy ghi nhận): còn khoản khác ra `ThanhToanMotPhan`; không còn ra `ChuaThanhToan`; luôn ghi `LichSuTrangThaiHoaDon` có lý do; nhân viên bị chặn; lý do rỗng bị chặn; minh chứng và lượt giữ nguyên.
  - `InvoiceLedgerServiceTests` (hủy hóa đơn): chặn khi có lượt `DangDoiChieu`; tự hủy lượt `ChoThanhToan`.
  - `ArchitectureBoundaryTests` vẫn pass.
- **Infrastructure.IntegrationTests** (cần DB test): các query mới của `InvoicePaymentStore` (điều kiện effective active, tra cứu theo mã, lọc chờ Admin); hai luồng xác nhận đồng thời có tổng vượt hóa đơn thì chỉ một thành công; hai luồng tạo lượt đồng thời chỉ tạo một lượt; cùng `MaGiaoDich` ở hai hóa đơn chỉ một bản ghi thành công; vi phạm unique được đổi thành lỗi nghiệp vụ và transaction không bị dùng lại; trạng thái `HetHan` được lưu khi thao tác thất bại nghiệp vụ (`InvoicePaymentConcurrencyTests`, thay cho `HoaDonRowLockConcurrencyTests`).
- **Web.IntegrationTests** (cần DB test): khách đi trọn luồng tạo lượt → QR → nộp minh chứng (storage giả) → xem trạng thái; khách khác không xem/thao tác được hóa đơn và ảnh; nhân viên chi nhánh khác nhận 403; Admin xác nhận được; endpoint cấu hình một phần trả 403 với nhân viên; tra cứu trả lượt `HetHan`/`DaHuy`; khung thanh toán hiển thị đúng lượt quá hạn; POST thiếu antiforgery bị từ chối.

---

## 33. Điều kiện nghiệm thu

1. Khách thanh toán đủ qua một lượt: QR đúng số tiền và nội dung; nộp minh chứng; nhân viên xác nhận; hóa đơn `DaThanhToan`; ledger gắn minh chứng.
2. Admin bật trả một phần; khách trả hai lượt; sau lượt đầu `ThanhToanMotPhan`, sau lượt hai `DaThanhToan`.
3. Minh chứng bị từ chối thì khách thấy lý do và nộp lại trên cùng lượt.
4. Lượt bỏ dở quá hạn không chặn tạo lượt mới.
5. Hai nhân viên xác nhận đồng thời không làm tổng vượt `TongTien`; một mã giao dịch ngân hàng không ghi nhận được hai lần.
6. Nhân viên không thấy và không thao tác được dữ liệu chi nhánh không được phân công.
7. Hủy ghi nhận một khoản khi còn khoản khác đưa hóa đơn về `ThanhToanMotPhan`; người, thời điểm và lý do hủy tra được.
8. Lượt `ChoThanhToan` không cản thu thủ công và được hủy có dấu vết; lượt `DangDoiChieu` cản thu thủ công.
9. Chuyển thiếu được ghi đúng số thực nhận, không ghi theo số lượt, kể cả khi tắt trả một phần.
10. Chuyển thừa không mất dấu: hóa đơn đủ nợ, bản ghi có thẻ tiền thừa, Admin lọc ra được.
11. Lượt `HetHan`/`DaHuy` vẫn tìm được bằng mã.
12. Khách đã rời phòng vẫn thanh toán được hóa đơn của mình.
13. Hóa đơn mới có `TongTien` là số nguyên; QR hiển thị đúng số còn lại.
14. Hóa đơn có lượt chờ đối chiếu không hủy được.
15. Không có migration mới; `git status` không có thay đổi trong `Migrations/`.

---

## 34. Giới hạn chấp nhận

- Nhân viên đối chiếu bằng mắt với sao kê; không đọc sao kê hay nhận webhook. Hàng đợi có thể dồn vào đầu tháng.
- Mỗi hóa đơn chỉ một lượt hoạt động; chỉ người đứng tên hợp đồng thanh toán.
- Lượt quá hạn chỉ được ghi `HetHan` khi có thao tác ghi kế tiếp; dữ liệu thô có thể còn lượt `ChoThanhToan` đã quá hạn.
- Không có ví, số dư hay hoàn tiền: tiền thừa chỉ được đánh dấu bằng thẻ trong `GhiChu`, xử lý ngoài hệ thống; tiền chuyển cho hóa đơn đã trả đủ không ghi nhận được.
- Trạng thái "chờ Admin", lý do hủy ghi nhận và các thẻ chênh lệch được biểu diễn bằng dòng lịch sử và tiền tố văn bản thay cho cột riêng; báo cáo dựa trên các tiền tố này.
- Không tự phân bổ một giao dịch cho nhiều hóa đơn.
- Dọn ảnh mồ côi chỉ là best-effort.
- `IMeterImageStorageService` dùng tạm cho ảnh minh chứng.

---

## 35. Hướng nâng cấp

Theo thứ tự ưu tiên: (1) đọc sao kê/webhook ngân hàng để tự khớp theo `MaYeuCau` và số tiền; (2) tách `KhoanTienNhan` và `PhanBoThanhToan` để quản lý tiền thừa và một giao dịch cho nhiều hóa đơn, thay các thẻ văn bản bằng cột; (3) outbox cho thông báo; (4) dọn ảnh Cloudinary định kỳ; (5) màn đối soát và báo cáo ngoại lệ; (6) ví hoặc quy trình hoàn tiền.

---

## 36. Quyết định đã chốt (02/10/2026)

| # | Vấn đề | Quyết định |
| --- | --- | --- |
| 1 | `TongTien` lẻ, VietQR chỉ nhận số nguyên | Làm tròn về đồng nguyên ở module 1 (§31.1); module 2 chỉ nhận số nguyên |
| 2 | Chuyển thừa | Admin ghi đủ nợ, thẻ `[TIEN-THUA]` trong `GhiChu`, thông báo Admin, có bộ lọc (§16) |
| 3 | Chuyển thiếu hoặc khác số lượt | Ghi nhận số thực nhận trong một transaction, không mở lại QR (§15) |
| 4 | Hạn lượt | 24 giờ, cấu hình được |
| a | Form đối chiếu | Người đối chiếu tự gõ số thực nhận, sửa mã và ngày giao dịch; service rẽ nhánh (§13) |
| b | Phân quyền chênh lệch | Nhân viên ghi nhận chênh lệch ≤ nợ; tiền thừa chỉ Admin, nhân viên chuyển Admin |
| c | Hủy hóa đơn khi có lượt | Chặn khi `DangDoiChieu`; tự hủy lượt `ChoThanhToan` (§31.2) |
| 5 | Mã giao dịch khi thu thủ công bằng chuyển khoản (03/10/2026) | Tùy chọn; thiếu mã thì ghi chú bắt buộc, sinh `CK-...` (§18, §22) |
| 6 | `PublicId` ảnh minh chứng (03/10/2026) | `payment-proofs/{MaYeuCau}_{hậu tố}`, không đổi `IMeterImageStorageService` (§12, §26) |
| 7 | Loại lỗi (03/10/2026) | `ServiceResult` thêm `ServiceErrorKind` (Validation/Forbidden/NotFound), Web map 400/403/404 (§29) |
| 8 | Màn công nợ ngoài module (03/10/2026) | Đổi lọc `== ChuaThanhToan` sang `!= DaThanhToan` và hiển thị số còn lại để hóa đơn trả một phần không biến mất |
| d | Dấu vết hủy ghi nhận | Luôn ghi `LichSuTrangThaiHoaDon` kèm lý do (§20) |
| e | Dữ liệu `TongTien` lẻ cũ | Chỉ là dữ liệu dev; không sửa, chặn tạo lượt cho hóa đơn lẻ |
| — | Chính sách tắt trả một phần | Chỉ cố định số tiền lượt và QR; không chặn ghi nhận tiền thực nhận (§1.2) |
