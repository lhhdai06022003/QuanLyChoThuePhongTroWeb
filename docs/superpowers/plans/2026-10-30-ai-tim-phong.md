# Public guest AI implementation plan

> **For agentic workers:** Use superpowers:executing-plans task by task, with test-driven-development and a final review.

**Goal:** Cho khách hỏi tìm phòng và tư vấn cọc bằng tiếng Việt ngay tại trang công khai.
**Architecture:** Service công khai riêng tái sử dụng Gemini; công cụ chỉ đọc `IPublicRoomService`; API chống CSRF và giới hạn tốc độ; widget nhỏ dùng JS/CSS gốc.
**Tech Stack:** .NET 8 MVC, Gemini adapter hiện có, xUnit, JavaScript/Node test runner.
**Spec:** `docs/superpowers/specs/2026-10-30-ai-tim-phong-design.md`.

## Global constraints

Không thay đổi schema/DB thật. Không mở công cụ quản lý hoặc thông tin cá nhân cho khách. Không tự push/merge. Giữ công việc chưa commit hiện có. Người dùng đã yêu cầu thực hiện phạm vi đề xuất; triển khai trực tiếp trên nhánh tính năng hiện tại.

## Review focus

1. Model gọi công cụ quản lý, yêu cầu ID bị ẩn hoặc giả mạo lịch sử không được lộ thông tin.
2. Phòng bị ẩn giữa lúc chat phải bị loại khỏi thẻ kết quả cuối.
3. Bộ lọc không hợp lệ phải hỏi lại, không bỏ lọc rồi gợi ý phòng sai ngân sách.
4. HTML/URL nguy hiểm từ AI hoặc tiêu đề phòng phải được hiển thị an toàn.
5. Gemini lỗi, hết hạn, vượt tần suất hoặc đóng chat lúc chờ phải khôi phục được giao diện.

## Task 1: Public assistant service

Create `Application/Features/GuestAssistant/{GuestAssistantContracts,GuestAssistantTools,GuestAssistantService}.cs`; register in Application DI.

- [x] Viết test bộ lọc, danh sách giới hạn, cọc chưa cấu hình, công cụ không được phép, giới hạn vòng/lịch sử và lỗi model; xem RED.
- [x] Thực hiện service `IGuestAssistantService.ChatAsync(GuestAssistantRequest?, IReadOnlyCollection<int>?, CancellationToken)` trả `GuestAssistantReply` kèm thẻ từ server.
- [x] Chạy test mới và test trợ lý cũ; xác nhận GREEN.

## Task 2: Public endpoint and widget

Create `Web/Controllers/GuestAssistantController.cs`, `Views/Shared/_GuestAiChat.cshtml`, `wwwroot/{css,js}/guest-ai-chat.*`; modify Program and public layout. Tests: `GuestAssistantEndpointTests.cs` and `tests/browser/guest-ai-chat.test.mjs`.

- [x] Viết test truy cập ẩn danh, CSRF, validation/rate limit và chống HTML; xem RED.
- [x] Thực hiện API + giới hạn 6/phút/IP + widget có gợi ý và thẻ phòng; giữ bộ nhớ chat trong tab.
- [x] Chạy test mới và endpoint AI cũ; xác nhận GREEN.

## Task 3: Verification and documentation

- [x] Kiểm tra build, UI máy tính/điện thoại, phím Escape/focus và lỗi dịch vụ; gọi Gemini thật với câu hỏi không có dữ liệu cá nhân nếu cấu hình sẵn có hoạt động.
- [x] Review riêng phần diff AI, sửa lỗi quan trọng, kiểm tra không có schema/secret thay đổi.
- [x] Ghi hướng dẫn dùng/cấu hình và kết quả kiểm tra. Không commit hoặc push trộn các thay đổi trước đó.

## Progress

- Planning: complete; recommended scope accepted by “thực hiện đi”. Native execution. Current feature checkout is reused to retain uncommitted public-deposit dependencies; no branch/worktree or DB changes.
- Task 1: complete; 120/120 focused Application tests passed after review fixes, including 24 guest cases. Initial 21 guest tests RED before implementation; 3 review regression tests RED before fixes.
- Task 2: complete; final 49/49 focused Web tests passed after review fixes. 10/10 JS tests passed, including retry, reset while pending, close/reopen and ordered room references. Initial endpoint tests 6/6 RED before implementation.
- Independent review: two P2 findings reproduced and fixed: recheck all disclosed rooms across multiple searches, preserve bounded ordered room references for follow-up questions. Remaining usability comments addressed by direct search fallback and JS lifecycle coverage.
- Live Gemini: HTTP 200 with real public room data; query under 3 million for 2 people returned rooms 25 and 32, actual deposits 200000/null. No reservation/payment writes.
- Task 3: complete; native browser verification at 1280×900 and 390×844; public search and follow-up “Phòng đầu tiên cần cọc bao nhiêu?” both returned actual data. Escape closes and restores opener focus. Mobile panel has left 12 px and right 363 px within 375 px content width; CSS corrected for scrollbars. Evidence under the task visualization directory `guest-ai-20261030`.
- Delivery: application remains running at `http://localhost:5010/phong`, `QuanLyPhongTroDb`, initialization/seed and background jobs disabled. Feature branch kept as-is; no commit, push or merge mixes this task with preceding uncommitted deposit work.
