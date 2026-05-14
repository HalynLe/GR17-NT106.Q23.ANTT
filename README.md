# NOTE UPDATE 14/5

* hiện tại eraser và undo/redo đều chỉ xóa được ngay tại thời điểm đó, khi out ra vào lại nét vẽ vẫn giữ nguyên và cũng chỉ được trên 1 client, client khác không thấy đã xoá, undo/redo có vẻ không xóa đúng nét, ai đó test lại phần này thử nha
* hiện tại logic save/load nét vẽ và dữ liệu chat vẫn bình thường, việc thêm hàm để chỉnh sửa eraser và undo/redo có thể gây nên lỗi (chưa xoá ở database nên vẫn được load lên khi reset, không load được nét vẽ nữa,...), nếu có chỉnh sửa nhớ test lại

khi chạy nhớ đổi tk MySQL trong ServerSocket.cs và MasterServer/appsettings.json

joined room: meme
room_id: 8
password: 123456

các account có thể đăng nhập để test realtime trong phòng vẽ nếu lười nhớ tài khoảng
name: tula
password: 123456

name: lottie
password: 123456

name: phuong
password:123456

sửa lại appsetting.json theo tk mySQL
