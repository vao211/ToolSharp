# FolderScannerUI

Ứng dụng desktop phân tích dung lượng ổ đĩa, quét thư mục và lọc tệp tin theo phần mở rộng với giao diện hiện đại, vuông vức, hỗ trợ **Dark / Light Mode** và **đa ngôn ngữ động (Việt – Anh)** thông qua file JSON trong thư mục `lang/`.

## ✨ Tính năng nổi bật

- **3 chế độ quét linh hoạt:**
  - **Quét kích thước thư mục:** Tính tổng dung lượng đệ quy của từng thư mục con (tùy chọn bao gồm tệp ở thư mục gốc), hỗ trợ nhấn đúp để đi sâu vào thư mục con và nút quay lên thư mục cha.
  - **Quét sâu toàn bộ tệp:** Liệt kê toàn bộ tệp tin nằm trong mọi thư mục con (tùy chọn hiển thị kèm thư mục và dung lượng cộng dồn).
  - **Lọc theo đuôi tệp:** Tìm kiếm nhanh tất cả tệp khớp với một hoặc nhiều đuôi tệp (ví dụ: `.mp4, .mkv, .zip`), kèm các nút gợi ý nhanh.
- **Giao diện vuông vức & Dark / Light Mode:**
  - Thiết kế dashboard sắc nét, bảng kết quả có thanh tỷ lệ `%` dung lượng và nhãn phân tầng màu theo kích thước.
  - Chuyển đổi trực tiếp giữa **Chế độ Tối (Dark)** và **Chế độ Sáng (Light)** ở góc trên bên phải; tự động lưu tùy chọn vào `setting.json`.
- **Đa ngôn ngữ Việt – Anh qua `lang/*.json`:**
  - Tự động quét tất cả file `*.json` trong thư mục `lang/` khi khởi động.
  - Hỗ trợ chuyển ngôn ngữ ngay trên giao diện và nút **Tải lại gói ngôn ngữ** mà không cần khởi động lại ứng dụng.
- **Tìm kiếm, lọc & sắp xếp tức thì:**
  - Tìm theo tên, đuôi tệp hoặc đường dẫn; lọc nhanh theo `Tất cả / Thư mục / Tệp tin`.
  - Sắp xếp tăng/giảm theo cột **Loại**, **Tên**, **Kích thước**, **Ngày tạo**.
- **An toàn & hiệu năng cao:**
  - Quét bất đồng bộ kèm thanh tiến trình trực tiếp và nút **Dừng quét**.
  - Tự động bỏ qua symlink/reparse point và các thư mục hệ thống không đủ quyền; hỗ trợ nút **Chạy quyền Admin** trên Windows.
- **Xuất báo cáo & thao tác nhanh:**
  - Xuất kết quả ra **CSV** (UTF-8 BOM mở chuẩn tiếng Việt trên Excel), **TXT** hoặc **JSON**.
  - Hỗ trợ mở vị trí trong File Explorer và sao chép nhanh đường dẫn từng mục.

## 🛠️ Công nghệ sử dụng

- **.NET 10**
- **Avalonia UI 12** (`Avalonia.Themes.Fluent`)
- **CommunityToolkit.Mvvm**

## 🚀 Hướng dẫn chạy & đóng gói

### Chạy ở môi trường phát triển

```bash
dotnet run --project FolderScannerUI/FolderScannerUI.csproj
```

### Đóng gói thành file `.exe` độc lập (Windows x64)

```bash
dotnet publish FolderScannerUI/FolderScannerUI.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

> Lưu ý: Thư mục `lang/` được cấu hình copy riêng ra thư mục build/publish để bạn có thể chỉnh sửa hoặc thêm file ngôn ngữ mới dễ dàng.

## 🌐 Thêm hoặc tùy chỉnh ngôn ngữ

1. Mở thư mục `lang/` cạnh file thực thi (hoặc `FolderScannerUI/lang/` trong mã nguồn).
2. Sao chép `en_us.json` hoặc `vi_vn.json` thành một file mới (ví dụ `ja_jp.json`).
3. Cập nhật trường `"language_name"` và các chuỗi dịch tương ứng.
4. Nhấn nút làm mới bên cạnh hộp chọn ngôn ngữ trong app để nạp ngay gói ngôn ngữ mới.

## 📁 Cấu trúc thư mục

```text
FolderScannerUI/
├── Assets/              # Biểu tượng ứng dụng (icon.ico)
├── lang/                # Các gói ngôn ngữ JSON (vi_vn.json, en_us.json)
├── Models/              # AppSettings, FileSystemItemInfo, ScanEnums
├── Services/            # FolderScannerService, ExportService, LocalizationService
├── ViewModels/          # MainWindowViewModel, ViewModelBase
├── Views/               # MainWindow.axaml & code-behind
├── App.axaml            # ThemeDictionaries (Light/Dark) & Styles
└── Program.cs           # Điểm khởi chạy Avalonia
```

---

Made with Gemini
