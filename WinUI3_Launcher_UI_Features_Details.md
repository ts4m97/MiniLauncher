# ĐẶC TẢ CHI TIẾT GIAO DIỆN VÀ CHỨC NĂNG
**Dự án:** WinUI 3 Mini App Launcher
**Môi trường:** Windows 10 / Windows 11 (Offline/Local Storage)

---

## PHẦN 1: GIAO DIỆN NGƯỜI DÙNG (UI) VÀ TRẢI NGHIỆM (UX)

Giao diện được thiết kế theo ngôn ngữ **Fluent Design** của Microsoft, tập trung vào sự tối giản, sang trọng và không gây mất tập trung.

### 1.1. Chất liệu và Khung cửa sổ (Window Styling)
* **Vật liệu nền (Backdrop):** Sử dụng **Mica** (trên Windows 11) hoặc **Acrylic** (trên Windows 10). Lớp nền có khả năng xuyên thấu, làm mờ các cửa sổ hoặc hình nền phía sau, tạo cảm giác không gian 3 chiều.
* **Đổ bóng (Drop Shadow):** Viền cửa sổ tỏa ra một lớp bóng mờ, giúp Launcher tách biệt hoàn toàn và nổi bật lên trên mọi ứng dụng khác đang mở.
* **Bo góc (Rounded Corners):** Cửa sổ chính được bo góc chuẩn 12px, các thành phần bên trong (thẻ, nút bấm) bo góc 4px - 8px.
* **Chủ đề (Theme):** Tự động đồng bộ với chế độ Light/Dark Mode của hệ điều hành Windows.

### 1.2. Bố cục tổng thể (Layout)
* **Thanh tìm kiếm (Search Bar):** Nằm to bản ở vị trí trên cùng. Có biểu tượng kính lúp nhỏ bên trái. Ô nhập liệu mặc định được focus (nhấp nháy con trỏ) ngay khi mở app.
* **Khu vực hiển thị danh sách (Content Area):**
  * Nằm ngay dưới thanh tìm kiếm.
  * Hỗ trợ tự động cuộn (Smooth Scrolling) với thanh cuộn tàng hình (chỉ hiện khi lướt chuột vào).
  * Hiển thị danh sách các ứng dụng/thư mục được ghim theo dạng lưới (Grid - icon to) hoặc danh sách (List - icon nhỏ kèm đường dẫn).
* **Thanh công cụ (Footer):** Rất mỏng ở dưới cùng, chứa icon **Cài đặt (Bánh răng)** và icon **Chợ ứng dụng (Cửa hàng)**.

### 1.3. Hệ thống Hoạt ảnh (Animations) - "Độ Bóng Bẩy"
* **Khi gọi Launcher (Entrance):** Cửa sổ phóng to từ 95% lên 100% kết hợp hiệu ứng trượt nhẹ từ dưới lên và Fade-in (từ mờ sang rõ) mượt mà trong 0.2 giây.
* **Hiệu ứng Hover (Di chuột):** Sử dụng `RevealBrush` (ánh sáng chạy theo con trỏ chuột). Khi trỏ vào một ứng dụng, thẻ ứng dụng đó nổi nhẹ lên, icon nảy lên một nhịp.
* **Hiệu ứng Click (Press):** Thẻ ứng dụng lún nhẹ xuống tạo cảm giác vật lý.
* **Hiệu ứng Tìm kiếm:** Khi gõ chữ, các ứng dụng không khớp sẽ mờ dần và thu nhỏ lại, các ứng dụng khớp sẽ trượt mượt mà lên để lấp đầy khoảng trống (Implicit Animations).

---

## PHẦN 2: CHI TIẾT CÁC CHỨC NĂNG CỐT LÕI

### 2.1. Khởi chạy và Ẩn ngầm (Launch & System Tray)
* **Phím tắt toàn cục (Global Hotkey):** Người dùng nhấn tổ hợp phím (ví dụ: `Alt + Space`) để gọi Launcher xuất hiện ngay lập tức ở giữa màn hình (hoặc tại vị trí con trỏ chuột), bất kể đang dùng phần mềm nào.
* **Thu gọn thông minh:** Nhấn phím `Esc`, hoặc click chuột ra ngoài vùng Launcher (Lost Focus), hoặc nhấn lại phím tắt, cửa sổ sẽ ngay lập tức biến mất (Fade-out).
* **Chạy ngầm:** Phần mềm không tắt hẳn mà nằm ngoan ngoãn dưới Khay hệ thống (System Tray / Taskbar Corner). Click chuột phải vào icon ở khay hệ thống để hiện menu (Mở, Cài đặt, Thoát).

### 2.2. Tìm kiếm và Truy cập nhanh
* **Lọc thời gian thực (Live Search):** Gõ ký tự vào ô tìm kiếm, danh sách bên dưới cập nhật kết quả ngay lập tức (độ trễ tính bằng mili-giây). Có thể tìm theo tên App, tên thư mục, hoặc từ khóa tùy chỉnh.
* **Thao tác bàn phím:** Dùng phím mũi tên Lên/Xuống để chọn app trong danh sách kết quả, nhấn `Enter` để mở. Điều này giúp thao tác mở app không cần chạm tới chuột.

### 2.3. Quản lý Mục ghim (Pin/Unpin)
* **Kéo thả (Drag & Drop):** Kéo trực tiếp một file `.exe` hoặc một thư mục từ File Explorer thả vào giao diện Launcher để thêm nhanh.
* **Menu Chuột phải (Context Menu):** Click chuột phải vào một app đã ghim để: Đổi tên hiển thị, Đổi icon, Mở thư mục chứa app đó, hoặc Xóa khỏi danh sách (Bỏ ghim).

---

## PHẦN 3: TÍNH NĂNG CHỢ ỨNG DỤNG OFFLINE (OFFLINE STORE)

Đây là tính năng độc đáo giúp quản lý hệ sinh thái phần mềm nội bộ mà không cần kết nối Internet:

### 3.1. Nguồn dữ liệu (Data Source)
* Trỏ đường dẫn kho lưu trữ (Store Path) vào một thư mục trên máy (Ví dụ: Ổ đĩa dùng chung nội bộ, hoặc thư mục Google Drive dạng Offline cục bộ).
* Thư mục này chỉ chứa các file `config.json` định nghĩa phần mềm, file icon `.png`, và (tùy chọn) các file shortcut `.lnk`.

### 3.2. Giao diện Chợ ứng dụng
* Khi click vào icon "Cửa hàng" dưới Footer, giao diện trượt ngang sang một trang mới.
* Hiển thị danh sách các ứng dụng/công cụ có sẵn trong thư mục kho lưu trữ.
* Phân loại theo thẻ (Ví dụ: Công cụ Dev, Phần mềm Văn phòng, Thư mục Dự án chung).

### 3.3. Cài đặt (Ánh xạ) 1-Click
* Các app trong Chợ offline sẽ có nút "Cài đặt" (Thực chất là "Ghim nhanh").
* Khi bấm vào, Launcher sẽ copy thông tin đường dẫn và icon của app đó vào file cấu hình gốc của bạn. Ngay lập tức app đó xuất hiện ngoài màn hình chính của Launcher mà không cần bất kỳ quá trình download nào.

---

## PHẦN 4: LƯU TRỮ VÀ TỐI ƯU HIỆU NĂNG

* **Không Internet, Không Đồng bộ Cloud:** Toàn bộ cấu hình (danh sách ghim, phím tắt, giao diện) được lưu tại một file `config.json` duy nhất ở thư mục `%AppData%`.
* **Gọn nhẹ (Portable-friendly):** Quá trình build dự án sẽ xuất ra một file `.exe` độc lập duy nhất (Single-File Publish). File production sạch sẽ, có thể copy mang sang máy Windows khác xài luôn.
* **Tiết kiệm RAM:** Khi ẩn xuống khay hệ thống, ứng dụng kích hoạt chế độ dọn rác (Garbage Collection) để giải phóng bộ nhớ, đảm bảo không làm chậm máy trong quá trình làm việc.
