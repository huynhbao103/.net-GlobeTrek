using System;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web.Helpers;
using System.Web.Mvc;
using System.Web.Security;
using GlobeTrek.Models;

namespace GlobeTrek.Controllers
{
    public class CustomerController : Controller
    {
        private TravelDBEntities db = new TravelDBEntities();

        public ActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "id,username,email,password,role,createdAt,updatedAt")] User user)
        {
            if (ModelState.IsValid)
            {
                var existingUser = db.Users.FirstOrDefault(u => u.email == user.email);
                if (existingUser != null)
                {
                    ModelState.AddModelError("email", "Email đã tồn tại.");
                    return View(user);
                }

                if (string.IsNullOrEmpty(user.password) || user.password.Length < 6)
                {
                    ModelState.AddModelError("password", "Mật khẩu phải có ít nhất 6 ký tự.");
                    return View(user);
                }

                user.password = Crypto.HashPassword(user.password);
                user.role = "User";
                user.createdAt = DateTime.Now;
                user.updatedAt = DateTime.Now;

                db.Users.Add(user);
                db.SaveChanges();

                return RedirectToAction("Index", "getAllTour");
            }
            return View(user);
        }

        public ActionResult LoginUser()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult LoginUser(string email, string password)
        {
            var user = db.Users.FirstOrDefault(u => u.email == email);
            if (user != null && Crypto.VerifyHashedPassword(user.password, password))
            {

                FormsAuthentication.SetAuthCookie(user.id.ToString(), false);
                Session["UserName"] = user.username;  // Lưu tên người dùng vào session
                Session["UserID"] = user.id;          // Lưu ID người dùng vào session
                Session["UserRole"] = user.role;      // Lưu role người dùng vào session
                return Json(new { success = true, userId = user.id, userName = user.username });
            }
            return Json(new { success = false, message = "Email hoặc mật khẩu không chính xác." });
        }

        [HttpPost]
        public ActionResult Logout()
        {
            FormsAuthentication.SignOut(); // Clear authentication cookie
            Session.Clear(); // Clear all session variables
            Session.Abandon(); // End the session
            return Json(new { success = true });
        }


        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            if ((int)Session["UserID"] != id)
            {
                return new HttpStatusCodeResult(HttpStatusCode.Forbidden);
            }

            User user = db.Users.Find(id);
            if (user == null)
            {
                return HttpNotFound();
            }

            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "id,username,email,password,role,createdAt,updatedAt")] User user)
        {
            if (ModelState.IsValid)
            {
                int userId = user.id;
                if (Session["UserID"] != null && (int)Session["UserID"] != userId)
                {
                    return new HttpStatusCodeResult(HttpStatusCode.Forbidden);
                }

                var existingUser = db.Users.FirstOrDefault(u => u.id == userId);
                if (existingUser != null)
                {
                    existingUser.username = user.username;
                    existingUser.email = user.email;
                    existingUser.updatedAt = DateTime.Now;

                    if (!string.IsNullOrEmpty(user.password) && user.password.Length >= 6)
                    {
                        existingUser.password = Crypto.HashPassword(user.password);
                    }

                    db.Entry(existingUser).State = EntityState.Modified;

                    try
                    {
                        db.SaveChanges();
                        TempData["SuccessMessage"] = "Cập nhật thông tin tài khoản thành công.";
                        Session["UserName"] = existingUser.username;
                        return RedirectToAction("Index", "getAllTour");
                    }
                    catch (Exception ex)
                    {
                        ModelState.AddModelError("", "Lỗi khi lưu thông tin tài khoản: " + ex.Message);
                        return View(user);
                    }
                }
                return HttpNotFound();
            }
            return View(user);
        }


        public ActionResult AddCustomerInfo()
        {
            if (Session["UserID"] == null)
            {
                return RedirectToAction("LoginUser", "Customer");
            }

            int userId = (int)Session["UserID"];
            var user = db.Users.Find(userId);
            if (user == null)
            {
                return HttpNotFound();
            }

            // Kiểm tra xem đã có CustomerInfo cho userId này chưa
            var customerInfo = db.CustomerInfoes.FirstOrDefault(ci => ci.userId == userId);
            ViewBag.CustomerInfo = customerInfo; // Truyền CustomerInfo vào ViewBag
            ViewBag.UserEmail = user.email;      // Truyền email của User
            ViewBag.UserName = user.username;    // Truyền username của User

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddCustomerInfo(int userId, string fullName, string phone, string email)
        {
            if (Session["UserID"] == null || (int)Session["UserID"] != userId)
            {
                return new HttpStatusCodeResult(HttpStatusCode.Forbidden);
            }

            var user = db.Users.Find(userId);
            if (user == null)
            {
                return HttpNotFound();
            }

            // Kiểm tra xem đã có CustomerInfo chưa
            if (db.CustomerInfoes.Any(ci => ci.userId == userId))
            {
                TempData["ErrorMessage"] = "Bạn đã thêm thông tin khách hàng rồi. Vui lòng sửa hoặc xóa thông tin hiện tại.";
                return RedirectToAction("AddCustomerInfo");
            }

            // Kiểm tra dữ liệu đầu vào
            if (string.IsNullOrEmpty(fullName) || string.IsNullOrEmpty(phone))
            {
                ModelState.AddModelError("", "Vui lòng nhập đầy đủ họ tên và số điện thoại.");
                ViewBag.UserEmail = user.email;
                ViewBag.UserName = user.username; // Truyền lại UserName
                return View();
            }

            // Thêm bản ghi CustomerInfo mới
            var newCustomerInfo = new CustomerInfo
            {
                userId = userId,
                fullName = fullName,
                phone = phone,
                email = user.email, // Đồng bộ email từ User
                orderId = null
            };
            db.CustomerInfoes.Add(newCustomerInfo);

            try
            {
                db.SaveChanges();
                TempData["SuccessMessage"] = "Thêm thông tin khách hàng thành công.";
                return RedirectToAction("AddCustomerInfo"); // Quay lại để hiển thị thông tin vừa thêm
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Lỗi khi thêm thông tin khách hàng: " + ex.Message;
                ViewBag.UserEmail = user.email;
                ViewBag.UserName = user.username; // Truyền lại UserName
                return View();
            }
        }


        public ActionResult EditCustomerInfo(int id)
        {
            if (Session["UserID"] == null)
            {
                return RedirectToAction("LoginUser", "Customer");
            }

            var customerInfo = db.CustomerInfoes.Find(id);
            if (customerInfo == null || customerInfo.userId != (int)Session["UserID"])
            {
                return HttpNotFound();
            }

            var user = db.Users.Find(customerInfo.userId);
            ViewBag.UserEmail = user.email;
            return View(customerInfo);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditCustomerInfo(int id, string fullName, string phone)
        {
            if (Session["UserID"] == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.Forbidden);
            }

            var customerInfo = db.CustomerInfoes.Find(id);
            if (customerInfo == null || customerInfo.userId != (int)Session["UserID"])
            {
                return HttpNotFound();
            }

            if (string.IsNullOrEmpty(fullName) || string.IsNullOrEmpty(phone))
            {
                ModelState.AddModelError("", "Vui lòng nhập đầy đủ họ tên và số điện thoại.");
                var user = db.Users.Find(customerInfo.userId);
                ViewBag.UserEmail = user.email;
                return View(customerInfo);
            }

            customerInfo.fullName = fullName;
            customerInfo.phone = phone;
            db.Entry(customerInfo).State = EntityState.Modified;

            try
            {
                db.SaveChanges();
                TempData["SuccessMessage"] = "Cập nhật thông tin khách hàng thành công.";
                return RedirectToAction("AddCustomerInfo");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Lỗi khi cập nhật thông tin khách hàng: " + ex.Message;
                var user = db.Users.Find(customerInfo.userId);
                ViewBag.UserEmail = user.email;
                return View(customerInfo);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteCustomerInfo(int id)
        {
            if (Session["UserID"] == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.Forbidden);
            }

            var customerInfo = db.CustomerInfoes.Find(id);
            if (customerInfo == null || customerInfo.userId != (int)Session["UserID"])
            {
                return HttpNotFound();
            }

            db.CustomerInfoes.Remove(customerInfo);
            try
            {
                db.SaveChanges();
                TempData["SuccessMessage"] = "Xóa thông tin khách hàng thành công.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Lỗi khi xóa thông tin khách hàng: " + ex.Message;
            }

            return RedirectToAction("AddCustomerInfo");
        }

        // Hiển thị form quên mật khẩu
        public ActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ForgotPassword(string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                ModelState.AddModelError("", "Vui lòng nhập email.");
                return View();
            }

            var user = db.Users.FirstOrDefault(u => u.email == email);
            if (user != null)
            {
                // Tạo token đặt lại mật khẩu (đơn giản là một chuỗi ngẫu nhiên)
                string resetToken = Guid.NewGuid().ToString();

                // Lưu token và email vào Session (hoặc bộ nhớ tạm thời)
                Session["ResetToken"] = resetToken;
                Session["ResetEmail"] = email;
                Session["ResetTokenExpiry"] = DateTime.Now.AddMinutes(30); // Token hết hạn sau 30 phút

                // Gửi email chứa liên kết đặt lại mật khẩu (giả lập)
                string resetLink = Url.Action("ResetPassword", "Customer", new { token = resetToken }, Request.Url.Scheme);
                SendResetPasswordEmail(email, resetLink);

                ViewBag.Message = "Vui lòng kiểm tra email để đặt lại mật khẩu.";
                return View("ForgotPasswordConfirmation");
            }
            else
            {
                ModelState.AddModelError("", "Email không tồn tại trong hệ thống.");
                return View();
            }
        }

        // Hiển thị form đặt lại mật khẩu
        public ActionResult ResetPassword(string token)
        {
            if (string.IsNullOrEmpty(token) || token != Session["ResetToken"]?.ToString())
            {
                ViewBag.ErrorMessage = "Liên kết đặt lại mật khẩu không hợp lệ hoặc đã hết hạn.";
                return View("Error");
            }

            if (DateTime.Now > (DateTime)Session["ResetTokenExpiry"])
            {
                ViewBag.ErrorMessage = "Liên kết đặt lại mật khẩu đã hết hạn.";
                return View("Error");
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ResetPassword(string newPassword, string confirmPassword)
        {
            if (newPassword != confirmPassword)
            {
                ModelState.AddModelError("", "Mật khẩu và xác nhận mật khẩu không khớp.");
                return View();
            }

            string email = Session["ResetEmail"]?.ToString();
            if (string.IsNullOrEmpty(email))
            {
                ViewBag.ErrorMessage = "Liên kết đặt lại mật khẩu không hợp lệ.";
                return View("Error");
            }

            var user = db.Users.FirstOrDefault(u => u.email == email);
            if (user != null)
            {
                // Cập nhật mật khẩu mới
                user.password = Crypto.HashPassword(newPassword);
                db.SaveChanges();

                // Xóa token và email khỏi Session
                Session.Remove("ResetToken");
                Session.Remove("ResetEmail");
                Session.Remove("ResetTokenExpiry");

                ViewBag.Message = "Mật khẩu của bạn đã được đặt lại thành công.";
                return View("ResetPasswordConfirmation");
            }
            else
            {
                ViewBag.ErrorMessage = "Email không tồn tại trong hệ thống.";
                return View("Error");
            }
        }

        // Phương thức gửi email (giả lập)
        private void SendResetPasswordEmail(string email, string resetLink)
        {
            // Giả lập gửi email (bạn có thể tích hợp thư viện gửi email thực tế như SendGrid, SMTP, etc.)
            string subject = "Đặt lại mật khẩu";
            string body = $"Vui lòng nhấp vào liên kết sau để đặt lại mật khẩu: <a href='{resetLink}'>Đặt lại mật khẩu</a>";
            // Gửi email ở đây (giả lập)
            System.Diagnostics.Debug.WriteLine($"Email sent to {email} with link: {resetLink}");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}