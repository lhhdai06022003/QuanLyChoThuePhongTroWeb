# Giao việc đợt 4 và đợt 5 cho agent thực thi

Người lập kế hoạch và nghiệm thu: phiên Fable (13/11/2026). Người thực thi: một agent khác, chạy trong repo này. Mỗi lượt chạy làm **một** đợt rồi dừng.

## 1. Trình tự

1. Người dùng mở một phiên agent mới ở thư mục gốc repo, dán lời giao việc ở mục 2 với `ĐỢT: 4`.
2. Agent làm theo kế hoạch đợt 4, viết `.bangiao/giao-dien/bao-cao-dot-4.md`, dừng.
3. Người dùng quay lại phiên nghiệm thu, nói "nghiệm thu đợt 4". Người nghiệm thu đọc báo cáo, tự chạy lại các lệnh kiểm, mở trình duyệt, rồi viết `.bangiao/giao-dien/nghiem-thu-dot-4.md` với dòng đầu `NGHIEM THU: DAT` hoặc `NGHIEM THU: CAN SUA` kèm danh sách việc phải sửa.
4. `CAN SUA`: người dùng dán lời sửa ở mục 4 cho agent thực thi; lặp lại bước 3. Tối đa hai vòng sửa cho một mục, sau đó người nghiệm thu tự sửa.
5. `DAT`: lặp lại từ bước 1 với `ĐỢT: 5`.
6. Đợt 5 đạt: người nghiệm thu cập nhật `README.md` của module và `.agents/hien-trang.md`, rồi hỏi người dùng về việc gộp nhánh.

## 2. Lời giao việc (dán nguyên văn, chỉ đổi số đợt)

```text
ĐỢT: 4

Bạn là agent thực thi đợt thiết kế lại giao diện ghi ở dòng "ĐỢT" phía trên, trong repo QuanLyChoThuePhongTroWeb (TroSync), nhánh feature/thiet-ke-lai-giao-dien. Thiết kế đã được người dùng duyệt. Việc của bạn là làm đúng kế hoạch, tự kiểm, viết báo cáo rồi dừng. Một phiên khác sẽ nghiệm thu bằng cách tự chạy lại lệnh và mở trình duyệt, nên báo cáo phải đúng sự thật.

Đọc trước, theo thứ tự:
1. AGENTS.md
2. .agents/skills/giao-dien-quan-ly/SKILL.md
3. docs/features/thiet-ke-lai-giao-dien/README.md
4. Đợt 4: spec-dot-4-phong-nguoi-thue-hop-dong.md và plan-dot-4-phong-nguoi-thue-hop-dong.md.
   Đợt 5: spec-dot-5-cac-man-con-lai.md, plan-dot-5-cac-man-con-lai.md, mục 1 và mục 2 của plan-dot-4-phong-nguoi-thue-hop-dong.md, và .bangiao/giao-dien/nghiem-thu-dot-4.md (dòng đầu phải là "NGHIEM THU: DAT"; không phải thì dừng và báo).
   Các file này nằm trong docs/features/thiet-ke-lai-giao-dien/.
5. docs/features/thiet-ke-lai-giao-dien/giao-viec-dot-4-5.md mục 3 (mẫu báo cáo).

Cách làm:
- Làm các việc V1, V2... theo đúng thứ tự trong plan. Mỗi việc: đọc trọn file, sửa, build, chạy script soát, kiểm trên trình duyệt phần "Kiểm" của việc đó, chạy lệnh kiểm id không mất, rồi commit cục bộ với message ghi trong plan. Không push, không merge, không đổi nhánh. Commit message không có dòng Co-Authored-By hay ghi công AI.
- Chỉ sửa file mà việc đó liệt kê. Không sửa file .cs trong src/, không đổi id, name, data-*, URL, điều kiện hiện nút, phép tính, thứ tự cột DataTables. Danh sách cấm đầy đủ ở mục 1 của plan đợt 4.
- Thiết kế đã chốt. Nếu thấy plan sai so với code thật, hoặc phải vi phạm một điều cấm mới làm được: không tự quyết. Dừng việc đó, ghi vào báo cáo mục "Cần người dùng quyết" (plan viết gì, code thật ra sao, bạn đề xuất gì), rồi làm việc tiếp theo không phụ thuộc vào nó.
- Một việc sửa hai vòng chưa đạt, hoặc FAIL không tìm ra nguyên nhân sau một lượt điều tra: dừng việc đó, ghi hồ sơ vào báo cáo (đã thử gì, lỗi nguyên văn, file đang dở).
- Kiểm trình duyệt bằng cấu hình web-test-db trong .claude/launch.json, đăng nhập tài khoản test (ghi nhớ kiem-trinh-duyet-db-test). Không đọc hay sửa appsettings.json, không viết mật khẩu ra chat hay báo cáo. Sửa view xong phải khởi động lại web mới thấy.
- Chạy test cần DB theo skill chay-test-tich-hop. Không chạy hai lượt dotnet test cùng lúc. Lệnh nào không chạy được thì ghi CHƯA CHẠY kèm lý do, không ghi PASS.
- Trả lời và viết tài liệu bằng tiếng Việt.

Xong đợt:
- Điền mục 4 và 5 của spec đợt đó, commit.
- Viết .bangiao/giao-dien/bao-cao-dot-<số đợt>.md theo mẫu.
- Dừng. Không làm đợt kế tiếp, không sửa README của module, không sửa .agents/.
- Tin nhắn cuối gửi người dùng: mốc và HEAD, số việc xong trên tổng số, bảng kết quả lệnh (PASS / FAIL / CHƯA CHẠY / KHÔNG ÁP DỤNG), các mục "Cần người dùng quyết", đường dẫn báo cáo.
```

## 3. Mẫu báo cáo (`.bangiao/giao-dien/bao-cao-dot-N.md`)

```markdown
# Báo cáo đợt N

- Mốc: `<hash>` · HEAD: `<hash>` · Ngày: dd/MM/yyyy
- Trạng thái: XONG / XONG MỘT PHẦN (ghi việc còn dở)

## 1. Từng việc

| Việc | Commit | Trạng thái | File đã sửa | Ghi chú |
| --- | --- | --- | --- | --- |
| V1 | `abc1234` | Xong | ... | ... |

## 2. Lệnh đã chạy

| Lệnh | Kết quả | Số liệu |
| --- | --- | --- |
| `dotnet build QuanLyChoThuePhongTroWeb.sln` | PASS / FAIL | cảnh báo mới: n |
| Domain.UnitTests | | n/n |
| Application.UnitTests | | n/n |
| Web.IntegrationTests toàn bộ | | n/n, thời gian |
| Infrastructure.IntegrationTests | KHÔNG ÁP DỤNG (không đổi Infrastructure) | |
| Script soát trên file đã sửa | | n LỖI, n XEM LẠI (n có từ trước) |
| `git diff --check <mốc>` | | |
| Kiểm `id` không mất | | từng view: rỗng, hoặc dòng in ra kèm giải thích |
| `diff` khối script Thanh lý, Gia hạn (đợt 4) | | GIONG / khác |

Dán nguyên văn dòng tổng kết của mỗi lệnh test.

## 3. Bảng ngày giờ (Q11)

| File:dòng | Trường | Mẫu giá trị thật | Có `Z`/offset | Cách đã chọn | Trước → sau trên cùng bản ghi |
| --- | --- | --- | --- | --- | --- |

## 4. Kiểm trình duyệt

| Màn | Vai trò | 1366 sáng | 1366 tối | 375 sáng | 375 tối | `scrollWidth` ở 375 | Console | Thao tác đã thử |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |

Dữ liệu thử đã tạo trên DB test (phòng, người thuê, hợp đồng, tài khoản...): liệt kê mã để người nghiệm thu mở lại.

Ảnh: tên file trong `E:\UTE\TieuLuanChuyenNganh\hinhanhprojectdemo`.

## 5. Lệch so với kế hoạch

Từng chỗ: plan viết gì, đã làm gì, vì sao. Không lệch thì ghi "Không".

## 6. Chuỗi và lớp giữ lại vì test đang dò

| Chuỗi hoặc lớp | File | Test |
| --- | --- | --- |

## 7. Cần người dùng quyết

Từng mục: bối cảnh, lựa chọn, đề xuất. Không có thì ghi "Không".

## 8. Chuyển cho thuan (chỉ đợt 5)

Từng vấn đề của ba màn Đón khách không được sửa: ảnh, file:dòng, mô tả.

## 9. Còn để ý

Những gì thấy mà không thuộc phạm vi.
```

## 4. Lời sửa sau nghiệm thu (khi kết quả là CAN SUA)

```text
Nghiệm thu đợt <N> trả về CAN SUA. Đọc .bangiao/giao-dien/nghiem-thu-dot-<N>.md, sửa đúng các mục trong danh sách "Phải sửa" (mỗi mục một commit cục bộ, message bắt đầu bằng "fix(giao-dien):"), không sửa gì ngoài danh sách. Vẫn theo mọi ràng buộc của plan. Xong thì thêm mục "Vòng sửa <k>" vào cuối .bangiao/giao-dien/bao-cao-dot-<N>.md: từng mục đã sửa thế nào, commit nào, lệnh kiểm nào đã chạy lại kèm kết quả. Rồi dừng.
```

## 5. Người nghiệm thu làm gì

1. Đọc báo cáo; đối chiếu `git log <mốc>..HEAD` với bảng việc.
2. Chạy lại: `git diff --stat <mốc>` (phạm vi), lệnh kiểm `id`, lệnh `diff` khối script, script soát, build, test (unit; Web.IntegrationTests toàn bộ nếu báo cáo ghi PASS thì chạy lại một lần để xác nhận).
3. Đọc diff của các file đụng tiền: `ThanhLy.cshtml`, `GiaHan.cshtml`, modal thu tiền nhanh trong `QuanLyPhongTro.cshtml`, `Areas/KhachThue/Views/HoaDon/Index.cshtml`.
4. Mở trình duyệt trên DB test: chọn ngẫu nhiên ít nhất một nửa số màn, ở 375px và chế độ tối; lặp lại các thao tác trong mục "Kiểm" của từng việc.
5. So thiết kế với mục 2 của spec.
6. Viết `.bangiao/giao-dien/nghiem-thu-dot-N.md`: dòng đầu `NGHIEM THU: DAT` hoặc `NGHIEM THU: CAN SUA`; bảng từng tiêu chí kèm bằng chứng; danh sách "Phải sửa" (mã S1, S2..., file:dòng, hiện tượng, kết quả mong đợi); ghi chú "áp cho đợt 5" nếu có.
