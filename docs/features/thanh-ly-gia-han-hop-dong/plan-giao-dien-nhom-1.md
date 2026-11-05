# Kế hoạch: cải tiến giao diện luồng hợp đồng (nhóm 1)

Ngày lập: 05/11/2026. Nhánh: `feature/thanh-ly-gia-han-hop-dong` (làm tiếp trên nhánh này, không tạo nhánh mới).
Người duyệt hướng: người dùng đồng ý làm nhóm 1 ngày 05/11/2026. Nhóm 2 (thống nhất kiểu chữ, ngày, tiền) và nhóm 3 (nút AI che nội dung, bảng tràn, bố cục 2 cột) **không** thuộc kế hoạch này.

## 0. Ràng buộc chung (bắt buộc)

- **Không đổi schema**, không sinh migration. Cuối cùng chạy `dotnet ef migrations has-pending-model-changes` phải báo không có thay đổi.
- Ranh giới tầng: Web không tham chiếu Domain; Application trả `ServiceResult`; service phía quản lý nhận `actorId` làm tham số đầu, fail closed; method mới cần phân quyền thì đi qua `HopDongAccessGuard.CheckAsync` (không thêm vào danh sách ngoại lệ của `BranchScopeArchitectureTests`).
- Lưu UTC, chỉ đổi sang giờ VN ở ranh giới. Ngày trả phòng lưu **23:59:59 giờ VN** (= 16:59:59Z cùng ngày), giống cách `GiaHanHopDongService` lưu ngày kết thúc cũ.
- Không sửa `Program.cs`, `.csproj`, `appsettings*`, fixture test, test kiến trúc. Không tạo script PowerShell phụ trợ.
- **Giữ nguyên các thay đổi chưa commit** đang có trên nhánh (sửa S5–S7: `InvoiceReceiptService.cs`, `GiaHanHopDongService.cs`, `ThanhLy.cshtml`, hai file test, `review.md`, `README.md` của module). Nếu phải sửa cùng file (`ThanhLy.cshtml`), sửa chồng lên, không hoàn tác đoạn `capNhatNutXacNhan`/`dangGui`.
- Không commit, push hay merge. Người dùng tự quyết.
- Giao diện, thông báo lỗi viết tiếng Việt. Chuỗi hiển thị trong JS phải escape (`escapeHtml` có sẵn trong `QuanLyHopDong.cshtml`, hoặc `$.fn.dataTable.render.text()`), Swal dùng `text` chứ không dùng `html` với dữ liệu người dùng.

## 1. Ngày trả phòng ngay trên màn thanh lý

**Vấn đề:** màn `/QuanLyNhaTro/HopDong/ThanhLy/{id}` lấy tháng trả phòng theo `ThoiDiemKetThuc`. Muốn đổi ngày phải về danh sách, mở form sửa 4 bước.

### 1.1 Application

File `Features/HopDongs/Services/ThanhLyHopDongService.cs` (và interface `IThanhLyHopDongService` cùng feature):

```csharp
Task<ServiceResult> CapNhatNgayTraPhongAsync(int actorId, int hopDongId, DateOnly ngayTraPhong, CancellationToken ct = default);
```

Thứ tự xử lý:

1. `actorId <= 0` → Forbidden. Lấy hợp đồng, kiểm quyền bằng `HopDongAccessGuard.CheckAsync(..., EmployeeActionCodes.ContractSettle, ...)` giống `ThanhLyAsync` (đọc đoạn đầu `ThanhLyAsync` và làm y hệt cách nó trả NotFound/Forbidden).
2. Mở transaction (`_unitOfWork.BeginTransactionAsync`), `LockHopDongAsync(hopDongId)`. Kiểm lại trong transaction:
   - hợp đồng phải `DangHoatDong`, nếu không: `Fail("Chỉ đổi ngày trả phòng của hợp đồng đang hoạt động.")`;
   - `ngayTraPhong` ≥ ngày bắt đầu (ngày VN của `ThoiDiemBatDau`), nếu không: `Fail("Ngày trả phòng phải từ ngày bắt đầu hợp đồng trở đi.")`;
   - ngày mới trùng ngày VN của `ThoiDiemKetThuc` hiện tại → trả `Ok` luôn, không ghi.
3. Chặn khi đã có hóa đơn tính theo ngày cũ: gọi `mocKiem` = tháng nhỏ hơn giữa tháng trả phòng cũ (ngày VN của `ThoiDiemKetThuc`, nếu có) và tháng của ngày mới. Nếu `_store.HasInvoiceFromPeriodAsync(hopDongId, mocKiem.Thang, mocKiem.Nam)` là `true` → `Fail($"Đã có hóa đơn từ T{thang}/{nam} trở đi tính theo ngày trả phòng cũ. Hủy các hóa đơn đó trước khi đổi ngày trả phòng.")`.
   Lý do: tiền phòng tháng cuối được tính theo ngày (`HoaDonCalculatorService.TinhTienPhong`), đổi ngày khi đã có hóa đơn tháng đó sẽ làm hóa đơn sai. `HasInvoiceFromPeriodAsync` đã bỏ hóa đơn `IsDeleted` (hóa đơn hủy).
4. Ghi `hopDong.ThoiDiemKetThuc = DateTime.SpecifyKind(ngayTraPhong.ToDateTime(new TimeOnly(23, 59, 59)).AddHours(-7), DateTimeKind.Utc)`, cập nhật `NgayCapNhat` nếu entity có và các service khác đang làm vậy, `SaveChanges` + commit. Trả `Ok(..., "Đã cập nhật ngày trả phòng.")`.
5. Log thông tin: actor, hợp đồng, ngày cũ → ngày mới (không log dữ liệu cá nhân).

Không cần store method mới. Nếu cần thì thêm vào `IHopDongLifecycleStore` + `HopDongLifecycleStore` + `FakeHopDongLifecycleStore`.

### 1.2 Web

`Areas/QuanLyNhaTro/Controllers/HopDongChiTietController.cs`:

```csharp
[HttpPost("/HopDong/NgayTraPhong/{id:int}")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> CapNhatNgayTraPhong(int id, [FromBody] NgayTraPhongBody? body, CancellationToken ct)
```

- `body` null hoặc `NgayTraPhong` không parse được bằng `DateOnly.TryParseExact(..., "yyyy-MM-dd", CultureInfo.InvariantCulture, ...)` → `BadRequest` với thông điệp `DuLieuKhongHopLe` (hằng có sẵn trong controller).
- Lỗi service → `this.ToErrorResult(result)`; thành công → JSON giống các POST khác của controller (đọc `ThanhLy` POST để làm đồng dạng).
- `NgayTraPhongBody` đặt cạnh `ThanhLyHopDongBody` (cùng chỗ khai báo), thuộc tính `string? NgayTraPhong`.

### 1.3 Giao diện `Views/HopDongChiTiet/ThanhLy.cshtml`

- Thêm vào DTO preview cờ `DangHoatDong` (bool) nếu chưa có cách nào khác để biết hợp đồng đang hoạt động.
- Ngay dưới tiêu đề trang, trước các card hiện có: card nhỏ "Ngày trả phòng" chỉ hiện khi `DangHoatDong`:
  - `<input type="date" id="ngayTraPhong" min="{ngày bắt đầu VN}" value="{ngày kết thúc VN hoặc rỗng}">`, nút `#btnLuuNgayTraPhong` "Cập nhật".
  - Dòng chú thích: "Tháng trả phòng quyết định hóa đơn cuối. Đổi ngày sẽ tính lại toàn bộ màn này."
  - Bấm nút: rỗng → `Swal` cảnh báo; khác rỗng → `Swal.fire` xác nhận (`text`, không `html`) → AJAX POST JSON `{ ngayTraPhong: 'yyyy-MM-dd' }`, header `RequestVerificationToken` → thành công `location.reload()`; lỗi → bật lại nút, Swal lỗi với `xhr.responseJSON.message`.
- Không đụng logic nút Xác nhận thanh lý đã sửa ở S7.

### 1.4 Test

- `tests/QuanLyChoThuePhongTroWeb.Application.UnitTests/ThanhLyHopDongServiceTests.cs` (dùng `FakeHopDongLifecycleStore`, `FakeEmployeeAccessService`, `FixedTimeProvider`):
  - đổi ngày hợp lệ → `ThoiDiemKetThuc` = 16:59:59Z đúng ngày;
  - ngày trước ngày bắt đầu → Fail, không ghi;
  - hợp đồng không hoạt động → Fail;
  - có hóa đơn chưa hủy ở tháng mới hoặc tháng cũ (lấy tháng nhỏ hơn) → Fail; hóa đơn đã hủy (`IsDeleted`) không chặn;
  - nhân viên không có `ContractSettle` / khác chi nhánh → Forbidden, không ghi;
  - ngày trùng ngày cũ → Ok, không gọi lưu.
- `tests/QuanLyChoThuePhongTroWeb.Web.IntegrationTests/ContractSettlementRenewalTests.cs`: POST không có antiforgery → 400; body sai định dạng → 400; nhân viên khác chi nhánh → 403 (theo cách test hiện có kiểm 403); thành công → đọc lại DB thấy giờ 16:59:59Z, GET màn thanh lý hiện ngày mới.

## 2. Nút "Lập hóa đơn T{m}/{y}" khi thiếu hóa đơn tháng trả phòng

**Vấn đề:** card "Chưa thanh lý được" chỉ liệt kê lý do bằng chữ; người dùng phải tự tìm màn hóa đơn, chọn chi nhánh, tháng, năm.

### 2.1 Application

- `ThanhLyCalculator`: tách điều kiện thiếu hóa đơn tháng trả phòng (dòng ~72) thành hàm public static, ví dụ `ThieuHoaDonThangTraPhong(int hopDongId, DateTime thoiDiemKetThucUtc, IReadOnlyList<ThanhLyHoaDonInput> hoaDons)`; `TimLyDoChan` gọi lại hàm này (giữ nguyên câu chữ lý do).
- `ThanhLyHopDongPreviewDto` (`DTOs/ThanhLyDtos.cs`) thêm:
  - `int ChiNhanhId`, `int PhongTroId`;
  - `int? ThangTraPhong`, `int? NamTraPhong` (từ ngày VN của `ThoiDiemKetThuc`);
  - `bool ThieuHoaDonThangTraPhong`;
  - `List<ThanhLyHoaDonCanXuLyDto> HoaDonChuaCongBo` (HoaDonId, MaHoaDon, Thang, Nam, TrangThaiText) cho các hóa đơn Nháp/Chờ duyệt/Đã chốt trong chuỗi.
- `GetPreviewAsync` điền các trường trên từ dữ liệu đã có (không thêm truy vấn nếu tránh được).

### 2.2 Giao diện

- `ThanhLy.cshtml`, trong card "Chưa thanh lý được":
  - nếu `ThieuHoaDonThangTraPhong`: nút `<a class="btn btn-primary">Lập hóa đơn T{m}/{y}</a>` tới `/QuanLyNhaTro/QuanLyHoaDon?chiNhanhId={ChiNhanhId}&thang={m}&nam={y}&moPhatSinh=1&phongTroId={PhongTroId}`; kèm dòng nhắc "Cần chốt chỉ số điện nước tháng này trước khi phát sinh hóa đơn" (chỉ là gợi ý);
  - với mỗi `HoaDonChuaCongBo`: link "Mở {MaHoaDon}" tới `/QuanLyNhaTro/QuanLyHoaDon?hoaDonId={id}` (trang hóa đơn đã hỗ trợ tham số này, `Views/HoaDon/Index.cshtml` khoảng dòng 1246).
- `Views/HoaDon/Index.cshtml`: đọc thêm `moPhatSinh` và `phongTroId` từ `urlParams` (cạnh các dòng ~420–433). Khi `moPhatSinh === '1'`, **sau khi** bộ lọc chi nhánh/tháng/năm đã nhận giá trị từ URL (kiểm thứ tự khởi tạo select2/flatpickr, có thể phải chờ danh sách chi nhánh tải xong), mở `#previewModal` như khi bấm `#btnPhatSinh`, đồng bộ `#previewChiNhanh/#previewThang/#previewNam`, gọi tải xem trước như `#btnLoadPreview`. Khi danh sách `.preview-select` đã vẽ và có `phongTroId`: chỉ tích ô có `data-id == phongTroId`, bỏ tích các ô khác, cuộn tới dòng đó. Không tự bấm nút tạo hóa đơn.
- Xóa `moPhatSinh` khỏi URL sau khi đã dùng (`history.replaceState`) để tải lại trang không mở modal lần nữa.

### 2.3 Test

- Unit test `ThanhLyCalculatorTests`: hàm mới đúng ở ranh giới tháng (ngày cuối tháng lưu 16:59:59Z, ngày 1 lưu 00:00 VN = 17:00Z hôm trước).
- Unit test `ThanhLyHopDongServiceTests`: preview điền `ThangTraPhong/NamTraPhong/ThieuHoaDonThangTraPhong/HoaDonChuaCongBo` đúng.
- Kiểm giao diện trên preview (mục 6).

## 3. Danh sách hợp đồng: cột "Hết hạn", huy hiệu "Đã gia hạn", "Quá hạn N ngày"

**Vấn đề:** danh sách không có ngày kết thúc; hợp đồng đã gia hạn hiện "Đã kết thúc" (lệch trang chi tiết, nơi hiện "Đã gia hạn → mã mới"); không thấy hợp đồng quá hạn.

### 3.1 Helper chung

`Features/HopDongs/Services/HopDongCodeChain.cs` thêm:

```csharp
// Mã kế tiếp trong chuỗi: mã có chỉ số gia hạn lớn hơn mã hiện tại, nhỏ nhất. Danh sách vào chỉ gồm hợp đồng chưa hủy, cùng mã gốc.
public static string? TimMaKeTiep(string maHienTai, IEnumerable<string> maCungChuoiChuaHuy)
```

Sửa `HopDongTraCuuService.TimKeTiep` dùng helper này (giữ nguyên hành vi: bỏ hợp đồng `DaHuy`). Thêm unit test cho helper vào `tests/QuanLyChoThuePhongTroWeb.Application.UnitTests/HopDongCodeChainTests.cs` (file đã có).

### 3.2 DTO và truy vấn

- `HopDongRes` thêm `string? MaHopDongKeTiep` và `int? SoNgayQuaHan`.
- `Infrastructure/Persistence/Features/HopDongStore.GetDataTableResponseAsync`:
  - Select thêm `PhongTroId` (đang thiếu).
  - Sau khi lấy trang `data`: lấy `PhongTroId` của trang, truy vấn một lần các hợp đồng `!IsDeleted && TrangThaiHopDong != DaHuy && phongIds.Contains(PhongTroId)` chỉ chọn `PhongTroId, MaHopDong` (gia hạn luôn cùng phòng). Trong bộ nhớ, với mỗi dòng: lọc các mã cùng phòng có `HopDongCodeChain.BelongsTo(ma, RootOf(dòng.MaHopDong))`, gọi `TimMaKeTiep`. Không để N+1 truy vấn.
  - Thêm sắp xếp `"thoiDiemKetThuc"` vào `switch` sắp xếp.
- `Application/Features/HopDongs/Services/HopDongService.DanhSachHopDongSideAsync`: sau khi store trả kết quả, tính `SoNgayQuaHan` cho dòng `DangHoatDong` có `ThoiDiemKetThuc`: `homNayVn = DateOnly.FromDateTime(now.AddHours(7))`, `ketThucVn = DateOnly.FromDateTime(ThoiDiemKetThuc.Value.AddHours(7))`; nếu `ketThucVn < homNayVn` thì `SoNgayQuaHan = homNayVn.DayNumber - ketThucVn.DayNumber`, ngược lại `null`. Dùng `_timeProvider` sẵn có (đã dùng ở dòng 70). Khớp bộ lọc `QuaHan` của store (hết hạn trước đầu ngày hôm nay).

### 3.3 Giao diện `Views/HopDong/QuanLyHopDong.cshtml`

- Thêm `<th>Hết hạn</th>` ngay sau "Ngày Bắt Đầu" và cột tương ứng trong `columns` (`data: "thoiDiemKetThuc"`, `name: "thoiDiemKetThuc"`), hiện `dd/MM/yyyy` theo giờ VN: `new Date(data).toLocaleDateString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh', day: '2-digit', month: '2-digit', year: 'numeric' })`; rỗng → "—". `"order": [[4, 'desc']]` vẫn trỏ cột Ngày bắt đầu (index 4), kiểm lại.
- Cột Trạng thái:
  - `1` và `row.soNgayQuaHan > 0`: thêm dưới badge "Đang hoạt động" một badge `bg-danger-lt` "Quá hạn {N} ngày";
  - `2` và `row.maHopDongKeTiep`: badge `bg-info-lt` "Đã gia hạn" với `title="→ {mã}"` (escape) thay cho "Đã kết thúc";
  - còn lại giữ như cũ.
- Kiểm `colspan`, chiều rộng cột, chế độ responsive của DataTables (nếu có) sau khi thêm cột.

### 3.4 Test

- Application `HopDongServiceTests`: fake store trả dòng hết hạn hôm qua / hôm nay / ngày mai / đã kết thúc → `SoNgayQuaHan` = 1 / null / null / null (với `FixedTimeProvider`, chú ý ranh giới 17:00Z).
- Infrastructure `HopDongLifecycleStoreTests` (hoặc file test store hợp đồng phù hợp): hợp đồng gốc đã kết thúc + `-GH1` hoạt động → dòng gốc có `MaHopDongKeTiep = "...-GH1"`; `-GH1` bị hủy → `null`; `-GH2` trên `-GH1` → dòng `-GH1` trỏ `-GH2`; dòng có `PhongTroId`. Sắp xếp theo `thoiDiemKetThuc` chạy được.
- Tìm Web test gọi endpoint danh sách hợp đồng (nếu có) và cập nhật nếu kiểm hình dạng JSON.

## 4. Menu "⋯" thêm Thanh lý, Gia hạn

`QuanLyHopDong.cshtml`, hàm render cột thao tác (~dòng 607–640): khi `row.trangThaiHopDong === 1` thêm, sau "Chi tiết":

```html
<a href="/QuanLyNhaTro/HopDong/ThanhLy/${data}" class="dropdown-item py-2"><i class="ti ti-file-check me-2"></i> Thanh lý</a>
<a href="/QuanLyNhaTro/HopDong/GiaHan/${data}" class="dropdown-item py-2"><i class="ti ti-calendar-plus me-2"></i> Gia hạn</a>
```

`data` là `hopDongId` số nguyên. Quyền vẫn do server kiểm (màn chi tiết cũng hiện nút theo trạng thái, không theo quyền), không cần thêm kiểm ở danh sách.

## 5. Menu trái giữ mục đang chọn trên màn con

**Vấn đề:** `_MenuItem.cshtml` chỉ tô sáng khi đường dẫn bằng `Url` hoặc bắt đầu bằng `Url + "/"`. Màn `/QuanLyNhaTro/HopDong/ChiTiet|ThanhLy|GiaHan/{id}` không bắt đầu bằng `/QuanLyNhaTro/QuanLyHopDong`, màn `/QuanLyNhaTro/NguoiThue/ChiTiet/{id}` không bắt đầu bằng `/QuanLyNhaTro/QuanLyNguoiThue`, nên nhóm menu bị thu gọn.

- `Web/Models/ModelsOther/MenuItem.cs` (namespace `QuanLyChoThuePhongTroWeb.Models.ModelsOther`): thêm `public List<string>? ActivePrefixes { get; set; }`.
- `Views/Shared/_MenuItem.cshtml`: đổi `IsActiveUrl(string url)` thành hàm nhận `MenuItem`, true khi `Url` khớp như hiện tại **hoặc** một prefix trong `ActivePrefixes` khớp (bằng prefix hoặc bắt đầu bằng `prefix + "/"`, không phân biệt hoa thường). Dùng cho cả `isAnyChildActive`, `isParentActive`, `isChildActive`.
- `Web/Services/MenuService.cs`: "Hợp đồng" `ActivePrefixes = ["/QuanLyNhaTro/HopDong"]`; "Người thuê" `ActivePrefixes = ["/QuanLyNhaTro/NguoiThue"]`. Grep để chắc không có mục menu khác có URL bắt đầu bằng hai prefix này.
- Test Web (nếu có test layout/menu thì thêm; nếu không, thêm một test trong Web.IntegrationTests): GET `/QuanLyNhaTro/HopDong/ChiTiet/{id}` bằng Admin, HTML có `dropdown-item active` trỏ `/QuanLyNhaTro/QuanLyHopDong`; GET `/QuanLyNhaTro/NguoiThue/ChiTiet/{id}` có mục "Người thuê" active.

## 6. Kiểm chứng

Chạy tuần tự (không chạy hai lượt `dotnet test` song song). Dừng preview trước khi build/test vì preview khóa file build.

1. `dotnet build QuanLyChoThuePhongTroWeb.sln`
2. `dotnet test tests/QuanLyChoThuePhongTroWeb.Domain.UnitTests`
3. `dotnet test tests/QuanLyChoThuePhongTroWeb.Application.UnitTests`
4. Integration (lấy chuỗi kết nối DB test từ biến môi trường User `QLCTPT_TEST_CONNECTION_STRING`, DB phải kết thúc `_test`; dùng skill `chay-test-tich-hop`): `Infrastructure.IntegrationTests` rồi `Web.IntegrationTests`. Thiếu biến thì ghi CHƯA CHẠY.
5. `dotnet ef migrations has-pending-model-changes` (lệnh trong `AGENTS.md`) → không có thay đổi.
6. `git diff --check`.
7. Giao diện trên preview `web` (`.claude/launch.json`, cổng 5009, DB phát triển, người dùng đã đăng nhập Admin; navigate lần đầu có thể lỗi, đợi ~5 giây thử lại). **Không bấm nút thao tác ghi thật** (thanh lý, gia hạn, tạo hóa đơn, đổi ngày trả phòng) trên DB phát triển khi chưa hỏi người dùng. Chỉ xem:
   - danh sách hợp đồng: cột Hết hạn, badge "Đã gia hạn" ở HD-GV2-2610-001 (id 1, đã có GH1 id 6), menu ⋯ của hợp đồng đang hoạt động có Thanh lý, Gia hạn;
   - màn thanh lý id 2 hoặc id 6: card Ngày trả phòng hiện đúng ngày, nút "Lập hóa đơn T…" dẫn tới màn hóa đơn đã lọc và mở modal xem trước, tích sẵn đúng phòng (đóng modal, không tạo);
   - menu trái tô sáng "Hợp đồng" ở chi tiết/thanh lý/gia hạn, "Người thuê" ở lịch sử thuê;
   - console không có lỗi JS; thử bề rộng ~800px cho bảng hợp đồng.

## 7. Tài liệu và bàn giao

- `docs/features/thanh-ly-gia-han-hop-dong/README.md`: thêm mục "Cải tiến giao diện nhóm 1" với 5 checkbox; ghi quy tắc chặn đổi ngày trả phòng khi đã có hóa đơn.
- `docs/features/thanh-ly-gia-han-hop-dong/review.md`: thêm phần kết quả kiểm chứng nhóm 1 (lệnh, PASS/FAIL/CHƯA CHẠY, những gì chưa thử thật).
- Báo cáo ngày `.agents/bao-cao-hang-ngay/<ngày>/bao-cao.md` theo mẫu `.agents/bao-cao-hang-ngay/README.md`.
- Bàn giao: liệt kê file đã sửa, lệnh đã chạy với kết quả, điểm lệch kế hoạch (nếu có) kèm lý do, việc chưa làm.

## 8. Thứ tự gợi ý

5 (menu, nhỏ, độc lập) → 4 (menu ⋯) → 3 (danh sách) → 1 (ngày trả phòng) → 2 (nút lập hóa đơn) → kiểm chứng → tài liệu.
