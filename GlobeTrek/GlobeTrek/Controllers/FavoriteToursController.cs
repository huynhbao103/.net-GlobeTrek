using System;
using System.Collections.Generic;
using System.Data.Entity; // Dùng cho Include
using System.Linq;
using System.Web.Mvc;
using GlobeTrek.Models;

namespace GlobeTrek.Controllers
{
    public class FavoriteToursController : Controller
    {
        private TravelDBEntities db = new TravelDBEntities();

        // GET: FavoriteTours/Index
        // Hiển thị danh sách tour yêu thích của user hiện tại
        public ActionResult Index()
        {
            // Lấy ID của user hiện tại từ Session
            var userIdString = Session["UserID"]?.ToString();
            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId))
            {
                // Nếu không có Session hoặc không parse được thành int, redirect đến login
                return RedirectToAction("LoginUser", "Customer");
            }

            var favoriteTours = db.FavoriteTours
                .Include(ft => ft.Tour)
                .Include(ft => ft.User)
                .Where(ft => ft.userId == userId)
                .ToList();

            return View(favoriteTours);
        }




        // POST: FavoriteTours/RemoveFavorite
        // Xóa tour yêu thích
        [HttpPost]
        [ValidateAntiForgeryToken] // Bảo mật chống CSRF
        public ActionResult RemoveFavorite(int id)
        {
            // Lấy ID của user hiện tại từ Session
            var userIdString = Session["UserID"]?.ToString();
            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId))
            {
                return Json(new { success = false, message = "Vui lòng đăng nhập để thực hiện thao tác này." });
            }

            var favorite = db.FavoriteTours
                .FirstOrDefault(ft => ft.id == id && ft.userId == userId);

            if (favorite == null)
            {
                return Json(new { success = false, message = "Không tìm thấy tour yêu thích." });
            }

            db.FavoriteTours.Remove(favorite);
            db.SaveChanges();

            return Json(new { success = true, message = "Đã xóa tour yêu thích thành công." });
        }

        // Giải phóng tài nguyên
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