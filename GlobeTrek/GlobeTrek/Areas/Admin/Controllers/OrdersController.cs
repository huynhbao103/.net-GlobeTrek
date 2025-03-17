using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using GlobeTrek.Models;

namespace GlobeTrek.Areas.Admin.Controllers
{
    public class OrdersController : Controller
    {
        private TravelDBEntities db = new TravelDBEntities();

        // GET: Admin/Orders
        public ActionResult Index()
        {
            // Include CustomerInfoes cùng với Tour và User
            var orders = db.Orders
                .Include(o => o.Tour)
                .Include(o => o.User)
                .Include(o => o.CustomerInfoes); 

            return View(orders.ToList());
        }
        // POST: Admin/Home/UpdateStatus
        [HttpPost]
        public JsonResult UpdateStatus(int id, string newStatus)
        {
            try
            {
                var order = db.Orders.Find(id);
                if (order == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy đơn hàng." });
                }

                // Kiểm tra logic chuyển trạng thái
                bool isValidTransition = false;
                switch (order.status.ToLower())
                {
                    case "pending":
                        isValidTransition = newStatus == "processing" || newStatus == "paid" || newStatus == "canceled";
                        break;
                    case "processing":
                        isValidTransition = newStatus == "paid" || newStatus == "canceled";
                        break;
                    case "paid":
                    case "canceled":
                        isValidTransition = false; // Không thể chuyển
                        break;
                }

                if (!isValidTransition)
                {
                    return Json(new { success = false, message = "Không thể chuyển trạng thái này." });
                }

                // Cập nhật trạng thái
                order.status = newStatus;
                order.updatedAt = DateTime.Now; // Cập nhật thời gian thay đổi
                db.SaveChanges();

                return Json(new { success = true, message = "Cập nhật trạng thái thành công!" });
            }
            catch (Exception ex)
            {
                // Ghi log chi tiết lỗi
                return Json(new { success = false, message = $"Lỗi: {ex.Message}, InnerException: {(ex.InnerException != null ? ex.InnerException.Message : "N/A")}" });
            }
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