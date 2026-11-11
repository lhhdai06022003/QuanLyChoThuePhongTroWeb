# Spec đợt 1: nền chung của giao diện mới

- Trạng thái: **đã thực thi xong trên nhánh** (11/11/2026, chưa gộp `master`). Ngày 11/11/2026 người dùng bảo lập kế hoạch và giao agent thực thi mà không yêu cầu sửa spec; bốn điểm ở mục 11 lấy theo đề xuất (điểm 4, ngày demo, vẫn chưa biết). Kế hoạch: [plan-dot-1-nen-chung.md](plan-dot-1-nen-chung.md)
- Ngày: 11/11/2026
- Nhánh: `feature/thiet-ke-lai-giao-dien`
- Phạm vi: lớp vỏ của cổng quản lý và cổng khách thuê (layout, menu, header, màu, font, nút trợ lý AI). Không sửa thân của từng màn; việc đó thuộc đợt 2 đến 5.
- Nguồn quyết định: mục ghi **(NgD)** là người dùng đã duyệt ngày 11/11/2026; mục ghi **(đề xuất)** là Claude đề xuất, người dùng duyệt cùng spec này.

## 1. Vì sao làm

Người dùng muốn ba nhóm (chủ trọ, nhân viên, khách thuê) nhìn là dùng được **(NgD)**. Hiện trạng cản điều đó ở lớp vỏ:

| Chỗ | Hiện trạng | Hậu quả |
| --- | --- | --- |
| Menu quản lý (`Services/MenuService.cs`, `Views/Shared/_MenuItem.cshtml`) | 16 mục trong một nhóm xổ xuống "Quản lý nhà trọ", xếp không theo trình tự công việc | Phải đọc cả danh sách mới tìm ra mục; nhóm đóng lại khi sang màn ngoài nhóm |
| Huy hiệu "Việc cần làm" | Số nhiều chữ số bị cắt ở mép menu ("1411") | Đọc sai số việc |
| Header (`Views/Shared/_Header.cshtml`) | `d-none d-lg-flex`: dưới 992px không có chuông thông báo, không đổi được sáng/tối, không có "Đổi mật khẩu" | Nhân viên dùng điện thoại mất ba chức năng |
| Màu | Tabler mặc định `#066fd1`, `site.css` gõ cứng `#206bc4`, logo `#2563eb`, màn Hóa đơn tự đổi sang xanh ngọc `#0f6b63`; 41 chỗ gõ cứng màu xanh cũ trong 11 file | Mỗi màn một sắc xanh, nút chính không nhất quán |
| Font | Inter | Dấu tiếng Việt chồng (ễ, ặ, ữ) sát dòng trên ở cỡ nhỏ |
| Cổng khách thuê (`_SidebarKhachThue.cshtml`) | Tên "Cổng khách thuê", icon Font Awesome, `font-weight-bold` (không có tác dụng trong Tabler 1.4), `<title>` cố định "Khách Thuê - Quản Lý Trọ" | Trông như sản phẩm khác |
| Nút trợ lý AI (`ai-chat-widget.css`) | Cố định `bottom: 25px; right: 25px`, che dòng tổng và nút phân trang của bảng; trên điện thoại khách thuê đè lên thanh tab dưới | Không bấm được thứ nằm dưới nút |

## 2. Mục tiêu, không làm, thất bại

**Mục tiêu**
1. Một bộ màu và một font cho cả hai cổng, đúng ở chế độ sáng và tối.
2. Menu quản lý nhóm theo việc, luôn mở, không đổi quyền xem của ai.
3. Dưới 992px vẫn có thông báo, đổi sáng/tối, đổi mật khẩu.
4. Cổng khách thuê mang tên và logo TroSync, dùng cùng bộ icon.
5. Nút trợ lý AI không che nội dung và không đè thanh tab.
6. Skill `giao-dien-quan-ly` và script soát khớp với nền mới, để các đợt sau (kể cả subagent) làm theo một chuẩn.

**Không làm (đợt này)**
- Sửa thân từng màn: tiêu đề trang, ô số liệu, bảng, bộ lọc, màu gõ cứng trong CSS riêng của màn (41 chỗ, trừ phần trong `site.css`). Sau đợt 1 các màn vẫn giữ bố cục cũ, chỉ đổi màu nền, font, menu, header.
- Thanh tab dưới cho nhân viên, thanh thu tiền, bảng thành thẻ trên điện thoại (đợt 2 trở đi).
- Đổi controller, Application, Domain, schema, `Program.cs`, `.csproj`, `appsettings*`.
- Cổng khách vãng lai. `wwwroot/css/trosync-tokens.css` **không sửa** vì `_PublicRoomLayout.cshtml` cũng nạp file này.
- Bỏ Font Awesome khỏi layout (quyết định 08/11/2026 giữ; các màn con của khách thuê còn dùng).
- Thêm thư viện JS hoặc gói NuGet.

**Coi là thất bại dù chạy được**
- Nhân viên thấy trong menu một mục trước đây chỉ Admin thấy, hoặc mất một mục trước đây thấy.
- Một màn bất kỳ của cổng khách vãng lai đổi màu, font hay bố cục.
- Chữ không đọc được trên nền ở chế độ tối, hoặc có thanh cuộn ngang ở 375px do lớp vỏ gây ra.
- Một `id` mà JS đang dùng (`badge-viec-can-lam`, `badge-thong-bao`, `list-thong-bao`, `btn-danh-dau-tat-ca`, `sidebar-menu`, `logoutForm`…) biến mất hoặc bị trùng.

## 3. Màu và font (đề xuất)

File mới `wwwroot/css/trosync-nen.css`, nạp trong `_Layout.cshtml` và `_LayoutKhachThue.cshtml` ngay sau `trosync-tokens.css`. File này đặt giá trị cho biến của Tabler (`--tblr-primary`, `--tblr-primary-rgb`, `--tblr-primary-darken`, `--tblr-link-color`, `--tblr-body-bg`, `--tblr-body-color`, `--tblr-border-color`, `--tblr-bg-surface`, `--tblr-bg-surface-secondary`, `--tblr-font-sans-serif`…) ở `:root` và `[data-bs-theme="dark"]`. Tabler 1.4 vẽ nút, liên kết, badge qua các biến này (đã kiểm: `.btn-primary` dùng `var(--tblr-primary)`), nên không cần ghi đè từng lớp.

| Vai trò | Sáng | Tối | Dùng cho |
| --- | --- | --- | --- |
| Nền trang | `#f3f5f8` | `#10161f` | `body` |
| Mặt thẻ | `#ffffff` | `#18212d` | card, menu, header |
| Chữ chính | `#16202e` | `#e8edf3` | nội dung |
| Chữ phụ | `#5b6676` | `#9aa7b7` | nhãn, chú thích |
| Viền | `#dde3ea` | `#2a3545` | card, bảng, ô nhập |
| Thương hiệu | `#1d5fd0` | tô nút `#2a66d4`, chữ và liên kết `#7fb0f7` (tách hai sắc để chữ trắng trên nút và chữ xanh trên nền tối đều đạt tương phản) | nút chính, liên kết, mục menu đang chọn |
| Đã thu, đạt | `#176f43` (đậm hơn bản đầu `#1a7f4b` để đạt 4,5:1 trên nền nhạt) | `#5fcf96` | chữ và badge trạng thái tốt |
| Chờ, sắp hạn | `#8a570c` | `#f0bd62` | chữ và badge cảnh báo |
| Quá hạn, lỗi | `#b02e24` | `#f08a80` | chữ và badge nguy hiểm |

- Giữ màu xanh làm thương hiệu (gần `#206bc4` cũ và logo) để các màn chưa làm lại không lệch tông trong lúc chờ đợt sau.
- Mọi cặp chữ trên nền phải đạt tương phản 4,5:1; kế hoạch kiểm lại bằng số trước khi chốt giá trị, lệch thì chỉnh sắc độ chứ không đổi vai trò.
- Bảng màu badge trạng thái trong skill (`success-lt`, `warning-lt`, `danger-lt`, `secondary-lt`, `info-lt`) giữ nguyên ý nghĩa.
- Font: **Be Vietnam Pro** (400, 500, 600) lấy từ Google Fonts như Inter hiện nay, thay dòng `<link>` Inter trong hai layout và biến font ở đầu `site.css`. Số liệu vẫn dùng `.num-tabular`. Lưu ý: như hiện tại, máy demo không có mạng thì trình duyệt rơi về font hệ thống.
- Màn đăng nhập `Areas/QuanLyNhaTro/Views/NguoiDung/DangNhap.cshtml` tự nạp `site.css` và Inter (không qua layout): đổi dòng font và nạp thêm `trosync-nen.css` để không lệch font; bố cục màn này để đợt 5.
- `site.css` (hai layout và màn đăng nhập nạp; `_PublicRoomLayout` không nạp): thay các giá trị `#206bc4` và `rgba(32, 107, 196, …)` bằng biến; đầu cột bảng bỏ `text-transform: uppercase` và giãn chữ, dùng cỡ 0,8125rem đậm 500 (chữ thường dễ đọc hơn với tiếng Việt có dấu); card bỏ bóng, giữ viền theo biến, bo 8px (`--ts-radius-admin`).
- SweetAlert trong hai layout: màu nút gõ cứng `#3085d6`, `#d33`, `#6c757d` đổi sang đọc biến thương hiệu, nguy hiểm, chữ phụ.

## 4. Menu cổng quản lý

Thứ tự và nhóm **(NgD duyệt cách nhóm; tên nhóm và vị trí từng mục là đề xuất)**:

| Nhóm | Mục (đường dẫn giữ nguyên) | Ai thấy |
| --- | --- | --- |
| (không nhãn) | Bảng điều khiển; Việc cần làm (kèm huy hiệu) | Admin, nhân viên |
| Phòng và khách | Phòng trọ; Người thuê; Hợp đồng | Admin, nhân viên |
| Thu tiền | Điện nước; Hóa đơn; Đối chiếu thanh toán; Lịch sử thanh toán; Giao dịch SePay | Giao dịch SePay chỉ Admin (như hiện tại) |
| Khách mới | Tin phòng công khai; Lịch xem phòng; Đặt cọc phòng | Admin, nhân viên |
| Vận hành | Sự cố và sửa chữa; Báo cáo | Admin, nhân viên |
| Thiết lập | Chi nhánh; Dịch vụ; Điều khoản hợp đồng; Tài khoản; Phân công chi nhánh | Chi nhánh, Tài khoản, Phân công chi nhánh chỉ Admin (như hiện tại); nhân viên thấy Dịch vụ và Điều khoản hợp đồng |

- **Sửa so với bản mẫu đã trình:** bản mẫu ghi nhóm Thiết lập "chỉ chủ trọ". Thực tế Dịch vụ và Điều khoản hợp đồng đang mở cho nhân viên; spec này không đổi quyền nên nhân viên vẫn thấy nhóm Thiết lập với hai mục đó.
- Đổi chữ: "Sự cố & Sửa chữa" thành "Sự cố và sửa chữa"; "Quản lý tài khoản" thành "Tài khoản"; nhóm "Quản lý người dùng" và "Quản lý nhà trọ" không còn. Các tên khác giữ nguyên (test `Menu_DoesNotUseTheEnglishDashboardLabel` cần `Name = "Bảng điều khiển"`).
- `MenuService.GetMenu()` vẫn trả `List<MenuItem>` hai cấp; `MenuItem` không thêm thuộc tính. `_MenuItem.cshtml` vẽ mục có con thành **nhãn nhóm + các mục luôn hiện** (không còn `dropdown-toggle`), nhóm không có mục nào người dùng được thấy thì ẩn cả nhãn (giữ logic `IsVisibleFor` hiện có).
- Mục đang mở: nền nhạt màu thương hiệu, chữ thương hiệu đậm 500; logic `IsActive` và `ActivePrefixes` giữ nguyên. Khi tải trang, mục đang mở được cuộn vào tầm nhìn nếu nằm dưới mép menu (vài dòng trong `site.js`).
- Huy hiệu việc cần làm nằm trong dòng, không bị cắt với số 4 chữ số.
- Dưới 992px menu vẫn thu vào nút ba gạch như hiện nay (thanh tab dưới cho nhân viên là việc của đợt 2).

## 5. Header và thanh trên của điện thoại (đề xuất)

- Từ 992px: giữ header hiện có (đổi sáng/tối, chuông, tên và vai trò), bỏ `shadow-sm`, chỉ còn viền dưới.
- Dưới 992px, thanh trên trong `_Sidebar.cshtml` và `_SidebarKhachThue.cshtml` có thêm:
  - Nút chuông là **liên kết** tới trang Thông báo của cổng tương ứng, kèm chấm số chưa đọc. Không lặp lại khối thả xuống của header để không trùng `id`; `thongbao.js` cập nhật thêm phần tử theo lớp `js-badge-thong-bao`.
  - Trong menu người dùng: "Chế độ tối/sáng", "Đổi mật khẩu" (cổng quản lý), "Đăng xuất".
- `_SidebarKhachThue.cshtml` hiện không có menu người dùng trên điện thoại: thêm như cổng quản lý (đăng xuất dùng form POST có token như hiện có).

## 6. Vỏ cổng khách thuê (đề xuất, theo hướng 6 đã duyệt)

- Logo và chữ TroSync như cổng quản lý, dòng phụ "Cổng khách thuê".
- Icon menu đổi sang `ti ti-*`; bỏ `font-weight-bold` và `style` gõ cứng trong `_SidebarKhachThue.cshtml`.
- Chữ menu viết hoa đầu câu: "Hóa đơn và thanh toán", "Báo sự cố"; các mục và đường dẫn giữ nguyên.
- `<title>`: "{tên màn} | TroSync" như cổng quản lý.
- Thanh tab dưới hiện có (`tenant-bottom-nav`, dưới 768px) giữ nguyên các mục; chỉ đổi màu theo biến. Việc chọn lại mục cho thanh tab thuộc đợt 2.
- **Sửa so với nhận định đã trình:** tôi đã nói khách thuê trên điện thoại "phải mở menu bên để đi lại". Không đúng: cổng khách thuê đã có thanh tab dưới ở dưới 768px. Vấn đề còn lại là nút AI đè lên thanh đó (mục 7).

## 7. Nút trợ lý AI (đề xuất)

- Nút thu còn 48px, cách mép 16px.
- `.page-wrapper` chừa lề dưới bằng chiều cao nút cộng 24px ở cả hai cổng, để dòng cuối và nút phân trang cuộn lên khỏi vùng nút.
- Ở cổng khách thuê dưới 768px, nút và khung trò chuyện nằm trên thanh tab (thanh cao 64px).
- Không đổi `id`, lớp mà `ai-chat-widget.js` dùng; không đổi hành vi trò chuyện.

## 8. Cập nhật chuẩn cho các đợt sau

- `.agents/skills/giao-dien-quan-ly/SKILL.md` và `references/mau-trang.md` (chép sang `.claude/skills/`): ghi bảng màu mục 3, quy tắc "một nút chính mỗi màn dùng `btn-primary`", tiêu đề trang không tô `text-primary`, và khung trang chung sẽ dùng từ đợt 2. Dòng "không tự chế phong cách mới" đổi thành "dùng biến trong `trosync-nen.css`".
- `scripts/soat-giao-dien.js`: thêm mục LỖI cho màu thương hiệu gõ cứng (`#206bc4`, `#066fd1`, `#2563eb`, `rgba(32, 107, 196`) trong dòng mới thêm.
- Các file này nằm ngoài repo dự án (repo `bo-nao-ai-agent`); thao tác bằng `git bn`, chỉ commit khi người dùng bảo.

## 9. File dự kiến đụng tới

| File | Việc |
| --- | --- |
| `wwwroot/css/trosync-nen.css` (mới) | Biến màu, font, sáng và tối |
| `wwwroot/css/site.css` | Dùng biến, kiểu menu nhóm, đầu bảng, card |
| `wwwroot/css/ai-chat-widget.css`, `wwwroot/css/tenant-bento.css` | Vị trí nút AI, màu thanh tab |
| `Views/Shared/_Layout.cshtml`, `Areas/KhachThue/Views/Shared/_LayoutKhachThue.cshtml` | Font, nạp `trosync-nen.css`, màu nút SweetAlert, `<title>` khách thuê |
| `Views/Shared/_Sidebar.cshtml`, `_MenuItem.cshtml`, `_Header.cshtml`, `Areas/KhachThue/Views/Shared/_SidebarKhachThue.cshtml` | Menu nhóm, thanh trên điện thoại, tên TroSync |
| `Areas/QuanLyNhaTro/Views/NguoiDung/DangNhap.cshtml` | Chỉ dòng font và nạp `trosync-nen.css` |
| `Services/MenuService.cs` | Nhóm và thứ tự mục |
| `wwwroot/js/site.js`, `wwwroot/js/thongbao.js` | Cuộn tới mục đang mở; chấm chưa đọc trên điện thoại |
| `tests/QuanLyChoThuePhongTroWeb.Web.IntegrationTests` (test mới) | Xem mục 10 |

Không sửa: `Program.cs`, `.csproj`, `appsettings*`, fixture test, test kiến trúc, `trosync-tokens.css`, mọi view trong `Areas/*/Views/<màn>/` trừ hai dòng nạp CSS của màn đăng nhập. Header giữ các lớp mà test đang kiểm (`badge-notification`, `status-dot`) và nhãn vai trò hiện có.

## 10. Kiểm chứng

| Việc | Cách kiểm |
| --- | --- |
| Quyền menu không đổi | Test Web mới: nhân viên mở Bảng điều khiển thì HTML không có liên kết Chi nhánh, Tài khoản, Phân công chi nhánh, Giao dịch SePay; có Dịch vụ, Điều khoản hợp đồng và đủ các mục còn lại. Admin có đủ 20 mục (bằng số mục hiện tại: 2 mục đầu, 2 mục tài khoản, 16 mục nhà trọ). Danh sách đường dẫn mỗi vai trò thấy phải trùng với trước khi sửa |
| Không vỡ test đang có | `dotnet build`; `Web.IntegrationTests` chạy một mình trên DB `*_test` (đặc biệt `VietnameseUiStaticGuardTests`, `LayoutLibraryStaticGuardTests`, `ViecCanLamEndpointTests`, `BangDieuKhienTheoVaiTroTests`, `AutoInvoiceUiStaticGuardTests`); `Application.UnitTests` |
| Khách vãng lai không đổi | Test tĩnh mới: `_PublicRoomLayout.cshtml` không nạp `trosync-nen.css`; `git diff` không có `trosync-tokens.css`, `guest-*.css`, `khach-vang-lai.css`. Mở `/phong` trước và sau, so ảnh |
| Soát giao diện | `node .agents/skills/giao-dien-quan-ly/scripts/soat-giao-dien.js` trên mọi file đã sửa, 0 LỖI |
| Trình duyệt | Cấu hình `web-test-db`, tài khoản test. Ba vai trò (Admin, nhân viên, khách thuê) × sáng, tối, 375px, trên các màn: Bảng điều khiển, Hóa đơn, Hợp đồng, Phòng trọ, Lịch sử thanh toán, trang chủ và Hóa đơn của khách thuê. Kiểm: menu đúng nhóm, huy hiệu không bị cắt, chữ đọc được, không cuộn ngang do vỏ, nút AI không che dòng cuối, console không lỗi. Ảnh lưu `hinhanhprojectdemo/giao-dien-1-NN-*.png` |
| Bàn giao | `git diff --check`; báo PASS / FAIL / CHƯA CHẠY từng dòng |

## 11. Điểm cần người dùng quyết

Duyệt spec này nghĩa là đồng ý các đề xuất dưới đây; muốn khác thì ghi lại, tôi sửa spec trước khi lập kế hoạch.

1. **Màu thương hiệu** giữ xanh, chốt `#1d5fd0` (mục 3). Phương án khác: đổi hẳn sang tông khác thì các màn chưa làm lại sẽ lệch màu cho tới hết đợt 5.
2. **Vị trí hai mục menu:** Giao dịch SePay nằm trong "Thu tiền"; Dịch vụ và Điều khoản hợp đồng nằm trong "Thiết lập" và nhân viên vẫn thấy (mục 4).
3. **Nút AI** giữ dạng nút nổi, thu nhỏ và chừa lề (mục 7). Phương án khác: đưa thành biểu tượng trên header, phải sửa thêm JS và CSS của khung trò chuyện.
4. **Ngày demo:** chưa biết. Nếu còn dưới một tuần, tôi đề nghị dừng sau đợt 2 và để đợt 3 đến 5 sau buổi demo.
