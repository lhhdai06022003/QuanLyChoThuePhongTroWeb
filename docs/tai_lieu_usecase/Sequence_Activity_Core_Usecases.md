# SƠ ĐỒ TUẦN TỰ & HOẠT ĐỘNG: CÁC USE CASE HỆ THỐNG

Tài liệu này chứa toàn bộ mã nguồn vẽ **Sơ đồ tuần tự (Sequence Diagram)** và **Sơ đồ hoạt động (Activity Diagram)** của chức năng Đăng nhập và 4 Use Case cốt lõi. Sơ đồ hoạt động được thiết kế theo luồng ngang (`graph LR`) hoặc dọc tối giản (`graph TD`) để dễ theo dõi và đưa vào báo cáo.

---

## 1. USE CASE XÁC THỰC: ĐĂNG NHẬP HỆ THỐNG

### 1.1. Sơ đồ tuần tự (Sequence Diagram)
Sơ đồ mô tả luồng xác thực tài khoản qua hai tầng băm mật khẩu PBKDF2/SHA256, tự động nâng cấp độ bảo mật băm mật khẩu (Re-hash) và lưu phiên đăng nhập Cookie.

```mermaid
sequenceDiagram
    autonumber
    actor ND as Người dùng (Khách thuê/Nhân viên)
    participant C as AuthController
    participant S as NguoiDungService
    participant DB as DbContext (PostgreSQL)

    ND->>C: Nhập Username/Password & gửi yêu cầu (POST Login)
    activate C
    C->>C: Kiểm tra ModelState.IsValid
    alt Dữ liệu đầu vào thiếu/sai định dạng
        C-->>ND: Trả về trang đăng nhập với thông tin lỗi
    else Dữ liệu hợp lệ
        C->>S: Gọi ValidateUserAsync(username, password)
        activate S
        S->>DB: Truy vấn thông tin người dùng theo Username
        activate DB
        DB-->>S: Trả về thông tin NguoiDung (Hash, Salt, IsActive, Role)
        deactivate DB
        alt Không tìm thấy tài khoản hoặc bị khóa
            S-->>C: Trả về null (Xác thực thất bại)
            C-->>ND: Thông báo lỗi "Tài khoản hoặc mật khẩu không đúng"
        else Tài khoản hợp lệ
            S->>S: Xác thực mật khẩu qua PBKDF2
            alt Khớp mật khẩu PBKDF2
                S->>S: Kiểm tra xem có cần nâng cấp Hash (NeedsRehash)?
            else Không khớp PBKDF2
                S->>S: Kiểm tra mật khẩu qua SHA256 cũ
                alt Khớp mật khẩu SHA256 cũ
                    S->>S: Đánh dấu cần nâng cấp mật khẩu (needsRehash = true)
                else Không khớp cả hai
                    S-->>C: Trả về null (Xác thực thất bại)
                    C-->>ND: Thông báo lỗi "Tài khoản hoặc mật khẩu không đúng"
                end
            end
            
            alt Xác thực thành công (Khớp mật khẩu)
                alt Cần nâng cấp Hash (needsRehash == true)
                    S->>S: Băm lại mật khẩu bằng PBKDF2 (Salt mới)
                end
                S->>S: Cập nhật LanDangNhapCuoi = DateTime.UtcNow
                S->>DB: Lưu thông tin cập nhật (SaveChanges)
                activate DB
                DB-->>S: Lưu thành công
                deactivate DB
                S-->>C: Trả về đối tượng NguoiDung hợp lệ
                deactivate S
                C->>C: Khởi tạo Claims định danh (Id, Name, Role)
                alt Là Khách thuê
                    C->>C: Bổ sung Claim NguoiThueId liên kết
                end
                C->>C: Gọi HttpContext.SignInAsync ghi nhận Cookie phiên làm việc
                C-->>ND: Chuyển hướng về trang chủ tương ứng (Admin Portal / Tenant Portal)
            end
        end
    end
    deactivate C
```

### 1.2. Sơ đồ hoạt động (Activity Diagram - Bố cục dọc tối giản)
```mermaid
graph TD
    Start([Bắt đầu]) --> Input[Nhập Username & Password]
    Input --> ValidateForm{Model hợp lệ?}
    
    ValidateForm -- Không --> ShowFormError[Hiển thị lỗi nhập liệu] --> EndFail([Kết thúc thất bại])
    
    ValidateForm -- Có --> QueryUser[Truy vấn CSDL theo Username]
    QueryUser --> CheckExist{Người dùng tồn tại & hoạt động?}
    
    CheckExist -- Không --> ShowAuthError[Báo lỗi tài khoản/mật khẩu sai] --> EndFail
    
    CheckExist -- Có --> VerifyPBKDF2{Khớp PBKDF2?}
    
    VerifyPBKDF2 -- Không --> VerifySHA256{Khớp SHA256 cũ?}
    VerifySHA256 -- Không --> ShowAuthError
    VerifySHA256 -- Có --> SetRehash[Đánh dấu needsRehash = true] --> CheckRehash
    
    VerifyPBKDF2 -- Có --> CheckRehash{Cần nâng cấp Hash?}
    
    CheckRehash -- Có --> RehashPassword[Băm lại mật khẩu bằng PBKDF2] --> UpdateLoginTime
    CheckRehash -- Không --> UpdateLoginTime[Cập nhật LanDangNhapCuoi = UtcNow]
    
    UpdateLoginTime --> SaveUser[Lưu tài khoản vào PostgreSQL]
    SaveUser --> CreateClaims[Khởi tạo Claims & Thêm Claim NguoiThueId nếu là Khách thuê]
    CreateClaims --> SignIn[Ghi nhận Cookie phiên làm việc HttpContext.SignInAsync]
    SignIn --> Redirect{Chuyển hướng theo Role?}
    
    Redirect -- Khách thuê --> RedirectTenant[Chuyển hướng đến KhachThue/Dashboard] --> EndSuccess([Kết thúc thành công])
    Redirect -- Admin / Staff --> RedirectAdmin[Chuyển hướng đến Home/Index] --> EndSuccess
```

---

## 2. USE CASE 1: LẬP HỢP ĐỒNG THUÊ PHÒNG (UC-07)

### 2.1. Sơ đồ tuần tự (Sequence Diagram)
```mermaid
sequenceDiagram
    autonumber
    actor NV as Nhân viên (Admin/Staff)
    participant C as HopDongController
    participant S as HopDongService
    participant US as NguoiDungService
    participant DB as DbContext (PostgreSQL)

    NV->>C: Gửi yêu cầu lập hợp đồng (POST CreateHopDongModel)
    activate C
    C->>C: Kiểm tra ModelState.IsValid
    alt Dữ liệu đầu vào không hợp lệ
        C-->>NV: Trả về HTTP 400 (Dữ liệu không hợp lệ)
    else Dữ liệu hợp lệ
        C->>S: Gọi CreateHopDongAsync(model)
        activate S
        S->>DB: Bắt đầu SqlTransaction (BeginTransactionAsync)
        activate DB
        DB-->>S: Trả về Transaction Object
        deactivate DB

        S->>DB: Kiểm tra trạng thái phòng (TrangThaiPhong == Trong)
        activate DB
        DB-->>S: Kết quả truy vấn
        deactivate DB
        alt Phòng đã được thuê
            S->>DB: Rollback Transaction
            S-->>C: Ném ngoại lệ "Phòng đã có khách thuê"
            C-->>NV: Trả về HTTP 400 (Lỗi nghiệp vụ)
        else Phòng trống
            S->>DB: Kiểm tra người đại diện có Hợp đồng hoạt động nào khác không
            activate DB
            DB-->>S: Kết quả kiểm tra
            deactivate DB
            alt Người đại diện đang thuê phòng khác
                S->>DB: Rollback Transaction
                S-->>C: Ném ngoại lệ "Khách đại diện đang đứng tên hợp đồng khác"
                C-->>NV: Trả về HTTP 400 (Lỗi nghiệp vụ)
            else Người đại diện hợp lệ
                S->>DB: Kiểm tra trùng lịch của các thành viên ở ghép
                activate DB
                DB-->>S: Danh sách thành viên trùng lịch (nếu có)
                deactivate DB
                alt Có thành viên bị trùng lịch sinh sống
                    S->>DB: Rollback Transaction
                    S-->>C: Ném ngoại lệ "Thành viên [Tên] đang ở phòng khác"
                    C-->>NV: Trả về HTTP 400 (Lỗi nghiệp vụ)
                else Các thành viên hợp lệ
                    S->>S: Đếm tổng số người ở ghép và so khớp SoNguoiToiDa
                    alt Vượt quá số người tối đa của phòng
                        S-->>C: Ném ngoại lệ "Số lượng người vượt quá giới hạn"
                        C-->>NV: Trả về HTTP 400 (Lỗi nghiệp vụ)
                    else Số người hợp lệ
                        S->>S: Tạo mã hợp đồng duy nhất dạng HD-[Mã Chi Nhánh]-[yyMM]-[AutoNumber]
                        
                        S->>DB: Thêm bản ghi HopDong mới
                        S->>DB: Thêm các bản ghi ChiTietThanhVienHopDong
                        S->>DB: Thêm các bản ghi HopDongDieuKhoan
                        S->>DB: Cập nhật TrangThaiPhong = DaThue

                        S->>US: Gọi AutoCreateRenterAccount(Email, Phone, RenterName)
                        activate US
                        US->>DB: Kiểm tra tài khoản đã tồn tại chưa
                        activate DB
                        DB-->>US: Kết quả kiểm tra
                        deactivate DB
                        alt Chưa có tài khoản
                            US->>US: Băm mật khẩu (PBKDF2) từ SĐT
                            US->>DB: Insert tài khoản NguoiDung mới (Role: KhachThue)
                        end
                        US-->>S: Hoàn thành tạo tài khoản
                        deactivate US

                        S->>DB: Commit Transaction
                        activate DB
                        DB-->>S: Lưu thay đổi thành công
                        deactivate DB
                        S-->>C: Trả về thông tin Hợp đồng đã tạo
                        deactivate S
                        C-->>NV: Trả về HTTP 200 (Lập hợp đồng thành công)
                    end
                end
            end
        end
    end
    deactivate C
```

### 2.2. Sơ đồ hoạt động (Activity Diagram - Bố cục Ngang)
```mermaid
graph LR
    Start([Bắt đầu]) --> Input[Nhập dữ liệu hợp đồng]
    Input --> ValidateForm{Form hợp lệ?}
    
    ValidateForm -- Không --> ShowFormError[Báo lỗi nhập liệu]
    ShowFormError --> EndFail([Kết thúc thất bại])
    
    ValidateForm -- Có --> BeginTx[Khởi động Transaction]
    BeginTx --> CheckRoom{Phòng trống?}
    
    CheckRoom -- Không --> RollbackRoom[Hủy & Báo phòng bận]
    RollbackRoom --> EndFail
    
    CheckRoom -- Có --> CheckRep{Người đại diện rảnh?}
    CheckRep -- Không --> RollbackRep[Hủy & Báo đại diện bận]
    RollbackRep --> EndFail
    
    CheckRep -- Có --> CheckMembers{Thành viên hợp lệ?}
    CheckMembers -- Không --> RollbackMem[Hủy & Báo trùng thành viên]
    RollbackMem --> EndFail
    
    CheckMembers -- Có --> CheckCapacity{Số người <= Sức chứa?}
    CheckCapacity -- Không --> RollbackCap[Hủy & Báo quá tải]
    RollbackCap --> EndFail
    
    CheckCapacity -- Có --> GenerateId[Sinh mã HĐ]
    GenerateId --> InsertDb[Lưu thông tin CSDL]
    InsertDb --> UpdateRoom[Trạng thái phòng = Đang thuê]
    UpdateRoom --> CheckAccount{Đã có TK?}
    
    CheckAccount -- Có --> CommitTx[Commit Transaction]
    CheckAccount -- Không --> CreateAccount[Tạo TK KhachThue]
    CreateAccount --> CommitTx
    
    CommitTx --> ShowSuccess[Báo thành công & Reset UI]
    ShowSuccess --> EndSuccess([Kết thúc thành công])
```

---

## 3. USE CASE 2: PHÁT SINH HÓA ĐƠN HÀNG LOẠT (UC-09)

### 3.1. Sơ đồ tuần tự (Sequence Diagram)
```mermaid
sequenceDiagram
    autonumber
    actor NV as Nhân viên (Admin/Staff)
    participant C as HoaDonController
    participant S as HoaDonService
    participant DB as DbContext (PostgreSQL)

    NV->>C: Yêu cầu xem trước phát sinh hóa đơn (Xem trước phát sinh)
    activate C
    C->>S: Gọi GetPreviewInvoicesAsync(branchId, month, year)
    activate S
    S->>DB: Lấy danh sách hợp đồng hoạt động & chỉ số điện nước chưa khóa
    activate DB
    DB-->>S: Trả về danh sách dữ liệu thô
    deactivate DB
    S->>S: Tính toán thử tiền phòng, tiền điện, nước, dịch vụ khác
    S-->>C: Trả về danh sách xem trước (Preview List)
    deactivate S
    C-->>NV: Hiển thị bảng kê xem trước trên giao diện
    deactivate C

    NV->>C: Nhấn nút phát sinh hóa đơn (POST GenerateBatch)
    activate C
    C->>S: Gọi GenerateBatchInvoicesAsync(branchId, month, year, selectedRoomIds)
    activate S
    S->>DB: Khởi động SQL Transaction
    
    loop Đối với từng phòng được chọn
        S->>DB: Kiểm tra bản ghi chỉ số điện nước tháng đó đã chốt chưa?
        activate DB
        DB-->>S: Trả về bản ghi DienNuoc (IsLocked?)
        deactivate DB
        alt Chưa chốt chỉ số điện nước
            S->>DB: Rollback Transaction
            S-->>C: Ném ngoại lệ "Phòng X chưa được chốt chỉ số điện nước"
            C-->>NV: Báo lỗi yêu cầu chốt chỉ số trước
        else Đã chốt chỉ số điện nước
            S->>S: Tính toán tiền điện = (Mới - Cũ) * Đơn giá điện
            S->>S: Tính toán tiền nước = (Mới - Cũ) * Đơn giá nước
            S->>S: Tính toán dịch vụ đăng ký theo phòng
            S->>S: Tổng cộng tiền phòng + dịch vụ
            S->>S: Sinh mã hóa đơn duy nhất dạng HD-[yyyyMM]-[AutoNumber]
            S->>DB: Thêm bản ghi HoaDon (TrangThai = ChuaThanhToan)
            S->>DB: Thêm các bản ghi ChiTietHoaDon tương ứng
            S->>DB: Khóa bản ghi điện nước (IsLocked = true)
        end
    end
    
    S->>DB: SaveChangesAsync & Commit Transaction
    activate DB
    DB-->>S: Lưu thành công toàn bộ hóa đơn
    deactivate DB
    S-->>C: Trả về số lượng hóa đơn đã phát sinh thành công
    deactivate S
    C-->>NV: Trả về HTTP 200 (Thông báo phát sinh thành công)
    deactivate C
```

### 3.2. Sơ đồ hoạt động (Activity Diagram - Bố cục Ngang)
```mermaid
graph LR
    Start([Bắt đầu]) --> SelectBranch[Chọn chi nhánh/ngày]
    SelectBranch --> ClickPreview[Xem trước phát sinh]
    ClickPreview --> QueryPreview[Truy vấn dữ liệu preview]
    QueryPreview --> ShowPreviewTable[Hiển thị bảng kê]
    ShowPreviewTable --> SelectRooms[Tích chọn các phòng]
    SelectRooms --> ClickGenerate[Phát sinh hóa đơn]
    ClickGenerate --> StartTx[Bắt đầu SQL Transaction]
    
    StartTx --> LoopStart{Duyệt từng phòng}
    LoopStart -- Còn phòng --> CheckIndex{Đã chốt điện nước?}
    
    CheckIndex -- Chưa --> RollbackTx[Hủy giao dịch & Báo lỗi chốt chỉ số]
    RollbackTx --> EndFail([Kết thúc thất bại])
    
    CheckIndex -- Có --> CalcBill[Tính toán tổng tiền hóa đơn]
    CalcBill --> GenerateCode[Sinh mã hóa đơn độc nhất]
    GenerateCode --> InsertInvoice[Lưu hóa đơn và chi tiết vào CSDL]
    InsertInvoice --> LockIndex[Khóa chỉ số IsLocked = true]
    LockIndex --> LoopStart
    
    LoopStart -- Hết phòng --> SaveDb[Lưu thay đổi CSDL & Commit Transaction]
    SaveDb --> ShowSuccess[Thông báo thành công]
    ShowSuccess --> EndSuccess([Kết thúc thành công])
```

---

## 4. USE CASE 3: GHI NHẬN THU TIỀN - QUÉT MÃ VIETQR (UC-09)

### 4.1. Sơ đồ tuần tự (Sequence Diagram)
```mermaid
sequenceDiagram
    autonumber
    actor NV as Nhân viên (Admin/Staff)
    actor KT as Khách thuê (Renter)
    participant C as HoaDonController
    participant H as VietQrHelper
    participant S as HoaDonService
    participant DB as DbContext (PostgreSQL)

    NV->>C: Yêu cầu mở chi tiết hóa đơn thanh toán
    activate C
    C->>C: Đọc cấu hình tài khoản thụ hưởng từ appsettings.json
    C->>H: Gọi GenerateQrPayload(soTaiKhoan, nganHang, tongTien, noiDung)
    activate H
    H->>H: Chuẩn hóa nội dung (THANH TOAN [MaHoaDon])
    H->>H: Mã hóa định dạng EMVCo & tính mã kiểm tra CRC16
    H-->>C: Trả về chuỗi VietQR Payload
    deactivate H
    C->>C: Render chuỗi Payload thành ảnh QR Code dạng PNG
    C-->>NV: Hiển thị Popup chi tiết hóa đơn đính kèm mã VietQR
    deactivate C

    KT->>NV: Thực hiện quét mã VietQR bằng Mobile Banking & chuyển khoản
    NV->>NV: Xác nhận tiền đã vào tài khoản (hoặc nhận tiền mặt)
    NV->>C: Nhập phương thức thanh toán & Nhấn "Xác nhận thu tiền"
    activate C
    C->>S: Gọi ConfirmPaymentAsync(invoiceId, paymentMethod, ghiChu, nhanVienId)
    activate S
    S->>DB: Bắt đầu Transaction
    S->>DB: Lấy thông tin hóa đơn (TrangThai == ChuaThanhToan)
    activate DB
    DB-->>S: Trả về bản ghi HoaDon
    deactivate DB
    
    S->>S: Cập nhật TrangThaiHoaDon = DaThanhToan
    S->>S: Cập nhật SoTienDaThanhToan = TongTienHoaDon
    S->>S: Cập nhật NgayThanhToan = DateTime.UtcNow
    
    S->>DB: Chèn bản ghi mới vào bảng LichSuThanhToan
    S->>DB: Commit Transaction
    activate DB
    DB-->>S: Lưu thành công
    deactivate DB
    
    S-->>C: Trả về kết quả ghi nhận thành công
    deactivate S
    C-->>NV: Hiển thị thông báo Thu tiền thành công & cập nhật trạng thái UI
    deactivate C
```

### 4.2. Sơ đồ hoạt động (Activity Diagram - Bố cục Ngang)
```mermaid
graph LR
    Start([Bắt đầu]) --> SelectInvoice[Chọn hóa đơn thu tiền]
    SelectInvoice --> CheckStatus{Chưa thanh toán?}
    
    CheckStatus -- Không --> BlockPayment[Báo lỗi: HĐ đã trả]
    BlockPayment --> EndFail([Kết thúc thất bại])
    
    CheckStatus -- Có --> ReadConfig[Đọc thông tin ngân hàng]
    ReadConfig --> BuildPayload[Tạo VietQR EMVCo & CRC16]
    BuildPayload --> RenderQR[Render QR hiển thị popup]
    RenderQR --> WaitScan[Khách quét QR & chuyển khoản]
    WaitScan --> ConfirmReceipt{Nhận đủ tiền?}
    
    ConfirmReceipt -- Không/Hủy --> EndFail
    ConfirmReceipt -- Có --> SelectMethod[Chọn phương thức/ghi chú]
    SelectMethod --> ClickConfirm[Nhấn Xác nhận thu tiền]
    ClickConfirm --> StartTx[Khởi tạo Transaction]
    StartTx --> UpdateInvoice[Cập nhật status = DaThanhToan]
    UpdateInvoice --> InsertHistory[Ghi lịch sử thanh toán]
    InsertHistory --> CommitTx[Commit Transaction]
    CommitTx --> ShowSuccess[Thông báo thành công & in biên lai]
    ShowSuccess --> EndSuccess([Kết thúc thành công])
```

---

## 5. USE CASE 4: TỰ ĐỘNG QUÉT VÀ GỬI EMAIL NHẮC NỢ (UC-15)

### 5.1. Sơ đồ tuần tự (Sequence Diagram)
```mermaid
sequenceDiagram
    autonumber
    participant B as InvoiceReminderService (Background Job)
    participant DB as DbContext (PostgreSQL)
    participant HD as HoaDonService
    participant E as EmailService
    participant MS as SMTP Mail Server
    participant C as Cache (sent_reminders.json)

    B->>B: Kích hoạt định kỳ (Mỗi 1 giờ)
    activate B
    B->>B: Đọc cấu hình appsettings.json (AutoReminderSettings.Enabled)
    alt Tính năng tự động bị tắt
        B->>B: Dừng tiến trình
    else Tính năng đang bật
        B->>C: Đọc danh sách ID hóa đơn đã gửi nhắc nợ trước đó
        activate C
        C-->>B: Trả về danh sách ID hóa đơn đã gửi
        deactivate C
        
        B->>DB: Quá hạn 5 ngày: Lọc các hóa đơn ChưaThanhToan & Chưa gửi nhắc nợ
        activate DB
        DB-->>B: Danh sách hóa đơn quá hạn
        deactivate DB
        
        loop Đối với từng hóa đơn quá hạn
            B->>B: Kiểm tra email khách đại diện có hợp lệ?
            alt Email trống/Sai định dạng
                B->>B: Ghi log Cảnh báo (Warning) và bỏ qua hóa đơn này
            else Email hợp lệ
                B->>HD: Gọi ExportPdfAsync(invoiceId)
                activate HD
                HD-->>B: Trả về file PDF hóa đơn (mảng byte)
                deactivate HD
                
                B->>E: Gọi SendInvoiceEmailAsync(EmailKhach, pdfBytes, templateBody)
                activate E
                E->>MS: Thiết lập kết nối bảo mật TLS (Port 587)
                activate MS
                E->>MS: Gửi thư đính kèm PDF hóa đơn
                MS-->>E: Gửi thư thành công
                deactivate MS
                E-->>B: Trả về kết quả Thành công
                deactivate E
                
                B->>C: Thêm ID hóa đơn vào file sent_reminders.json
                B->>B: Ghi log Thông tin (Information) gửi thành công
            end
        end
    end
    B->>B: Ngủ chờ chu kỳ tiếp theo (1 giờ sau)
    deactivate B
```

### 5.2. Sơ đồ hoạt động (Activity Diagram - Bố cục dọc tối giản)
```mermaid
graph TD
    Start([Bắt đầu quét]) --> CheckEnabled{Enabled == true?}
    CheckEnabled -- Không --> Sleep[Ngủ chờ chu kỳ kế] --> End([Kết thúc])
    CheckEnabled -- Có --> Load[Đọc cache & Lọc HĐ quá hạn >5 ngày]
    Load --> Loop{Duyệt từng HĐ?}
    Loop -- Hết --> Sleep
    Loop -- Còn --> CheckSent{Đã gửi nhắc nợ?}
    
    CheckSent -- Có --> Loop
    CheckSent -- Chưa --> CheckEmail{Email hợp lệ?}
    
    CheckEmail -- Không --> LogWarn[Ghi log: Thiếu email] --> Loop
    CheckEmail -- Có --> SendMail[Sinh PDF & Gửi Mail nhắc nợ via SMTP]
    
    SendMail --> CheckResult{Gửi thành công?}
    CheckResult -- Không --> LogErr[Ghi log: SMTP lỗi] --> Loop
    CheckResult -- Có --> Update[Lưu ID vào sent_reminders & Ghi log] --> Loop
```

---

## 6. USE CASE 5: QUẢN LÝ SỰ CỐ & SỬA CHỮA (UC-16)

### 6.1. Luồng Khách Thuê Báo Cáo Sự Cố

#### 6.1.1. Sơ đồ tuần tự (Sequence Diagram)
```mermaid
sequenceDiagram
    autonumber
    actor KT as Khách thuê (Renter)
    participant C as SuCoController (KhachThue)
    participant HD as HopDongService
    participant CL as CloudinaryStorageService
    participant S as YeuCauSuCoService
    participant TB as ThongBaoService
    participant DB as DbContext (PostgreSQL)

    KT->>C: Truy cập trang báo sự cố (GET /KhachThue/SuCo/Index)
    activate C
    C->>HD: Lấy danh sách phòng đang thuê (GetHopDongsByNguoiThueIdAsync)
    activate HD
    HD-->>C: Trả về danh sách phòng trọ
    deactivate HD
    C-->>KT: Hiển thị Form báo sự cố & Lịch sử sự cố đã gửi
    deactivate C

    KT->>C: Gửi báo cáo sự cố (POST Create: PhongTroId, TieuDe, MoTa, HinhAnhFiles)
    activate C
    C->>C: Kiểm tra dung lượng (<=5MB) & định dạng file ảnh (.jpg, .png...)
    alt File ảnh không hợp lệ
        C-->>KT: Phản hồi JSON lỗi: "Kích thước/định dạng file không cho phép"
    else File ảnh hợp lệ
        opt Có file ảnh đính kèm
            C->>CL: Upload hình ảnh sự cố (UploadImageAsync)
            activate CL
            CL-->>C: Trả về danh sách URL hình ảnh Cloudinary
            deactivate CL
        end
        C->>S: Tạo bản ghi sự cố mới (CreateAsync, TrangThai = ChoTiepNhan)
        activate S
        S->>DB: Add YeuCauSuCo & SaveChangesAsync
        activate DB
        DB-->>S: Trả về kết quả lưu CSDL thành công
        deactivate DB
        S-->>C: Trả về true (Tạo sự cố thành công)
        deactivate S

        C->>TB: Gửi thông báo sự cố mới cho Admin & NhanVien (GuiChoQuyenAsync)
        activate TB
        TB->>DB: Lưu thông báo hệ thống vào CSDL
        activate DB
        DB-->>TB: Lưu thông báo thành công
        deactivate DB
        TB-->>C: Hoàn tất phát thông báo
        deactivate TB

        C-->>KT: Phản hồi JSON: success = true, message = "Gửi báo cáo sự cố thành công!"
    end
    deactivate C
```

#### 6.1.2. Sơ đồ hoạt động (Activity Diagram - Bố cục Ngang)
```mermaid
graph LR
    Start([Bắt đầu]) --> InputData[Khách thuê nhập Tiêu đề, Mô tả & chọn Phòng]
    InputData --> CheckImg{Có file ảnh đính kèm?}
    
    CheckImg -- Có --> ValidateImg{File <= 5MB & định dạng hợp lệ?}
    ValidateImg -- Không --> ErrImg[Báo lỗi file ảnh không hợp lệ] --> EndFail([Kết thúc thất bại])
    ValidateImg -- Có --> UploadCloud[Upload ảnh lên Cloudinary lấy URLs] --> ValidateModel
    
    CheckImg -- Không --> ValidateModel{Dữ liệu rỗng?}
    ValidateModel -- Có --> ErrModel[Báo lỗi nhập thiếu dữ liệu] --> EndFail
    
    ValidateModel -- Không --> SaveDb[Lưu YeuCauSuCo với TrangThai = ChoTiepNhan]
    SaveDb --> NotifyAdmin[Gửi ThongBao tự động tới Admin/Staff]
    NotifyAdmin --> ShowSuccess[Phản hồi gửi báo cáo thành công]
    ShowSuccess --> EndSuccess([Kết thúc thành công])
```

---

### 6.2. Luồng Admin Tiếp Nhận & Xử Lý Sự Cố

#### 6.2.1. Sơ đồ tuần tự (Sequence Diagram)
```mermaid
sequenceDiagram
    autonumber
    actor AD as Admin / Nhân viên
    participant C as YeuCauSuCoController (QuanLyNhaTro)
    participant CN as ChiNhanhService
    participant S as YeuCauSuCoService
    participant TB as ThongBaoService
    participant DB as DbContext (PostgreSQL)

    AD->>C: Truy cập danh sách sự cố (GET /QuanLyNhaTro/YeuCauSuCo?chiNhanhId=&trangThai=&soThang=6)
    activate C
    C->>S: Truy vấn danh sách sự cố theo bộ lọc (GetAllAsync)
    activate S
    S->>DB: Query YeuCauSuCos (Include PhongTro, NguoiThue)
    activate DB
    DB-->>S: Trả về danh sách sự cố
    deactivate DB
    S-->>C: Trả về List<YeuCauSuCo>
    deactivate S

    C->>CN: Lấy danh sách Chi nhánh cho Dropdown
    activate CN
    CN-->>C: Trả về List<ChiNhanh>
    deactivate CN

    C-->>AD: Hiển thị bảng danh sách sự cố & Modal cập nhật
    deactivate C

    AD->>C: Gửi yêu cầu cập nhật (POST UpdateStatus: Id, TrangThai, ChiPhiSuaChua, CongVaoHoaDon, LyDoTuChoi, GhiChuAdmin)
    activate C
    alt TrangThai == DaHuy mà LyDoTuChoi bị rỗng
        C-->>AD: Phản hồi JSON lỗi: "Vui lòng nhập lý do từ chối."
    else Dữ liệu hợp lệ
        C->>S: Cập nhật trạng thái sự cố (UpdateStatusAsync)
        activate S
        S->>DB: Cập nhật TrangThai, ChiPhi, CongVaoHoaDon, NgayXuLy, GhiChu & SaveChangesAsync
        activate DB
        DB-->>S: Trả về kết quả lưu CSDL
        deactivate DB
        S-->>C: Trả về true (Cập nhật thành công)
        deactivate S

        C->>TB: Gửi thông báo chuyển trạng thái cho Khách thuê (GuiChoKhachThueAsync)
        activate TB
        TB->>DB: Lưu thông báo vào CSDL
        activate DB
        DB-->>TB: Lưu thông báo thành công
        deactivate DB
        TB-->>C: Hoàn tất thông báo
        deactivate TB

        C-->>AD: Phản hồi JSON: success = true, message = "Cập nhật trạng thái thành công."
    end
    deactivate C
```

#### 6.2.2. Sơ đồ hoạt động (Activity Diagram - Bố cục Ngang)
```mermaid
graph LR
    Start([Bắt đầu]) --> FilterList[Admin lọc & chọn sự cố cần xử lý]
    FilterList --> OpenModal[Mở Modal cập nhật trạng thái & chi phí]
    OpenModal --> InputStatus[Nhập Trạng thái, Chi phí, Ghi chú/Lý do]
    InputStatus --> CheckCancel{Trạng thái mới = Đã hủy?}
    
    CheckCancel -- Có --> CheckReason{Lý do từ chối rỗng?}
    CheckReason -- Có --> ErrReason[Báo lỗi: Thiếu lý do từ chối] --> EndFail([Kết thúc thất bại])
    CheckReason -- Không --> SetCancel[Gán TrangThai = DaHuy & NgayXuLy] --> SaveDb
    
    CheckCancel -- Không --> SetNormal[Gán TrangThai mới, Chi phi, CongVaoHoaDon] --> SaveDb
    
    SaveDb[Lưu thay đổi YeuCauSuCo vào CSDL] --> SendNotify[Phát thông báo cho Khách thuê]
    SendNotify --> ShowSuccess[Báo cập nhật thành công]
    ShowSuccess --> EndSuccess([Kết thúc thành công])
```

