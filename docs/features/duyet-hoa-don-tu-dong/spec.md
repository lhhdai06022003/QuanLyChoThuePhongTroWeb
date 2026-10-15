# Spec: Kiểm tra chỉ số theo hợp đồng và duyệt hóa đơn trước khi gửi

- **Trạng thái:** v4, người dùng đã duyệt (12/10/2026); đã code (13/10/2026).
- **Nhánh:** `feature/duyet-hoa-don-tu-dong` (từ `master` = `c11946c`).
- **Thay thế** trong [spec hóa đơn tự động](../hoa-don-tu-dong/spec.md): mục 4.1 điều kiện 3, 5.2 bước 4–5, 5.3, 6, 7.1, 7.2, `HD4`.
- **Schema:** không đổi.

## 1. Chức năng

1. Sau khi AI đọc chỉ số, code so lượng tiêu thụ với lịch sử **trong cùng hợp đồng** để phát hiện bất thường.
2. Chỉ số hợp lý thì hệ thống tự tạo hóa đơn **nháp**. Hóa đơn tự tạo và hóa đơn lập tay đi chung một luồng: nhân viên gửi duyệt → Admin chốt → nhân viên hoặc Admin công bố. Khách chỉ thấy hóa đơn sau khi công bố.
3. Trước khi công bố vẫn sửa được hóa đơn và chỉ số.
4. Hóa đơn công bố kèm **hạn phản hồi** (mốc để khách liên hệ nếu thấy sai). Khách **thanh toán được ngay**.

## 2. Kiểm tra chỉ số bất thường

AI chỉ đọc số; prompt không chứa lịch sử. Phần so sánh là một chính sách thuần ở Application (ví dụ `MeterAnomalyPolicy`), dùng chung cho chuỗi tự động và màn hình nhân viên.

### 2.1 Lịch sử

Lấy tối đa **6** kỳ gần nhất của phòng thỏa:
- `DaDuyet`, chưa xóa, trước kỳ đang xét;
- **trọn tháng trong hợp đồng hiện hành**: hợp đồng bắt đầu ≤ 00:00 ngày 1 và kết thúc (nếu có) ≥ 23:59:59 ngày cuối tháng, tính theo giờ Việt Nam.

Lọc theo khoảng thời gian của hợp đồng, không cần cột mới.

### 2.2 Quy tắc

Gọi **M** là trung vị tiêu thụ (mới − cũ) của các kỳ lịch sử, tính riêng điện và nước.

| Lý do | Điều kiện |
|---|---|
| `ChiSoGiam` (có sẵn) | mới < cũ |
| `NghiSaiChuSo` (mới) | cũ ≥ 100 và mới ≥ cũ × 9 |
| `TieuThuBatThuong` (đủ ≥ 2 kỳ) | tiêu thụ > M × 2 **và** tiêu thụ − M > 50 kWh / 5 m³ |
| `TieuThuThapBatThuong` (mới, đủ ≥ 2 kỳ) | M > 20 kWh / 2 m³ **và** tiêu thụ < M × 0,3 |
| `TieuThuBatThuong` (chưa đủ 2 kỳ) | tiêu thụ > 500 kWh / 30 m³ × số ngày hợp đồng phủ trong tháng / số ngày của tháng |

Ví dụ: với M = 40 kWh, khoảng hợp lệ là 12–90 kWh. Khách vào ngày 20/10 thì trần điện ≈ 193,5 kWh.

Thứ tự kiểm tra: độ tin cậy, không đọc được, `ChiSoGiam`, `NghiSaiChuSo`, rồi tiêu thụ. Gặp lý do đầu tiên thì dừng.

### 2.3 Khi bị cờ

| Lý do | Báo nhân viên | Báo khách chụp lại |
|---|---|---|
| `TieuThuBatThuong` | Có | Không |
| `TieuThuThapBatThuong`, `NghiSaiChuSo` | Có | Có |

- Chuỗi tự động dừng, không tạo hóa đơn. Thông báo kèm số liệu, ví dụ "Điện: 400 kWh, trung vị 40 kWh (cho phép 12–90)".
- Nhân viên xác nhận ảnh hoặc nhập tay: màn hình hiện cảnh báo với cùng số liệu. Số bị cờ thì bắt buộc ghi chú, server kiểm lại. Hộp "Điều chỉnh" hóa đơn chỉ cảnh báo, không bắt ghi chú.

### 2.4 Cấu hình (`AutoInvoiceOptions`)

| Khóa | Mặc định |
|---|---|
| `NguongDoTinCay` | 0.90 |
| `SoKyLichSuToiDa` (thay `SoKyTrungBinh`) | 6 |
| `SoKyToiThieu` | 2 |
| `HeSoTang` (thay `HeSoBatThuong`) | 2 |
| `SanTangDienKwh` / `SanTangNuocM3` | 50 / 5 |
| `HeSoThap` | 0.3 |
| `NguongXetThapDienKwh` / `NguongXetThapNuocM3` | 20 / 2 |
| `TranDienKwh` / `TranNuocM3` | 500 / 30 |

Bỏ `ChuKyJobPhut`.

## 3. Tự tạo hóa đơn

Chuỗi tự động: tự xác nhận ảnh đạt ngưỡng → chốt kỳ → tạo hóa đơn **nháp** (lịch sử có nhãn `[TU_DONG]`) rồi **dừng**.
- Báo nhân viên phụ trách phòng và Admin: "Phòng X kỳ MM/yyyy: có hóa đơn tự động chờ gửi duyệt". **Không** gửi gì cho khách.
- Người thực hiện cần quyền `Meter.Review` và `Invoice.Draft`; không cần `Invoice.Submit` nữa.

## 4. Luồng duyệt và công bố

### 4.1 Luồng chung

```
Tự động:  [hệ thống tạo] ─┐
Lập tay:  Phát sinh ──────┴→ Nháp → [Nhân viên] Gửi duyệt → Chờ duyệt → [Admin] Chốt → Đã chốt → [Nhân viên/Admin] Công bố → Đã gửi
```

Quyền giữ như hiện nay: gửi duyệt và công bố là nhân viên chi nhánh hoặc Admin; chốt và trả lại chỉ Admin.

### 4.2 Sửa trước khi công bố

| Trạng thái | Ai sửa | Cách sửa | Sau khi sửa |
|---|---|---|---|
| Nháp | Nhân viên, Admin | Màn sửa chi tiết hóa đơn và màn chốt điện nước như hiện nay | Nháp |
| Chờ duyệt | Nhân viên, Admin | Hộp "Điều chỉnh" (chỉ số điện nước, thêm, sửa, xóa khoản phí) | Chờ duyệt |
| Đã chốt | **Chỉ Admin** | Hộp "Điều chỉnh" | Đã chốt (Admin là người duyệt, không cần duyệt lại) |

- Mỗi lần điều chỉnh chạy trong một transaction: mở lại về `Nhap` → cập nhật chỉ số và dòng → tính lại tiền → đưa về trạng thái ban đầu. Lịch sử có nhãn `[DIEU_CHINH]`. Lỗi thì rollback. Không báo khách.
- Admin vẫn có nút "Trả lại" để đưa hóa đơn chờ duyệt về nháp.
- Màn chốt điện nước: kỳ có hóa đơn chờ duyệt hoặc đã chốt thì khóa sửa số và hiện link "Sửa qua hóa đơn".

### 4.3 Công bố

Dùng nút "Công bố" sẵn có (`PublishAsync`), thêm một bước: đặt `HanPhanHoi = NgayGui + ChiNhanh.SoGioPhanHoiHoaDon` giờ. Nếu mốc này muộn hơn `HanThanhToan` thì lấy bằng `HanThanhToan`.

Email và thông báo công bố có thêm dòng "Nếu thấy sai sót, vui lòng liên hệ {SĐT chi nhánh} trước dd/MM/yyyy HH:mm".

### 4.4 Bỏ luồng tạm tính cũ

Xóa `AutoInvoiceFinalizeJob`, `AutoInvoiceFinalizeUseCase`, `GetDueProvisionalInvoicesAsync`, bước gửi tạm tính (`GuiTamTinh`) và các chặn riêng của hóa đơn tạm tính trong `FinalizeAsync`, `RejectAsync`. Cột, CHECK và index của `HanPhanHoi` giữ nguyên.

## 5. Sau khi công bố

- Khách xem, tải PDF, thanh toán ngay; hóa đơn tính vào công nợ như mọi hóa đơn `DaGui`.
- Hạn phản hồi chỉ để hiển thị. Hết hạn không chặn gì.
- **Điều chỉnh hóa đơn đã gửi** (nhân viên hoặc Admin): chỉ khi chưa có khoản thu và không có yêu cầu thanh toán đang đối chiếu (điều kiện hủy sẵn có). Trong một transaction: hủy bản cũ → cập nhật → tạo bản thay thế `-R{n}` ở **Chờ duyệt**, rồi đi tiếp luồng 4.1 (Admin chốt, công bố lại). Khách nhận thông báo "hóa đơn đã hủy để điều chỉnh, hóa đơn mới sẽ gửi sau khi duyệt". Đã có khoản thu thì nút khóa, hiện lý do.
- Quyền hủy giữ như hiện nay.

## 6. Giao diện

**Cổng khách thuê**
- Chỉ thấy hóa đơn `DaGui` và hóa đơn đã hủy từng được gửi. Bỏ vế tạm tính trong `GetInvoicesByContractIdsAsync`, `CheckHoaDonOwnershipAsync` và `ExportTenantPdfAsync`.
- Trong hạn phản hồi thì hiện dòng "liên hệ trước …".

**Cổng quản lý**
- Danh sách hóa đơn: thêm nhãn "Tự động"; bỏ nhãn "Tạm tính, tự chốt lúc …".
- Nút "Điều chỉnh" hiện theo bảng 4.2 và mục 5; khi bị khóa thì hiện lý do.
- Chi tiết hóa đơn `DaGui`: hiện hạn phản hồi.
- Màn chốt điện nước và xác nhận ảnh: cảnh báo theo 2.3.
- Việc cần làm: giữ `HD1` (nháp chưa gửi duyệt, gồm hóa đơn tự động), `HD2` (chờ Admin chốt), `HD3` (đã chốt chưa gửi khách); bỏ `HD4`; `HD5` thêm hai lý do mới.

## 7. Tiêu chí chấp nhận

1. Trung vị điện 40 kWh: tháng mới 90 kWh thì tạo hóa đơn nháp. 91 kWh thì dừng `TieuThuBatThuong`. 11 hoặc 0 kWh thì dừng `TieuThuThapBatThuong`, khách được nhắc chụp lại.
2. Lịch sử của khách cũ không được dùng cho hợp đồng mới. Khách vào ngày 20/10: 150 kWh thì qua, 200 kWh thì bị cờ.
3. Chỉ số cũ 1.234, AI đọc 12.345: dừng `NghiSaiChuSo`.
4. Xác nhận số bị cờ mà không ghi chú thì bị từ chối.
5. Hóa đơn tự tạo ở **Nháp**; nhân viên nhận thông báo; khách không thấy, không nhận email.
6. Nhân viên gửi duyệt; nhân viên bấm chốt bị từ chối; Admin chốt được.
7. Điều chỉnh hóa đơn chờ duyệt: tiền tính lại, vẫn chờ duyệt. Admin điều chỉnh hóa đơn đã chốt: vẫn đã chốt. Nhân viên điều chỉnh hóa đơn đã chốt thì bị từ chối.
8. Nhân viên công bố: hóa đơn `DaGui` có `HanThanhToan` và `HanPhanHoi`; khách nhận thông báo và email PDF có dòng "liên hệ trước …"; khách thanh toán được ngay.
9. Điều chỉnh hóa đơn `DaGui` chưa có khoản thu: bản cũ `DaHuy`, có `-R1` ở chờ duyệt. Đã có khoản thu thì bị chặn.
10. Không còn hóa đơn nào tự chốt theo thời gian.

## 8. Kiểm thử

| Project | Nội dung |
|---|---|
| `Domain.UnitTests` | Đặt `HanPhanHoi` khi công bố; điều chỉnh hóa đơn đã chốt giữ trạng thái và người chốt |
| `Application.UnitTests` | `MeterAnomalyPolicy` sát ngưỡng; chuỗi tự động dừng ở Nháp; điều chỉnh theo bảng 4.2 (gồm quyền chỉ Admin ở Đã chốt); công bố đặt hạn phản hồi; điều chỉnh `DaGui`; Việc cần làm; `Validate()` cấu hình |
| `Infrastructure.IntegrationTests` | Truy vấn lịch sử theo hợp đồng (biên ngày 1 và cuối tháng); truy vấn khách xem được |
| `Web.IntegrationTests` | Khách không thấy hóa đơn chưa công bố; khách thanh toán sau khi công bố; nhân viên bị chặn chốt và chặn điều chỉnh hóa đơn đã chốt; job tự chốt không còn được đăng ký |

## 9. Chia đợt

| Đợt | Nội dung |
|---|---|
| A | Kiểm tra bất thường và cấu hình (mục 2, phần server) |
| B | Tự tạo dừng ở Nháp, điều chỉnh theo trạng thái, hạn phản hồi khi công bố, bỏ luồng tạm tính (mục 3, 4, 5, phần dữ liệu của mục 6) |
| C | Giao diện và Việc cần làm (mục 6) |

Sau khi xong: cập nhật `AGENTS.md` (mục Hiện trạng) và đánh dấu các mục hết hiệu lực trong spec cũ.
