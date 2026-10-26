# Review và kiểm chứng: Thanh toán trực tuyến — VNPay (đợt 2)

- **Nhánh:** `feature/thanh-toan-truc-tuyen`. **Spec:** [spec.md](spec.md). **Kế hoạch:** [plan.md](plan.md). **Hướng dẫn thiết lập:** [../huong-dan-thiet-lap.md](../huong-dan-thiet-lap.md) mục 9.
- **Cập nhật lần cuối:** 26/10/2026, sau khi xử lý các việc còn mở (`3de7181`), dọn DB test và rà lại từ code tới tài liệu.
- **Kết luận:**
  - Cả 2 phần đã code xong, reviewer ra CHOT cho từng phần. **Không đổi schema.**
  - Test tự động: tester và reviewer từng phần đều PASS. Lượt chạy lại trên mã hiện tại PASS ở Domain, Application, Infrastructure; Web từng có 2 FAIL do `appsettings.json` đang bật VNPay lọt vào test. Đã cố định `VnPayOptions` trong fixture, Web PASS 657/657 (mục 4). Sau các sửa T2, T4, `PaymentUrl`, bảng 375 px và dọn DB test: Domain 135, Application 2092, Infrastructure 621, Web 658 đều PASS (mục 4).
  - Lần thử sandbox đầu tiên: IPN không tới vì chưa khai IPN URL (mục 8 ý 7).
  - Sandbox, 26/10/2026: V01, V02 PASS, có log ngrok đối chiếu; V07 (cách encode chữ ký) PASS theo V01; V03–V06 PASS theo người dùng báo.
  - Cookie khi VNPay chuyển về: đạt ở V01/V02 (trang kết quả cần đăng nhập, log ngrok trả 200, không phải 302 về trang đăng nhập). SignalR tự tải lại trang "đang ghi nhận": đạt ở V04 theo người dùng báo.
  - Giao diện 375 px sáng/tối đã kiểm 26/10/2026 trên trình duyệt giả lập: phần VNPay đạt; phát hiện lỗi có từ trước làm modal chi tiết hóa đơn bị bóp còn 32 px ở 375 px (mục 7).

## 1. Nguồn bằng chứng và giới hạn

| Nguồn | Có gì | Giới hạn |
| --- | --- | --- |
| Commit `74e5c81` | Phạm vi phần 1 (44 file) | Commit message chỉ tóm tắt |
| [plan.md](plan.md) §3, §4 "Kết quả" | Các quyết định người dùng duyệt (B1–B5', X1–X5, Q1, D1–D4), khuyến nghị của reviewer, số test | Do agent ghi lại sau mỗi lượt `/ship` |
| `.bangiao/danh-gia.md`, `ket-qua-test.md`, `thay-doi.md` | Review, test và danh sách thay đổi gốc của **phần 2** | `.bangiao/` không lưu trong Git; mỗi lượt `/ship` ghi đè lượt trước |
| Phần 1 | Phán quyết CHOT và số test, ghi ở plan §3 và báo cáo ngày 26/10/2026 | **Bản review và kết quả test gốc của phần 1 không còn** vì phần 2 đã ghi đè |
| Đối chiếu khi viết file này (26/10/2026) | Tìm kiểu, route, khóa cấu hình trong `src/`; so `git diff` với `master` và `fbb393a`; chạy lại test | Chỉ đối chiếu tên, sự tồn tại và diff; **chưa đối chiếu hành vi từng dòng** của spec với code |
| Rà lại sau `3de7181` (26/10/2026) | Đọc `VnPayPaymentService`, `VnPayProtocol`, `VnPayIpnController`, `ThanhToanHoaDonController`, `khachthue-vnpay.js`, phần tab trong `khachthue-thanhtoan.js`, view và view model trang kết quả, badge quản lý, `VnPayOptions`, đăng ký DI; so với spec §4, §5, §6.1, §8, §10 | Đối chiếu bằng đọc code và test tự động; trang kết quả trên trình duyệt chỉ thấy trạng thái "Không xác thực" (mục 7) |

## 2. Kết quả theo phần

| Phần | Commit | Phán quyết reviewer | Ghi chú |
| --- | --- | --- | --- |
| 1 — backend: cấu hình, bộ ký, IPN, ghi sổ, thông báo, chặn ghi trùng | `74e5c81` | CHOT | Người dùng duyệt B1–B5' (plan §3) |
| 2 — tab trong panel khách, tạo link, trang kết quả, badge ở màn quản lý, hướng dẫn mục 9, T1/T3/Q1 | `0f326ad` | CHOT, kèm 3 khuyến nghị thấp đã sửa (mục 3) | Người dùng duyệt G1–G6, X1(c), X2(b), X3, X4(b), X5(a), Q1, D1–D4 (plan §4) |
| Sau sandbox — T2, T4, `PaymentUrl` không chứa `?`/`#`, bảng hóa đơn khách 375 px, tài liệu | `3de7181` | Không qua `/ship`; người dùng chọn cách xử lý từng việc (mục 3, mục 7) | Toàn bộ test PASS trên DB test mới (mục 4). `HandleIpnAsync` thêm bước 4b so với lúc reviewer phần 2 xác minh |

### Các điểm reviewer phần 2 đã xác minh

- Mọi điểm liên quan đến tiền đều có dòng "ĐÃ XÁC NHẬN" trong `.bangiao/ke-hoach.md`; không có điểm tiền nào nằm ngoài phần đã duyệt.
- `HandleIpnAsync` giữ nguyên từng dòng so với `74e5c81`, chỉ constructor thêm `IInvoicePaymentStore`.
- Tạo link và trang kết quả chỉ đọc: không ghi, không khóa, không mở transaction.
- Quyền:
  - `nguoiThueId` lấy từ claim, service kiểm `KhachXemDuoc`.
  - Khách khác bị 404 khi tạo link, và nhận `KhongXacThuc` ở trang kết quả, không lộ mã hóa đơn hay số tiền.
- Không có open redirect: URL trả về luôn là `PaymentUrl` (https, đã qua `Validate()`), và JS chỉ chuyển trang khi URL bắt đầu bằng `https://`.
- Log không chứa URL, chữ ký, `HashSecret` hay `vnp_OrderInfo`. View không dùng `Html.Raw`.
- Ranh giới tầng giữ nguyên (test kiến trúc PASS); không sửa file cấm.

## 3. Khuyến nghị của reviewer và cách xử lý

| Từ | Mục | Trạng thái |
| --- | --- | --- |
| Phần 1 | T1: log dùng chung ghi "SePay 0" cho VNPay | **Đã làm** ở phần 2, qua `IncomingBankTransfer.LogLabel` (Publisher, Dispatcher, và cả Worker theo Q1) |
| Phần 1 | T2: trả `01` cho hóa đơn không tồn tại (hiện trả `02` + cảnh báo hoàn tiền, đúng spec) | **Đã làm** 26/10/2026 (người dùng chọn): IPN thành công cho hóa đơn không có trong DB trả `01`, log Warning, không cảnh báo hoàn tiền; hóa đơn đã xóa mềm/hủy vẫn `02` + cảnh báo; IPN không thành công vẫn `00` (spec §5.2 bước 4b) |
| Phần 1 | T3: cảnh báo việc thu thủ công bằng mã TXN thô trong lúc VNPay còn gọi lại IPN | **Đã làm**: hướng dẫn mục 9.7 |
| Phần 1 (có từ đợt 1) | T4: thu thủ công chưa cấm mã bắt đầu bằng `VNPAY-`/`SEPAY-` | **Đã làm** 26/10/2026 (người dùng chọn): `InvoicePaymentPolicy.ValidateMaGiaoDich` từ chối tiền tố (không phân biệt hoa thường) ở thu thủ công và xác nhận minh chứng cũ, báo nhập mã gốc (spec §6.1) |
| Phần 2 | Race: trang kết quả có thể báo nhầm `KhongGhiNhan` khi IPN commit giữa hai lần đọc | **Đã sửa** 26/10/2026: đọc lại sổ trước khi kết luận; thêm test `Success_WhenTheIpnCommitsBetweenTheLedgerAndInvoiceReads_IsDaGhiNhan` |
| Phần 2 | Hướng dẫn 9.4 khẳng định "Sandbox chấp nhận cả hai" dù chưa thử | **Đã sửa**: đổi thành "chưa thử", kèm việc cần làm nếu VNPay báo lỗi tham số; sau V01 đổi tiếp thành "đã thử ở V01" (mục 6) |
| Phần 2 | Trạng thái cũ trong README/plan | **Đã sửa** |
| Phần 2 | VNPay có hạn mức số tiền tối thiểu mỗi giao dịch, chưa đối chiếu với tài liệu | **Không làm** (người dùng xác nhận 26/10/2026 trường hợp này không xảy ra). Tài liệu chính thức `pay.html` không nêu hạn mức tối thiểu/tối đa, chỉ quy định `vnp_Amount` là số 1–12 chữ số đã nhân 100 |
| Phần 2 | `VnPayProtocol.BuildPaymentUrl` luôn nối `?`, sai nếu `PaymentUrl` đã có sẵn query | **Đã làm** 26/10/2026 (người dùng chọn): `VnPayOptions.Validate()` báo lỗi khi khởi động nếu `PaymentUrl` chứa `?` hoặc `#`; không đổi cách dựng URL đã chạy đúng ở V01 |

## 4. Kết quả test

| Lệnh | Kết quả | Thời điểm và giới hạn |
| --- | --- | --- |
| `dotnet test QuanLyChoThuePhongTroWeb.sln` (một lượt) | PASS: Domain 135, Application 1957, Infrastructure 619, Web 602 | Phần 1, tester, 26/10/2026 |
| `dotnet test QuanLyChoThuePhongTroWeb.sln` (một lượt, DB `quanlyphongtro_test`) | PASS: Domain 135/135, Application 2064/2064, Infrastructure 621/621, Web 657/657 | Phần 2, tester, 26/10/2026, trước khi sửa race |
| `dotnet ef migrations has-pending-model-changes` | PASS: "No changes have been made to the model since the last migration." | Tester của phần 1 và phần 2 |
| `dotnet test tests/QuanLyChoThuePhongTroWeb.Application.UnitTests` | PASS 2065/2065 | Sau khi sửa race |
| Web.IntegrationTests lọc `FullyQualifiedName~VnPay\|FullyQualifiedName~TenantOnlinePayment` | PASS 118/118 | Reviewer phần 2, và chạy lại sau khi sửa race |
| `dotnet test QuanLyChoThuePhongTroWeb.sln` (một lượt, DB `quanlyphongtro_test`) | Domain PASS 135/135; Application PASS 2065/2065; Infrastructure PASS 621/621; Web FAIL hàng loạt | 26/10/2026, mã hiện tại (gồm 3 sửa sau review). Web: test đầu tiên FAIL vì một truy vấn DB hết thời gian 30 giây (`ThongBaoChiNhanhTests`), sau đó nhiều test FAIL dây chuyền, mỗi test 1 ms. Coi là lỗi môi trường DB, chưa điều tra thêm |
| `dotnet test tests/QuanLyChoThuePhongTroWeb.Web.IntegrationTests` (chạy lại một mình) | 655/657, 2 FAIL | Hai test kiểm mặc định "VNPay tắt": `VnPayCheckoutWebTests.Panel_ByDefault_ReportsVnPayDisabled`, `VnPayIpnTests.DefaultHost_WithoutVnPayConfiguration_Returns404`. Lỗi do môi trường, không do code: lúc chạy, người dùng đang bật `VnPay:Enabled` trong `appsettings.json` để thử sandbox. `CustomWebApplicationFactory` gỡ nguồn `appsettings.json` nhưng `AddInfrastructure` đã đọc `VnPayOptions` trước đó. Factory cố định `SePayOptions` nhưng chưa cố định `VnPayOptions`. Các test VNPay khác tự thay `VnPayOptions` nên vẫn PASS |
| Sửa `CustomWebApplicationFactory` (người dùng duyệt 26/10/2026): cố định `VnPayOptions { Enabled = false }` giống `SePayOptions`; test cần bật VNPay vẫn tự thay qua `WithWebHostBuilder` | — | Chỉ sửa fixture, không sửa code sản phẩm hay kỳ vọng test |
| `dotnet test tests/QuanLyChoThuePhongTroWeb.Web.IntegrationTests` sau khi sửa fixture (một mình, `appsettings.json` vẫn bật VNPay) | PASS 657/657 | 26/10/2026. Cùng mã sản phẩm với lượt toàn solution ở trên, nên Domain, Application, Infrastructure lấy kết quả PASS của lượt đó |
| `dotnet ef migrations has-pending-model-changes` | PASS: "No changes have been made to the model since the last migration." | 26/10/2026, mã `0f326ad` |
| `git diff --check` | PASS (chỉ cảnh báo LF/CRLF) | 26/10/2026 |
| Sau khi sửa T2, T4, `PaymentUrl` (26/10/2026): `dotnet build` | PASS, 0 lỗi | |
| `dotnet test` Application | PASS 2092/2092 | Có sửa kỳ vọng 2 test cũ theo quyết định của người dùng: `VnPayIpnServiceTests.UnknownInvoice_*` (02 → 01) và `InvoicePaymentConfirmationSePayGuardTests.ManualCollection_WithExactSePayKeyTyped_*` (câu báo trùng → câu báo tiền tố); thêm `InvoiceManualCodePrefixTests`, 2 ca IPN, 3 ca `VnPayOptionsTests` |
| `dotnet test QuanLyChoThuePhongTroWeb.sln` | Domain 135, Application 2092, Infrastructure 621 PASS; Web FAIL hàng loạt | Web: timeout DB ở `ThongBaoChiNhanhTests` rồi FAIL dây chuyền (mục 8 ý 10). Trong đó có 1 lỗi thật: `VnPayIpnScenarioTests` còn kỳ vọng T2 cũ, đã sửa thành `TxnRefOfAnInvoiceThatDoesNotExist_Returns01_WithoutARefundAlert` |
| `dotnet test` Web chạy một mình | FAIL 555/1210 | Cùng lỗi timeout DB ở `ThongBaoChiNhanhTests`, không do code |
| Web lọc `VnPay|TenantOnlinePayment|SePay|HoaDon|Invoice|LichSu|Payment` | PASS 384/384 | Kiểm các thay đổi thanh toán |
| Dọn DB test (người dùng duyệt 26/10/2026): `DROP DATABASE quanlyphongtro_test WITH (FORCE)` rồi tạo lại rỗng; fixture tự migrate | — | Mục 8 ý 10. Không đụng DB phát triển |
| `dotnet test QuanLyChoThuePhongTroWeb.sln` trên DB test mới | Domain PASS 135/135; Application PASS 2092/2092; Infrastructure PASS 621/621 (24 giây); Web không build được | Web lỗi MSB3027: DLL bị khóa bởi web preview đang chạy để kiểm 375 px. Lỗi môi trường, đã tắt preview rồi chạy lại Web một mình |
| `dotnet test tests/QuanLyChoThuePhongTroWeb.Web.IntegrationTests` (một mình, DB test mới) | PASS 658/658 (2 phút 33 giây) | 26/10/2026, mã `3de7181`. Lượt Web đầy đủ đầu tiên PASS sau các sửa T2, T4, `PaymentUrl`, bảng 375 px |
| `dotnet ef migrations has-pending-model-changes` | PASS: "No changes have been made to the model since the last migration." | 26/10/2026, mã `3de7181`. `git diff master...HEAD` rỗng trên Domain, `Persistence/Configurations`, `Persistence/Migrations`, `Program.cs`, `.csproj`, `.gitignore` |

### Test theo spec §11

| Spec | File test |
| --- | --- |
| §11.1 bộ ký, tách `vnp_TxnRef`, đổi giờ | `VnPayProtocolTests`, `VnPayCheckoutProtocolTests` |
| §11.1 tạo link | `VnPayCheckoutServiceTests`, `VnPayCheckoutEdgeCaseTests` |
| §11.1 IPN theo bảng §5.2, trả một phần, gọi lại, dòng đã hủy ghi nhận | `VnPayIpnServiceTests`, `VnPayIpnServiceBoundaryTests`, `InvoiceTransferHandlerVnPayTests`, `IncomingBankTransferVnPayTests` |
| §11.1 thông báo, nhãn, biên lai | `PaymentNotificationFormatterVnPayTests`, `PaymentNotificationDispatcherVnPayTests`, `InvoiceReceiptServiceVnPayTests`, `InvoicePaymentPolicyVnPayTests` |
| §11.1 chặn ghi trùng §6.1 | `InvoicePaymentVnPayGuardTests` |
| Trang kết quả (§4.3, X1–X3) | `VnPayReturnResultServiceTests`, `VnPayCheckoutEdgeCaseTests` |
| Log theo kênh (T1, Q1) | `PaymentLogLabelTests`, `PaymentNotificationWorkerLogLabelTests` |
| Cấu hình §10 | `VnPayOptionsTests` |
| §11.2 Infrastructure: tìm dòng `VNPAY-` kể cả đã xóa mềm, ghi song song cùng `vnp_TransactionNo` | `VnPayLedgerTests` (`VoidedVnPayRow_StillBlocksTheSameTransaction_ButNotTheUndeletedQuery`, `SameTransactionNumber_Concurrent_IsRecordedOnce`, `SameTransactionNumberOnTwoInvoices_Concurrent_UniqueIndexIsTheLastLineOfDefense`) |
| §11.2 Web: IPN, tạo link, trang kết quả, panel, badge quản lý | `VnPayIpnTests`, `VnPayIpnScenarioTests`, `VnPayCheckoutWebTests`, `VnPayCheckoutExtraWebTests` |
| §11.1 "test SePay hiện có PASS, không sửa kỳ vọng" | So với `fbb393a` (commit ngay trước VNPay): file test cũ bị sửa chỉ có hai fake `FakeInvoicePaymentStore.cs`, `PaymentTestHarness.cs`. Ba chỗ sửa trong test VNPay của phần 1 (D2) chỉ thêm đối số constructor và stub, không đổi kỳ vọng. **Ngoại lệ đã duyệt (`3de7181`):** quy tắc T4 đổi kỳ vọng một test SePay, `InvoicePaymentConfirmationSePayGuardTests.ManualCollection_WithExactSePayKeyTyped_IsStillRejected_ByTheSystemPrefixRule` (mã `SEPAY-…` nhập tay vẫn bị từ chối, nhưng bằng câu báo tiền tố thay cho câu báo trùng) |

## 5. Đối chiếu spec với code (26/10/2026)

- **§9 Kiến trúc:** đều có trong `src/`:
  - Kiểu: `VnPayOptions`, `VnPayProtocol`, `IVnPayPaymentService` (`CreatePaymentUrlAsync`, `HandleIpnAsync`, `GetReturnResultAsync`), `OnlinePaymentChannel`, `VnPayResponseCodes`, `VnPayReturnStatus` (6 trạng thái).
  - Controller và route: `VnPayIpnController` (`vnpay-ipn`), `POST .../ThanhToan/VnPay/{hoaDonId}`, `GET .../ThanhToan/VnPay/KetQua`.
  - Panel và nhãn: `vnPayEnabled` trong JSON panel, nhãn "Thẻ ATM / Visa / MasterCard (VNPay)", tiêu đề cảnh báo "THANH TOÁN VNPAY CẦN HOÀN TIỀN".
- **§9.2:** `AddInfrastructure` bind `VnPayOptions` và gọi `Validate()`.
- **§9.3:** spec chỉ nói thêm tab vào `khachthue-thanhtoan.js`. Code thêm `khachthue-vnpay.js` cho POST và SignalR theo D1, vì test bảo mật cấm POST trong file panel; lệch này đã được duyệt.
- **§9.1:** spec để mở cách tách phần ghi sổ dùng chung. Plan §2.1 chọn dùng chung `InvoiceTransferHandler` qua `IncomingBankTransfer.Kenh`, không tách bộ ghi sổ riêng.
- **§10:** khối `VnPay` trong `appsettings.example.json` khớp spec từng khóa và giá trị mẫu. So với `fbb393a`, file này chỉ thêm khối đó.
- **Hành vi (rà lại sau `3de7181`):** IPN theo đúng thứ tự bảng §5.2, kể cả bước 4b (`GetTargetByHoaDonAsync` không lọc xóa mềm, nên hóa đơn đã xóa/hủy đi tiếp tới bước 6, trả `02` kèm cảnh báo). Tạo link và trang kết quả chỉ đọc; `nguoiThueId` lấy từ claim. Quy tắc T4 (`ValidateMaGiaoDich`) chỉ được gọi ở thu thủ công và xác nhận minh chứng, không chặn luồng hệ thống tự ghi. Tab VNPay chỉ hiện khi `coTheThanhToan` (X5). Không thấy lệch hành vi; các lệch tìm thấy đều ở tài liệu (mục 6).
- **Schema:** `git diff master` rỗng trên `Persistence/Migrations`, `Persistence/Configurations`, `ApplicationDbContext.cs` và `Domain`.
- **File cấm:** so với `master`, không sửa `Program.cs`, `.csproj`, `.gitignore`; `appsettings*.json` chỉ có `appsettings.example.json` (được plan §1 cho phép).

## 6. Đối chiếu tài liệu

| Tài liệu | Kết quả |
| --- | --- |
| [README.md](README.md) | Khớp: các checkbox tính năng đã tick, kể cả V01–V07 |
| [plan.md](plan.md) | Khớp: trạng thái, quyết định và số test của cả hai phần, kèm ghi chú sau review |
| [spec.md](spec.md) §16 | Khớp: có B1–B5' và X1–X5 |
| [spec.md](spec.md) §15 | Sau lần đối chiếu này: đã tick hướng dẫn thiết lập và mục cập nhật README/danh mục/`AGENTS.md`. Mục "§11.1, §11.2 PASS" đã tick: build 0 lỗi; Domain, Application, Infrastructure PASS trong lượt toàn solution; Web PASS 657/657 khi chạy lại một mình sau khi sửa fixture; `has-pending-model-changes` PASS (mục 4). Mục V01–V07 đã tick sau khi thử sandbox; đủ 4/4 |
| [../huong-dan-thiet-lap.md](../huong-dan-thiet-lap.md) mục 9 | Đủ 9.1–9.8. V01–V07 khớp spec §11.3, viết chi tiết hơn (V03 có cách dựng tình huống, V04 tắt/bật ngrok). Khóa cấu hình ở 9.3 khớp §10 |
| `docs/features/README.md`, `AGENTS.md` | Khớp trạng thái hiện tại và đường dẫn mới `thanh-toan-truc-tuyen/vnpay/` |

**Lệch tìm thấy, đã sửa 26/10/2026 (người dùng duyệt):**

1. Spec §13 ghi về `vnp_IpAddr` qua ngrok: "Sandbox chấp nhận; chạy thật cần xem lại". Đã sửa spec §13 và hướng dẫn 9.4 thành "đã thử ở V01": lần tạo link 18:42:52 đi qua domain ngrok (log ngrok) nên `vnp_IpAddr` là IP tunnel, ReturnUrl là `http://{domain}`, và VNPay chấp nhận.
2. Lệnh ngrok viết hai kiểu: spec §12 (và spec SePay) dùng `ngrok http --url={domain-tĩnh} 5009`, còn hướng dẫn mục 4 và 9.2 dùng `ngrok http 5009 --url=https://{domain-tĩnh}`. Đã sửa hai spec theo kiểu của hướng dẫn.

**Lệch tìm thấy khi rà lại sau `3de7181`, đã sửa 26/10/2026 (người dùng duyệt), chỉ tài liệu và một dòng comment:**

1. Trạng thái cũ: mục 7 của file này ghi "Chưa thử"; mục 6 ghi README "trừ V01–V07" và spec §15 "V01–V07 chưa tick"; `README.md`, `plan.md`, `docs/features/README.md` ghi "chưa kiểm 375 px" và chưa nhắc `3de7181`; báo cáo ngày ghi "T2, T4 chưa làm".
2. Cách encode chữ ký còn ghi "chưa chắc/chưa xác nhận" ở spec §5.3, comment `VnPayProtocol.BuildSignData` và dòng V07 của hướng dẫn, dù V01 đã xác nhận.
3. Quyết định X1–X5 chỉ nằm ở spec §16 và plan §4: đã đưa vào §4.1 (X5), §4.2 (X4), bảng §4.3 (X1, X2, X3).
4. Mục 4 ghi "test SePay không đổi kỳ vọng", nhưng T4 đã đổi kỳ vọng một test SePay: ghi thành ngoại lệ đã duyệt.

## 7. Thử tay (spec §11.3)

**Đã thử 26/10/2026.** Người dùng làm theo hướng dẫn mục 9 sau khi tự đăng ký sandbox (`TmnCode`, `HashSecret`, thẻ test), điền `appsettings.json`, mở ngrok và khai URL IPN.

| Mã | Trạng thái |
| --- | --- |
| V01 | **PASS** (26/10/2026 18:42–18:43, hóa đơn 12, `vnp_Amount=252800000` tức 2.528.000đ). Log ngrok: tạo link 200 → VNPay gọi `GET /api/webhooks/payment/vnpay-ipn`, trả `{"RspCode":"00","Message":"Confirm Success"}` → khách về trang kết quả với mã `00`. Người dùng báo: hóa đơn sang "Đã thanh toán", có email biên lai, Telegram, thông báo cho nhân viên, và cổng quản lý có badge **VNPay** kèm `TXN {mã}`. Lần trước đó (18:08) IPN không tới vì chưa khai IPN URL (mục 8 ý 7) |
| V02 | **PASS** (18:52, hóa đơn 13). Log ngrok: VNPay gọi IPN với `vnp_ResponseCode=24`, web trả `00 Confirm Success` mà không ghi sổ (đúng dòng 4 bảng §5.2); khách về trang kết quả với mã `24`. Người dùng báo trang hiện đã hủy |
| V03–V06 | **PASS theo người dùng báo** (26/10/2026). Agent không đối chiếu được: log ngrok chỉ còn request trong khoảng 18:42–18:52, trong đó chỉ có V01 và V02 |
| V07 (cách encode chữ ký) | **PASS theo V01**: VNPay chấp nhận link do `VnPayProtocol` ký, và IPN của VNPay qua được bước kiểm chữ ký (không trả `97`). Mới có một giao dịch, nội dung đơn ASCII |
| Giao diện 375 px, sáng và tối (panel hai tab, trang kết quả) | **Đã kiểm 26/10/2026** trên trình duyệt trong app, giả lập 375×812 (Android), DB phát triển, hóa đơn tháng 4 còn nợ 2.444.500đ, `VnPay:Enabled` bật. Panel (sau khi bỏ phần đệm lỗi, xem dưới): không tràn ngang, hai tab xếp dọc rộng 347 px cao 44 px, nút "Thanh toán qua VNPay" 347×48 px; tối (`localStorage.tablerTheme = dark`, đã trả lại) đọc được. Trang kết quả: chỉ kiểm trạng thái "Không xác thực" (không có chữ ký VNPay để dựng trạng thái khác), sáng/tối không tràn, nút 240×48 px. **Phát hiện:** (1) bảng danh sách hóa đơn khách `#hoaDonTable` rộng 682 px làm trang rộng 718 px; khi mở modal, Bootstrap bù "thanh cuộn" `padding-right: 343px` nên modal chi tiết (cả panel thanh toán) chỉ còn rộng 32 px; lỗi có từ trước VNPay. **Đã sửa 26/10/2026** (người dùng chọn): khung bảng gắn class `kt-hoadon-list-scroll`, `hoadon.css` cho riêng class này `overflow-x: auto !important` (thắng rule `overflow: visible !important` của `site.css`); kiểm lại 375 px: trang 375 px, bảng cuộn trong khung (302/682 px), modal rộng 375 px, padding 0; khách vuốt ngang trong bảng để tới nút "Xem chi tiết". Test `TenantInvoiceList_ScrollsInsideItsOwnBox_SoTheDetailModalKeepsTheFullWidth`; Web lọc thanh toán khách 96/96 PASS. Chưa sửa (người dùng để sau): (2) tab đang chọn chỉ khác bằng nền `#f9fafb` và viền nhạt ở chế độ sáng; chữ tab không chọn ở chế độ tối `#6b7280` trên `#1f2937` tương phản khoảng 3:1; (3) DataTables của trang này còn chữ tiếng Anh ("Show 10 entries", "Search:", "Previous/Next") |

## 8. Rủi ro và việc còn mở

1. ~~Chữ ký và tham số tạo link chưa được VNPay thật chấp nhận~~: sandbox đã chấp nhận ở V01 (26/10/2026). Mới có một giao dịch, chưa thử ở production.
2. **ReturnUrl và cookie:** web không đọc forwarded headers. Mở qua domain ngrok thì ReturnUrl dựng ra là `http://`; nên mở bằng `http://localhost:5009` (hướng dẫn 9.4).
3. **Không đối soát bù** (Q4): IPN bị lỡ sau 10 lần gọi lại thì giao dịch không được ghi. Admin phải thu thủ công, và lưu ý mục 9.7.
4. **Hàng đợi thông báo nằm trong bộ nhớ** như đợt 1: khởi động lại có thể mất tin, bù bằng "Gửi lại biên lai".
5. T2, T4, `BuildPaymentUrl` đã làm; hạn mức tối thiểu không làm theo người dùng (mục 3).
6. ~~Test Web phụ thuộc `appsettings.json` của máy chạy test~~: **đã sửa** 26/10/2026, `CustomWebApplicationFactory` cố định `VnPayOptions` (mục 4).
7. **Thử sandbox lần đầu, 26/10/2026, 18:08–18:09:** VNPay báo thành công (trang kết quả nhận `vnp_ResponseCode=00`, giao dịch hiện trên dashboard VNPay) nhưng hóa đơn chưa được ghi sổ. Log ngrok không có request nào tới `/api/webhooks/payment/vnpay-ipn`, nghĩa là VNPay chưa gọi IPN, do chưa khai IPN URL trên cổng merchant sandbox. Sau khi người dùng khai URL, lần thử 18:42 ghi sổ đúng (V01, mục 7).
8. **Giao dịch sandbox lỡ IPN lúc 18:08** (`HD12_639286096935264636`) đã trả tiền trên VNPay nhưng không ghi sổ. Nếu sau này VNPay gọi lại IPN cho giao dịch đó, hóa đơn 12 đã trả đủ nên hệ thống sẽ trả `02` và gửi Telegram "cần hoàn tiền". Đây là hành vi đúng; với tiền sandbox thì bỏ qua.
9. Phần 2 đã commit 26/10/2026 (cùng gộp thư mục tài liệu, review.md, sửa fixture); chưa push.
10. **DB test bị phình:** từ 26/10/2026 cả lượt Web chạy một mình cũng FAIL hàng loạt. Test đầu tiên FAIL là truy vấn đọc của `ThongBaoChiNhanhTests` (đếm `thong_bao` theo nội dung) hết thời gian 30 giây; sau đó factory dùng chung hỏng nên các test sau FAIL trong 1 ms. Bảng `thong_bao` của DB test có khoảng 65 triệu dòng, 22 GB, và mỗi lượt test lại thêm vào. **Đã dọn 26/10/2026** (người dùng chọn): xóa và tạo lại DB test; sau đó toàn bộ test PASS (mục 4). **Gốc rễ chưa sửa** (người dùng để sau), nên bảng sẽ phình lại theo số lượt chạy: test Web tạo tài khoản Admin mà không dọn; `ThongBaoStore` gửi thông báo cho mọi Admin đang hoạt động, nên mỗi thông báo nhân theo số Admin tích lũy; `ThongBaoChiNhanhTests` dùng `NoiDung.Contains(...)`, quét toàn bảng.
