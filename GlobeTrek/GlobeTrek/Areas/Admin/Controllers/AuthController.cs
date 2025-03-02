using System;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using GlobeTrek.Models;
using System.Security.Cryptography;
using System.Text;
using GlobeTrek.Filters;

namespace GlobeTrek.Areas.Admin.Controllers
{
    [AdminAuthorize]
    public class AuthController : Controller
    {
        private TravelDBEntities db = new TravelDBEntities();

        // GET: Admin/Auth/Login
        public ActionResult Login()
        {
            return View();
        }
            
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(string email, string password)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                ModelState.AddModelError("", "email và mật khẩu không được để trống.");
                return View();
            }

            string hashedpassword = Hashpassword(password);

            var admin = db.Users.FirstOrDefault(u => u.email == email && u.password == hashedpassword && u.role == "Admin");
            if (admin != null)
            {
                Session["AdminID"] = admin.id;
                Session["Role"] = "Admin";
                return RedirectToAction("Index", "Home", new { area = "Admin" });
            }

            ModelState.AddModelError("", "Tài khoản hoặc mật khẩu không đúng!");
            return View();
        }
            
        // GET: Admin/Auth/Register (Chỉ đăng ký Admin, không cho User)
        public ActionResult Register()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Register([Bind(Include = "username,email,password")] User user)
        {
            if (db.Users.Any(u => u.email == user.email))
            {
                ModelState.AddModelError("email", "email này đã tồn tại!");
                return View(user);
            }

            if (ModelState.IsValid)
            {
                user.password = Hashpassword(user.password); // Mã hóa mật khẩu
                user.role = "Admin"; // Chỉ tạo Admin
                user.createdAt = DateTime.Now;
                user.updatedAt = DateTime.Now;

                db.Users.Add(user);
                db.SaveChanges();
                return RedirectToAction("Login");
            }

            return View(user);
        }

        // GET: Admin/Auth/Logout
        public ActionResult Logout()
        {
            Session.Clear();
            return RedirectToAction("Login");
        }

        private string Hashpassword(string password)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                StringBuilder builder = new StringBuilder();
                foreach (var b in bytes)
                {
                    builder.Append(b.ToString("x2"));
                }
                return builder.ToString();
            }
        }
    }
}
