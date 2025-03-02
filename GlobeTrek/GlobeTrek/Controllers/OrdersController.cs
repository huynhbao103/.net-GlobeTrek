using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using GlobeTrek.Models;

namespace GlobeTrek.Controllers
{
    public class OrdersController : Controller
    {
        private readonly TravelDBEntities _context;

        public OrdersController()
        {
            _context = new TravelDBEntities();
        }
        // Phương thức GetUserIdFromSession
        private int GetUserIdFromSession()
        {
            var userId = Session["UserID"]; // Lấy giá trị từ session

            if (userId != null)
            {
                // Kiểm tra xem giá trị có thể chuyển thành số nguyên không
                if (int.TryParse(userId.ToString(), out int result))
                {
                    return result; // Trả về giá trị nếu chuyển thành công
                }
                else
                {
                    // Ghi log thông báo khi giá trị không thể chuyển thành int
                    Console.WriteLine($"Giá trị UserId trong session không hợp lệ: {userId}");
                    return 0; // Trả về 0 nếu không thể chuyển thành int
                }
            }

            // Trả về 0 nếu không có giá trị trong session
            return 0;
        }


        [HttpGet]
        //[Authorize(Roles = "User")]
        public ActionResult CreateOrder(int? tourId)
        {
            try
            {
                var userId = GetUserIdFromSession();

                if (userId == 0)
                {
                    // Log lỗi khi không thể lấy userId hợp lệ từ session
                    Console.WriteLine("UserId không hợp lệ hoặc không tồn tại trong session.");
                    return Json(new { success = false, message = "Có lỗi xảy ra: UserId không hợp lệ." });
                }

                // Kiểm tra tourId có hợp lệ không
                if (!tourId.HasValue || tourId.Value <= 0)
                {
                    return Json(new { success = false, message = "TourId không hợp lệ." });
                }

                // Tiến hành tạo order nếu mọi thứ hợp lệ
                var model = new Order
                {
                    userId = userId,
                    tourId = tourId.Value,
                    bookingDate = DateTime.Now,
                    status = "Pending"
                };

                return View(model);
            }
            catch (FormatException ex)
            {
                // Ghi log lỗi FormatException và trả về thông báo chi tiết
                Console.WriteLine($"FormatException: {ex.Message}");
                return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
            }
            catch (Exception ex)
            {
                // Ghi log lỗi chung và trả về thông báo chung
                Console.WriteLine($"Exception: {ex.Message}");
                return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
            }
        }

        private async Task<bool> UpdateAvailabilityOnCreateOrder(int tourId, DateTime bookingDate, int adultCount, int childCount)
        {
            Console.WriteLine($"Bắt đầu cập nhật số vé cho tour {tourId} vào ngày {bookingDate}");

            var tour = await _context.Tours
                .Include(t => t.TourAvailabilities)
                .FirstOrDefaultAsync(t => t.id == tourId);

            if (tour == null)
            {
                Console.WriteLine($"Không tìm thấy tour với ID {tourId}");
                return false;
            }

            var selectedAvailability = tour.TourAvailabilities
                .FirstOrDefault(a => a.date.Date == bookingDate.Date);

            if (selectedAvailability == null)
            {
                Console.WriteLine($"Không tìm thấy số lượng vé cho ngày {bookingDate}");
                return false;
            }

            int totalSeatsRequested = adultCount + childCount;
            if (selectedAvailability.availableSeats < totalSeatsRequested)
            {
                Console.WriteLine($"Không đủ số vé. Còn lại: {selectedAvailability.availableSeats}, Yêu cầu: {totalSeatsRequested}");
                return false;
            }

            // Update seats with explicit tracking
            selectedAvailability.availableSeats -= totalSeatsRequested;
            _context.Entry(selectedAvailability).State = EntityState.Modified;
            Console.WriteLine($"Số vé còn lại sau khi cập nhật: {selectedAvailability.availableSeats}");

            await _context.SaveChangesAsync();
            return true;
        }

        private async Task<bool> UpdateAvailabilityOnCancelOrder(int tourId, DateTime bookingDate, int adultCount, int childCount)
        {
            Console.WriteLine($"Bắt đầu cập nhật số vé khi hủy cho tour {tourId} vào ngày {bookingDate}");

            var tour = await _context.Tours
                .Include(t => t.TourAvailabilities)
                .FirstOrDefaultAsync(t => t.id == tourId);

            if (tour == null)
            {
                Console.WriteLine($"Không tìm thấy tour với ID {tourId}");
                return false;
            }

            var selectedAvailability = tour.TourAvailabilities
                .FirstOrDefault(a => a.date.Date == bookingDate.Date);

            if (selectedAvailability == null)
            {
                Console.WriteLine($"Không tìm thấy số lượng vé cho ngày {bookingDate}");
                return false;
            }

            int totalSeatsToReturn = adultCount + childCount;
            selectedAvailability.availableSeats += totalSeatsToReturn;
            _context.Entry(selectedAvailability).State = EntityState.Modified;
            Console.WriteLine($"Số vé còn lại sau khi hủy: {selectedAvailability.availableSeats}");

            await _context.SaveChangesAsync();
            return true;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> CreateOrder(int tourId, decimal adultPrice, decimal childPrice,
            int adultCount, int childCount, decimal totalValue, DateTime bookingDate,
            string paymentMethod, string customerName, string customerEmail,
            string customerPhone, string status = "Pending")
        {
            try
            {
                var userId = GetUserIdFromSession();
                if (userId == 0)
                {
                    TempData["Error"] = "UserId không hợp lệ.";
                    return RedirectToAction("Index", "getAllTour");
                }

                if (string.IsNullOrWhiteSpace(customerName) || string.IsNullOrWhiteSpace(customerEmail) ||
                    string.IsNullOrWhiteSpace(customerPhone))
                {
                    TempData["Error"] = "Thông tin khách hàng không đầy đủ. Vui lòng nhập đầy đủ thông tin.";
                    return RedirectToAction("Index", "getAllTour");
                }

                bool isUpdated = await UpdateAvailabilityOnCreateOrder(tourId, bookingDate, adultCount, childCount);
                if (!isUpdated)
                {
                    TempData["Error"] = "Không đủ chỗ trống cho ngày đã chọn.";
                    return RedirectToAction("Index", "getAllTour");
                }

                var checkAvailability = await _context.TourAvailabilities
                    .FirstOrDefaultAsync(a => a.tourId == tourId && a.date == bookingDate);

                if (checkAvailability != null)
                {
                    Console.WriteLine($"Sau khi cập nhật: Số vé còn lại cho {bookingDate} là {checkAvailability.availableSeats}");
                }

                using (var transaction = _context.Database.BeginTransaction())
                {
                    var order = new Order
                    {
                        userId = userId,
                        tourId = tourId,
                        adultPrice = adultPrice,
                        childPrice = childPrice,
                        adultCount = adultCount,
                        childCount = childCount,
                        totalValue = totalValue,
                        bookingDate = bookingDate,
                        paymentMethod = paymentMethod,
                        status = status,
                        createdAt = DateTime.UtcNow,
                        updatedAt = DateTime.UtcNow,
                        CustomerInfoes = new HashSet<CustomerInfo>()
                    };

                    order.CustomerInfoes.Add(new CustomerInfo
                    {
                        fullName = customerName,
                        email = customerEmail,
                        phone = customerPhone
                    });

                    _context.Orders.Add(order);
                    await _context.SaveChangesAsync();

                    if (!order.CustomerInfoes.Any() || order.CustomerInfoes.First().id == 0)
                    {
                        transaction.Rollback();
                        TempData["Error"] = "Không thể lưu thông tin khách hàng. Đơn hàng không được tạo.";
                        return RedirectToAction("Index", "getAllTour");
                    }

                    transaction.Commit();
                    TempData["Success"] = $"Đơn hàng #{order.id} đã được tạo thành công!";
                    return RedirectToAction("Index", "getAllTour"); 
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
                TempData["Error"] = $"Có lỗi xảy ra: {ex.Message}";
                return RedirectToAction("Index", "getAllTour");
            }
        }


        public ActionResult MyOrders()
        {
      
            var userIdString = Session["UserID"]?.ToString();
            if (string.IsNullOrEmpty(userIdString))
            {
                return RedirectToAction("LoginUser", "Customer"); 
            }

           
            if (!int.TryParse(userIdString, out int userId))
            {
                System.Diagnostics.Debug.WriteLine($"Không thể chuyển đổi UserID '{userIdString}' sang số nguyên.");
                return RedirectToAction("LoginUser", "Customer");
            }

            var orders = _context.Orders
                .Where(o => o.userId == userId)
                .OrderByDescending(o => o.createdAt)
                .ToList();

           
            return View(orders);
        }
        [HttpPost]
        public async Task<ActionResult> CancelOrder(int orderId)
        {
            var userIdString = Session["UserID"]?.ToString();
            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId))
            {
                return Json(new { success = false, message = "Vui lòng đăng nhập lại." });
            }

            try
            {
                var order = await _context.Orders
                    .FirstOrDefaultAsync(o => o.id == orderId && o.userId == userId);
                if (order == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy đơn hàng." });
                }

                if (order.status != "Pending")
                {
                    return Json(new { success = false, message = "Chỉ có thể hủy đơn hàng đang chờ." });
                }

                using (var transaction = _context.Database.BeginTransaction())
                {
                    try
                    {
                        // Cập nhật số vé khi hủy
                        bool isUpdated = await UpdateAvailabilityOnCancelOrder(order.tourId, order.bookingDate, order.adultCount, order.childCount);
                        if (!isUpdated)
                        {
                            transaction.Rollback();
                            return Json(new { success = false, message = "Không thể cập nhật số vé khi hủy đơn hàng." });
                        }

                        // Cập nhật trạng thái đơn hàng
                        order.status = "canceled";
                        await _context.SaveChangesAsync();

                        transaction.Commit();

                        return Json(new { success = true, message = "Đơn hàng đã được hủy!", newStatus = order.status });
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        Console.WriteLine($"Lỗi khi hủy đơn hàng {orderId}: {ex.Message}");
                        if (ex.InnerException != null)
                        {
                            Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                            if (ex.InnerException.InnerException != null)
                            {
                                Console.WriteLine($"Inner Inner Exception: {ex.InnerException.InnerException.Message}");
                            }
                        }
                        return Json(new { success = false, message = $"Lỗi khi hủy đơn hàng: {ex.Message}. Chi tiết: {ex.InnerException?.Message}" });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi không mong đợi trong CancelOrder: {ex.Message}");
                return Json(new { success = false, message = "Có lỗi xảy ra khi xử lý yêu cầu." });
            }
        }
        //// thanh toán 
        //[HttpPost]
        //public ActionResult PayOrder(int orderId)
        //{
        //    var userIdString = Session["UserID"]?.ToString();
        //    if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId))
        //    {
        //        return Json(new { success = false, message = "Vui lòng đăng nhập lại." });
        //    }

        //    var order = _context.Orders.FirstOrDefault(o => o.id == orderId && o.userId == userId);
        //    if (order == null)
        //    {
        //        return Json(new { success = false, message = "Không tìm thấy đơn hàng." });
        //    }

        //    if (order.status != "Pending")
        //    {
        //        return Json(new { success = false, message = "Đơn hàng này không thể thanh toán." });
        //    }

        //    // Giả lập thanh toán (có thể thay bằng tích hợp cổng thanh toán như VNPay)
        //    order.status = "paid";
        //    _context.SaveChanges();

        //    return Json(new { success = true, message = "Thanh toán thành công!", newStatus = order.status });
        //}
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _context.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}