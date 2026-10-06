# Báo cáo: an toàn và quy mô của AuraEngine (2026-10-06)

## 1. Mục tiêu

Hệ thống physics phải an toàn (không crash, không hỏng bộ nhớ, không âm thầm sai) và mô phỏng được càng nhiều object càng tốt. Các tính năng khác chỉ ghi vào trạng thái tài liệu.

## 2. Kết luận

- Đã tìm và sửa 3 lỗi kernel ảnh hưởng trực tiếp tới an toàn và quy mô. Commit `5e9cdfc`, `6c3a437`, `2529e59`.
- Sau sửa: 3D chạy đúng đến 50.000 body, 2D chạy đúng đến 50.000 body. Không có NaN, không có body xuyên sàn.
- Hiệu năng không tuyến tính theo mong muốn ở 3D: từ khoảng 10.000 body luôn thức trở lên, một bước vượt 16 ms (không giữ được 60 fps).
- Còn lỗi an toàn chưa xử lý (mục 6) và chưa chạy smoke test trong Unity với dylib mới.

## 3. Phương pháp

Thêm chế độ `bench --scale` vào `Native/AuraEngine/tests/ManagedKernel/BenchRunner.cs`: đống box đơn vị xếp sát nhau, không cho ngủ, rơi xuống mặt phẳng, 120 bước 1/60 s. Sau đó kiểm tra từng body: NaN, hoặc `y < -0.6` (xuyên sàn). Đo trên Apple silicon, bản release, dùng tất cả worker.

## 4. Lỗi tìm thấy và đã sửa

| # | Lỗi | Hậu quả | Sửa |
|---|---|---|---|
| 1 | Jolt giới hạn 8192 cặp va chạm và 8192 contact; lỗi trả về của `PhysicsSystem::Update` bị bỏ qua | Từ khoảng 5.000 body chạm nhau, 25–70% box rơi xuyên sàn, không có cảnh báo | Nâng cả hai lên 2^20 (`kMaxBodyPairs`, `kMaxContactConstraints`) |
| 2 | `TempAllocatorImpl` cố định 64 MB | Khi nâng giới hạn ở mục 1, tiến trình abort với "TempAllocator: Out of memory trying to allocate 528482304 bytes" | Đổi sang `TempAllocatorMalloc` |
| 3 | Box2D không chép `linearDamping` và `angularDamping` vào body | Body 2D không bị giảm tốc (vx giữ 10,0 sau 120 bước, Jolt còn 3,663) | Nối hai trường vào `b2BodyDef`; bật lại 3 oracle damping 2D |

Lỗi 3 do agent M phát hiện qua oracle. Lỗi 1 và 2 do bài stress.

## 5. Số đo sau sửa

Bước trung bình, đống box dày đặc, luôn thức:

| Số body | 3D | 2D |
|---|---|---|
| 2.000 | 2,6 ms | 1,1 ms |
| 5.000 | 7,5 ms | 3,4 ms |
| 10.000 | 24,7 ms | 8,5 ms |
| 20.000 | 59 ms | 18 ms |
| 50.000 | 187 ms | 44 ms |

Chú ý:
- Đây là trường hợp xấu nhất. Body ngủ gần như không tốn chi phí, nên scene thật cần giữ `allowSleeping` bật.
- Bước đầu tiên ở 3D 50.000 body mất khoảng 2,1 s (tạo cache contact). Bước đó có thể gây giật khi nạp.
- Ngân sách CI cũ trong `BenchRunner` chỉ phủ đến 1.000 body; mức lớn hơn hiện chỉ báo cáo, chưa có ngưỡng kiểm tra.

## 6. Giới hạn cứng và rủi ro còn mở

Giới hạn:
- 65.536 body mỗi world Jolt. Vượt giới hạn, `AttachBody` trả về id không hợp lệ, world vẫn step, slot giải phóng dùng lại được.
- 4.096 world cùng tồn tại.
- Box2D không có giới hạn cứng đã biết; đã chạy 50.000 body.

Rủi ro chưa xử lý:
1. `Aura_Step` không báo lỗi `Update` của Jolt. Với cache 2^20 lỗi này khó xảy ra nhưng không còn được phát hiện nếu xảy ra. Cần nâng ABI.
2. Cache 2^20 làm Jolt xin tạm tới khoảng 528 MB trong một bước đầy tải. Chưa đo đỉnh bộ nhớ trên thiết bị di động. Đây là rủi ro lớn nhất của bản sửa.
3. `RestoreState` 3D chưa chính xác từng bit.
4. Crash hiếm của Box2D `b2Solve` (soak seed 1, episode 847) chưa giải quyết.
5. Chưa chạy ThreadSanitizer.
6. Chưa chạy CI trên GitHub; chưa build thử Android/iOS.
7. Chưa soak ở quy mô lớn dưới Guard Malloc (chỉ chạy suite).

## 7. Kiểm thử

- Suite kernel: 459 case pass, chạy thường và dưới Guard Malloc thật (đã thấy banner `GuardMalloc[`).
- Package O (mới): đống 6.000 box 3D, đống 12.000 box 2D, vượt giới hạn body.
- Package M (68 oracle rigid-body) và N (52 oracle khớp, trường, nhân vật, CCD, xe): tất cả pass.
- Chưa kiểm: Editor EditMode, smoke test các scene, FPS, lifecycle với dylib mới. Dylib mới đã bị `build_plugin.sh` chép đè vào `Assets/Plugins` nhưng chưa commit và Unity chưa khởi động lại.

## 8. Lỗi kernel khác do oracle tìm ra (chưa sửa, ngoài ưu tiên)

- Jolt: contact impulse luôn bằng 0 (`aura_jolt_contacts.cpp:54`). Box2D: impulse chỉ lấy điểm đầu và phát sinh self-contact (box, box).
- Nhân vật 3D: sau khi rơi, `Velocity.Y` giữ khoảng −6 m/s; tốc độ leo dốc 30° giảm từ 3 xuống 0 m/s sau cú rơi 2,1 m; không bị bệ di động mang theo.
- Chassis xe đang ngủ không chạy vì `Aura_SetVehicleInput` không kích hoạt body.
- Khớp Box2D mềm hơn Jolt (dầm 10 kg võng 1,6 cm so với 0,64 cm).

## 9. Đề xuất bước tiếp theo

1. Báo lỗi `Update` của Jolt qua `Aura_Step` (nâng ABI lên 13).
2. Đo đỉnh bộ nhớ và cân nhắc cache theo `initialBodyCapacity` để thiết bị di động không xin 528 MB.
3. Soak ở 10.000+ body dưới Guard Malloc; thêm ngưỡng CI cho 5.000 và 10.000 body.
4. Triển khai dylib mới, khởi động lại Unity, chạy smoke/EditMode/FPS rồi mới commit các scene.
5. Sau đó quay lại các lỗi ở mục 8 và scene demo.
