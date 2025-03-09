using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using GlobeTrek.Models;

namespace GlobeTrek.Controllers
{
    public class PromotionsController : Controller
    {
        private TravelDBEntities db = new TravelDBEntities();

        // GET: Promotions/Index
        public ActionResult Index()
        {
            var promotions = db.Promotions
                .Include(p => p.Tour)
                .Where(p => p.IsActive == true && p.EndDate >= DateTime.Now) // Chỉ hiển thị khuyến mãi còn hiệu lực
                .Select(p => new TourPromotionViewModel
                {
                    TourId = p.TourId,
                    TourName = p.Tour.title, // Giả sử Tour có thuộc tính "title"
                    OriginalPrice = p.Tour.price, // Giả sử Tour có thuộc tính "Price"
                    DiscountedPrice = p.Tour.price - p.Discount, // Tính giá sau giảm
                    StartDate = p.StartDate,
                    EndDate = p.EndDate
                })
                .ToList();

            return View(promotions);
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

    // ViewModel để hiển thị tour giảm giá
    public class TourPromotionViewModel
    {
        public int TourId { get; set; }
        public string TourName { get; set; }
        public decimal OriginalPrice { get; set; }
        public decimal DiscountedPrice { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }
}