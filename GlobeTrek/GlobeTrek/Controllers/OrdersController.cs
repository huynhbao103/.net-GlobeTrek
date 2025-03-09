using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using GlobeTrek.Models;
using PayPal.Api;
using System.Configuration;
using System.Net.Mail;
using System.Net;

namespace GlobeTrek.Controllers
{
    public class OrdersController : Controller
    {
        private readonly TravelDBEntities _context;

        public OrdersController()
        {
            _context = new TravelDBEntities();
        }

        private int? GetUserIdFromSession()
        {
            var userId = Session["UserID"];
            if (userId != null && int.TryParse(userId.ToString(), out int result))
            {
                return result;
            }
            return null;
        }

        [HttpGet]
        public ActionResult CreateOrder(int? tourId)
        {
            try
            {
                if (!tourId.HasValue || tourId.Value <= 0)
                {
                    return Json(new { success = false, message = "TourId không hợp lệ." });
                }

                var model = new GlobeTrek.Models.Order
                {
                    userId = GetUserIdFromSession(),
                    tourId = tourId.Value,
                    orderDate = DateTime.Now,
                    bookingDate = DateTime.Now,
                    status = "pending",
                    paymentMethod = "pointer-wallet" // Giá trị mặc định
                };

                return View(model);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception: {ex.Message}");
                return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
            }
        }

        private async Task<bool> UpdateAvailabilityOnCreateOrder(int tourId, DateTime bookingDate, int adultCount, int childCount)
        {
            Console.WriteLine($"Bắt đầu cập nhật số vé cho tour {tourId} vào ngày {bookingDate}");
            var tour = await _context.Tours.Include(t => t.TourAvailabilities).FirstOrDefaultAsync(t => t.id == tourId);

            if (tour == null)
            {
                Console.WriteLine($"Không tìm thấy tour với ID {tourId}");
                return false;
            }

            var selectedAvailability = tour.TourAvailabilities.FirstOrDefault(a => a.date.Date == bookingDate.Date);
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

            selectedAvailability.availableSeats -= totalSeatsRequested;
            _context.Entry(selectedAvailability).State = EntityState.Modified;
            Console.WriteLine($"Số vé còn lại sau khi cập nhật: {selectedAvailability.availableSeats}");

            await _context.SaveChangesAsync();
            return true;
        }

        private async Task<bool> UpdateAvailabilityOnCancelOrder(int tourId, DateTime bookingDate, int adultCount, int childCount)
        {
            Console.WriteLine($"Bắt đầu cập nhật số vé khi hủy cho tour {tourId} vào ngày {bookingDate}");
            var tour = await _context.Tours.Include(t => t.TourAvailabilities).FirstOrDefaultAsync(t => t.id == tourId);

            if (tour == null)
            {
                Console.WriteLine($"Không tìm thấy tour với ID {tourId}");
                return false;
            }

            var selectedAvailability = tour.TourAvailabilities.FirstOrDefault(a => a.date.Date == bookingDate.Date);
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
            string customerPhone, string status = "pending")
        {
            try
            {
                var userId = GetUserIdFromSession();

                if (string.IsNullOrWhiteSpace(customerName) || string.IsNullOrWhiteSpace(customerEmail) ||
                    string.IsNullOrWhiteSpace(customerPhone))
                {
                    TempData["Error"] = "Thông tin khách hàng không đầy đủ. Vui lòng nhập đầy đủ thông tin.";
                    return RedirectToAction("Index", "getAllTour");
                }

                if (adultCount < 0 || childCount < 0 || totalValue < 0)
                {
                    TempData["Error"] = "Số lượng người hoặc tổng giá trị không hợp lệ.";
                    return RedirectToAction("Index", "getAllTour");
                }

                bool isUpdated = await UpdateAvailabilityOnCreateOrder(tourId, bookingDate, adultCount, childCount);
                if (!isUpdated)
                {
                    TempData["Error"] = "Không đủ chỗ trống cho ngày đã chọn.";
                    return RedirectToAction("Index", "getAllTour");
                }

                using (var transaction = _context.Database.BeginTransaction())
                {
                    var order = new GlobeTrek.Models.Order
                    {
                        userId = userId,
                        tourId = tourId,
                        orderDate = DateTime.Now,
                        adultPrice = adultPrice,
                        childPrice = childPrice,
                        adultCount = adultCount,
                        childCount = childCount,
                        totalValue = totalValue,
                        bookingDate = bookingDate,
                        paymentMethod = paymentMethod,
                        status = status,
                        createdAt = DateTime.Now,
                        updatedAt = DateTime.Now,
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

                    if (paymentMethod == "paypal")
                    {
                        try
                        {
                            var apiContext = PaypalConfiguration.GetAPIContext();
                            string baseURI = $"{Request.Url.Scheme}://{Request.Url.Authority}/Orders/PaymentWithPaypal?";
                            var guid = Convert.ToString((new Random()).Next(100000));
                            var createdPayment = CreatePayment(apiContext, baseURI + "guid=" + guid, order);

                            var links = createdPayment.links.GetEnumerator();
                            string paypalRedirectUrl = null;
                            while (links.MoveNext())
                            {
                                if (links.Current.rel.ToLower().Trim().Equals("approval_url"))
                                {
                                    paypalRedirectUrl = links.Current.href;
                                }
                            }

                            if (paypalRedirectUrl == null)
                            {
                                TempData["Error"] = "Không thể tạo thanh toán PayPal: Không tìm thấy approval_url.";
                                return RedirectToAction("Index", "getAllTour");
                            }

                            Session.Add(guid, createdPayment.id);
                            return Redirect(paypalRedirectUrl);
                        }
                        catch (Exception paypalEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"PayPal Error: {paypalEx.Message}");
                            if (paypalEx.InnerException != null)
                            {
                                System.Diagnostics.Debug.WriteLine($"Inner Exception: {paypalEx.InnerException.Message}");
                            }
                            System.Diagnostics.Debug.WriteLine($"Stack Trace: {paypalEx.StackTrace}");
                            TempData["Error"] = $"Lỗi khi khởi tạo thanh toán PayPal: {paypalEx.Message}" +
                                (paypalEx.InnerException != null ? $" (Inner: {paypalEx.InnerException.Message})" : "");
                            return RedirectToAction("Index", "getAllTour");
                        }
                    }

                    return RedirectToAction("Index", "getAllTour");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"General Error: {ex.Message}");
                TempData["Error"] = $"Có lỗi xảy ra: {ex.Message}";
                return RedirectToAction("Index", "getAllTour");
            }
        }

        public ActionResult MyOrders(string email = null)
        {
            var userId = GetUserIdFromSession();

            try
            {
                if (userId.HasValue)
                {
                    var ordersByUserId = _context.Orders
                        .Where(o => o.userId == userId.Value)
                        .OrderByDescending(o => o.createdAt)
                        .ToList();
                    return View(ordersByUserId);
                }
                else if (!string.IsNullOrWhiteSpace(email))
                {
                    var ordersByCustomerInfo = _context.Orders
                        .Include(o => o.CustomerInfoes)
                        .Where(o => o.CustomerInfoes.Any(c => c.email == email))
                        .OrderByDescending(o => o.createdAt)
                        .ToList();

                    if (!ordersByCustomerInfo.Any())
                    {
                        TempData["Info"] = "Không tìm thấy đơn hàng nào khớp với email cung cấp.";
                    }
                    return View(ordersByCustomerInfo);
                }
                else
                {
                    TempData["Info"] = "Vui lòng nhập email để tìm kiếm đơn hàng.";
                    return View(new List<GlobeTrek.Models.Order>());
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi trong MyOrders: {ex.Message}");
                TempData["Error"] = "Có lỗi xảy ra khi tải danh sách đơn hàng.";
                return View(new List<GlobeTrek.Models.Order>());
            }
        }

        [HttpPost]
        public async Task<ActionResult> CancelOrder(int orderId)
        {
            var userId = GetUserIdFromSession();

            try
            {
                var order = await _context.Orders.FirstOrDefaultAsync(o => o.id == orderId && o.userId == userId);
                if (order == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy đơn hàng." });
                }

                if (order.status != "pending")
                {
                    return Json(new { success = false, message = "Chỉ có thể hủy đơn hàng đang chờ." });
                }

                using (var transaction = _context.Database.BeginTransaction())
                {
                    bool isUpdated = await UpdateAvailabilityOnCancelOrder(order.tourId, order.bookingDate, order.adultCount, order.childCount);
                    if (!isUpdated)
                    {
                        transaction.Rollback();
                        return Json(new { success = false, message = "Không thể cập nhật số vé khi hủy đơn hàng." });
                    }

                    order.status = "canceled";
                    order.updatedAt = DateTime.Now;
                    await _context.SaveChangesAsync();

                    transaction.Commit();
                    return Json(new { success = true, message = "Đơn hàng đã được hủy!", newStatus = order.status });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi trong CancelOrder: {ex.Message}");
                return Json(new { success = false, message = "Có lỗi xảy ra khi xử lý yêu cầu." });
            }
        }

        // Action mới để thanh toán đơn hàng chưa thanh toán
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> PayOrder(int orderId, string email = null)
        {
            System.Diagnostics.Debug.WriteLine($"PayOrder called with orderId: {orderId}, email: {email}");
            var userId = GetUserIdFromSession();

            try
            {
                GlobeTrek.Models.Order order; // Chỉ rõ namespace
                if (userId.HasValue)
                {
                    // Người dùng đã đăng nhập: kiểm tra theo userId
                    order = await _context.Orders.FirstOrDefaultAsync(o => o.id == orderId && o.userId == userId);
                    System.Diagnostics.Debug.WriteLine($"Checking order for userId: {userId}");
                }
                else if (!string.IsNullOrWhiteSpace(email))
                {
                    // Khách vãng lai: kiểm tra theo email trong CustomerInfoes
                    order = await _context.Orders
                        .Include(o => o.CustomerInfoes)
                        .FirstOrDefaultAsync(o => o.id == orderId && o.CustomerInfoes.Any(c => c.email == email));
                    System.Diagnostics.Debug.WriteLine($"Checking order for email: {email}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("No userId or email provided.");
                    return Json(new { success = false, message = "Vui lòng đăng nhập hoặc cung cấp email để thanh toán." });
                }

                if (order == null)
                {
                    System.Diagnostics.Debug.WriteLine("Order not found.");
                    return Json(new { success = false, message = "Không tìm thấy đơn hàng." });
                }

                if (order.status != "pending")
                {
                    System.Diagnostics.Debug.WriteLine("Order status is not pending.");
                    return Json(new { success = false, message = "Chỉ có thể thanh toán đơn hàng đang chờ." });
                }

                var apiContext = PaypalConfiguration.GetAPIContext();
                System.Diagnostics.Debug.WriteLine("PayPal API context initialized.");
                string baseURI = $"{Request.Url.Scheme}://{Request.Url.Authority}/Orders/PaymentWithPaypal?";
                var guid = Convert.ToString((new Random()).Next(100000));
                var createdPayment = CreatePayment(apiContext, baseURI + "guid=" + guid, order);

                var links = createdPayment.links.GetEnumerator();
                string paypalRedirectUrl = null;
                while (links.MoveNext())
                {
                    if (links.Current.rel.ToLower().Trim().Equals("approval_url"))
                    {
                        paypalRedirectUrl = links.Current.href;
                        break;
                    }
                }

                if (paypalRedirectUrl == null)
                {
                    System.Diagnostics.Debug.WriteLine("No approval_url found in PayPal response.");
                    return Json(new { success = false, message = "Không thể tạo thanh toán PayPal: Không tìm thấy approval_url." });
                }

                Session.Add(guid, createdPayment.id);
                System.Diagnostics.Debug.WriteLine($"Redirect URL: {paypalRedirectUrl}");
                return Json(new { success = true, redirectUrl = paypalRedirectUrl });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"PayOrder Error: {ex.Message}");
                if (ex.InnerException != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
                return Json(new { success = false, message = $"Lỗi khi khởi tạo thanh toán: {ex.Message}" });
            }
        }

        [HttpGet]
        public async Task<ActionResult> PaymentWithPaypal(string guid, string PayerID, string Cancel = null)
        {
            var apiContext = PaypalConfiguration.GetAPIContext();

            if (!string.IsNullOrEmpty(Cancel))
            {
                TempData["Info"] = "Thanh toán đã bị hủy.";
                return RedirectToAction("MyOrders");
            }

            try
            {
                string payerId = PayerID;
                if (string.IsNullOrEmpty(payerId))
                {
                    return HttpNotFound("Thiếu thông tin PayerID từ PayPal.");
                }

                var paymentId = Session[guid] as string;
                if (string.IsNullOrEmpty(paymentId))
                {
                    return HttpNotFound("Không tìm thấy paymentId trong session.");
                }

                var executedPayment = ExecutePayment(apiContext, payerId, paymentId);
                if (executedPayment.state.ToLower() != "approved")
                {
                    TempData["Error"] = "Thanh toán không được phê duyệt.";
                    return RedirectToAction("MyOrders");
                }

                var orderId = int.Parse(executedPayment.transactions[0].invoice_number);
                var order = await _context.Orders.FirstOrDefaultAsync(o => o.id == orderId);
                if (order != null)
                {
                    order.status = "paid";
                    order.paymentMethod = "paypal"; // Cập nhật phương thức thanh toán
                    order.updatedAt = DateTime.Now;
                    await _context.SaveChangesAsync();
                    await SendOrderConfirmationEmail(order);
                    TempData["Success"] = $"Thanh toán đơn hàng #{orderId} thành công!";
                }

                Session.Remove(guid);
                return RedirectToAction("MyOrders");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi trong PaymentWithPaypal: {ex.Message}");
                TempData["Error"] = "Có lỗi xảy ra khi xử lý thanh toán.";
                return RedirectToAction("MyOrders");
            }
        }

        private Payment CreatePayment(APIContext apiContext, string redirectUrl, GlobeTrek.Models.Order order)
        {
            var itemList = new ItemList { items = new List<Item>() };
            itemList.items.Add(new Item
            {
                name = $"Tour #{order.tourId}",
                currency = "USD",
                price = order.totalValue.ToString("F2"),
                quantity = "1",
                sku = order.id.ToString()
            });

            var payer = new Payer { payment_method = "paypal" };
            var redirUrls = new RedirectUrls
            {
                cancel_url = redirectUrl + "&Cancel=true",
                return_url = redirectUrl
            };

            var amount = new Amount
            {
                currency = "USD",
                total = order.totalValue.ToString("F2")
            };

            var transactionList = new List<Transaction>();
            transactionList.Add(new Transaction
            {
                description = $"Thanh toán đơn hàng #{order.id}",
                invoice_number = order.id.ToString(),
                amount = amount,
                item_list = itemList
            });

            var payment = new Payment
            {
                intent = "sale",
                payer = payer,
                transactions = transactionList,
                redirect_urls = redirUrls
            };

            return payment.Create(apiContext);
        }

        private Payment ExecutePayment(APIContext apiContext, string payerId, string paymentId)
        {
            var paymentExecution = new PaymentExecution { payer_id = payerId };
            var payment = new Payment { id = paymentId };
            return payment.Execute(apiContext, paymentExecution);
        }




        //email
        // Phương thức gửi email xác nhận
        private async Task SendOrderConfirmationEmail(GlobeTrek.Models.Order order)
        {
            try
            {
                var customerInfo = order.CustomerInfoes.FirstOrDefault();
                if (customerInfo == null || string.IsNullOrWhiteSpace(customerInfo.email))
                {
                    System.Diagnostics.Debug.WriteLine("Không tìm thấy thông tin email khách hàng.");
                    return;
                }

                // Cấu hình SMTP (ví dụ: dùng Gmail)
                var smtpClient = new SmtpClient("smtp.gmail.com")
                {
                    Port = 587,
                    Credentials = new NetworkCredential("GlobeTrek.huflit@gmail.com", "nall vere bxbu xsbj"), // Thay bằng email và mật khẩu ứng dụng
                    EnableSsl = true,
                };

                // Nội dung email
                var mailMessage = new MailMessage
                {
                    From = new MailAddress("GlobeTrek.huflit@gmail.com", "GlobeTrek"),
                    Subject = $"Xác nhận thanh toán đơn hàng #{order.id}",
                    Body = $@"
        <html>
        <head>
            <style>
                body {{
                    font-family: Arial, sans-serif;
                    background-color: #f4f4f4;
                    margin: 0;
                    padding: 0;
                }}
                .container {{
                    max-width: 600px;
                    margin: 20px auto;
                    background-color: #ffffff;
                    border-radius: 8px;
                    box-shadow: 0 2px 5px rgba(0,0,0,0.1);
                    padding: 20px;
                }}
                .header {{
                    background-color: #D4E8D4; /* Xanh lá nhạt */
                    padding: 15px;
                    text-align: center;
                    border-radius: 8px 8px 0 0;
                }}
                .header h2 {{
                    color: #2E7D32; /* Xanh lá đậm */
                    margin: 0;
                    font-size: 24px;
                }}
                .content {{
                    padding: 20px;
                    color: #333333;
                }}
                .content p {{
                    font-size: 16px;
                    line-height: 1.5;
                }}
                .order-details {{
                    background-color: #F9F9F9;
                    padding: 15px;
                    border-radius: 5px;
                    margin-top: 15px;
                }}
                .order-details h3 {{
                    color: #2E7D32; /* Xanh lá đậm */
                    margin: 0 0 10px 0;
                    font-size: 18px;
                }}
                .order-details ul {{
                    list-style-type: none;
                    padding: 0;
                    margin: 0;
                }}
                .order-details li {{
                    font-size: 14px;
                    padding: 8px 0;
                    border-bottom: 1px solid #EEEEEE;
                }}
                .order-details li:last-child {{
                    border-bottom: none;
                }}
                .footer {{
                    text-align: center;
                    padding: 15px;
                    font-size: 14px;
                    color: #666666;
                }}
                .footer p {{
                    margin: 5px 0;
                }}
            </style>
        </head>
        <body>
            <div class='container'>
                <div class='header'>
                    <h2>Xin chào {customerInfo.fullName}</h2>
                </div>
                <div class='content'>
                    <p>Cảm ơn bạn đã thanh toán thành công đơn hàng tại <strong>GlobeTrek</strong>! Dưới đây là thông tin chi tiết về đơn hàng của bạn:</p>
                    <div class='order-details'>
                        <h3>Thông tin đơn hàng</h3>
                        <ul>
                            <li><strong>Mã đơn hàng:</strong> #{order.id}</li>
                            <li><strong>Tour ID:</strong> {order.tourId}</li>
                            <li><strong>Số người:</strong> {order.adultCount} người lớn, {order.childCount} trẻ em</li>
                            <li><strong>Tổng giá:</strong> {order.totalValue.ToString("N0")} VND</li>
                            <li><strong>Ngày đặt:</strong> {order.orderDate}</li>
                            <li><strong>Ngày đi:</strong> {order.bookingDate.ToString("dd/MM/yyyy")}</li>
                            <li><strong>Phương thức thanh toán:</strong> {order.paymentMethod}</li>
                            <li><strong>Trạng thái:</strong> Đã thanh toán</li>
                        </ul>
                    </div>
                    <p>Chúc bạn có một chuyến đi tuyệt vời cùng GlobeTrek!</p>
                </div>
                <div class='footer'>
                    <p>Trân trọng,</p>
                    <p><strong>Đội ngũ GlobeTrek</strong></p>
                    <p>Email: GlobeTrek.huflit@gmail.com</p>
                </div>
            </div>
        </body>
        </html>",
                    IsBodyHtml = true,
                };
                mailMessage.To.Add(customerInfo.email);

                // Gửi email bất đồng bộ
                await smtpClient.SendMailAsync(mailMessage);
                System.Diagnostics.Debug.WriteLine($"Email xác nhận đã gửi đến {customerInfo.email}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Lỗi khi gửi email: {ex.Message}");
                if (ex.InnerException != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
            }
        }

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