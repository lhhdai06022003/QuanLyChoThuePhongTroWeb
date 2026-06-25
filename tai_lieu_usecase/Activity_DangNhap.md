# SƠ ĐỒ HOẠT ĐỘNG: CHỨC NĂNG ĐĂNG NHẬP HỆ THỐNG

Tài liệu này cung cấp mã vẽ **Sơ đồ hoạt động (Activity Diagram)** biểu diễn chi tiết luồng xử lý xác thực bảo mật đa tầng (PBKDF2/SHA256, Rehash, Claims và Phân quyền chuyển hướng) của chức năng Đăng nhập.

---

## 1. Sơ đồ hoạt động bằng mã Mermaid.js (Cách 1)
Anh/chị dán đoạn mã này vào [Mermaid Live Editor](https://mermaid.live/) hoặc Draw.io để xem trực quan:

```text
graph TD
    Start([Bắt đầu]) --> Input[Người dùng nhập Username & Password]
    Input --> Submit[Nhấn nút Đăng nhập]
    Submit --> CheckModel{Model hợp lệ?}
    
    CheckModel -- Không --> ErrorModel[Báo lỗi nhập thiếu thông tin]
    ErrorModel --> EndFail([Kết thúc thất bại])
    
    CheckModel -- Có --> QueryUser[Truy vấn thông tin User trong DB]
    QueryUser --> CheckExist{User tồn tại & hoạt động?}
    
    CheckExist -- Không --> ErrorAuth[Báo lỗi tài khoản hoặc mật khẩu sai]
    ErrorAuth --> EndFail
    
    CheckExist -- Có --> VerifyPBKDF2{Khớp PBKDF2?}
    
    VerifyPBKDF2 -- Không --> VerifySHA256{Khớp SHA256 cũ?}
    VerifySHA256 -- Không --> ErrorAuth
    VerifySHA256 -- Có --> SetRehash[Đánh dấu cần nâng cấp mật khẩu]
    
    VerifyPBKDF2 -- Có --> CheckRehash
    SetRehash --> CheckRehash{Cần nâng cấp Hash?}
    
    CheckRehash -- Có --> RehashPassword[Băm lại mật khẩu bằng PBKDF2]
    CheckRehash -- Không --> UpdateLoginTime
    RehashPassword --> UpdateLoginTime[Cập nhật LanDangNhapCuoi = UtcNow]
    
    CheckRehash -- Không --> UpdateLoginTime
    
    UpdateLoginTime --> SaveUser[Lưu thay đổi vào Database]
    SaveUser --> CreateClaims[Khởi tạo Claims định danh]
    CreateClaims --> CheckRole{Vai trò là Khách thuê?}
    
    CheckRole -- Có --> AddTenantClaim[Thêm Claim NguoiThueId]
    CheckRole -- Không --> SignIn
    
    AddTenantClaim --> SignIn[Gọi HttpContext.SignInAsync ghi nhận Cookie]
    SignIn --> Redirect{Vai trò?}
    
    Redirect -- Khách thuê --> RedirectTenant[Chuyển hướng đến KhachThue/Dashboard]
    Redirect -- Admin / Staff --> RedirectAdmin[Chuyển hướng đến Home/Index]
    
    RedirectTenant --> EndSuccess([Kết thúc thành công])
    RedirectAdmin --> EndSuccess
```

---

## 2. File vẽ Draw.io XML
Tệp XML chứa sơ đồ hoạt động trực quan đã được thiết kế sẵn tại:
[Activity_DangNhap.xml](file:///e:/UTE/TieuLuanChuyenNganh/QuanLyChoThuePhongTroWeb/tai_lieu_usecase/Activity_DangNhap.xml)

*   **Cách sử dụng:** Anh/chị truy cập [Draw.io](https://app.diagrams.net/), chọn **File** -> **Import from** -> **Device...** rồi chọn tệp [Activity_DangNhap.xml](file:///e:/UTE/TieuLuanChuyenNganh/QuanLyChoThuePhongTroWeb/tai_lieu_usecase/Activity_DangNhap.xml) để mở sơ đồ hoạt động.
