using System;
using System.Linq;
using System.Web.Mvc;
using GlobeTrek.Models;

namespace GlobeTrek.Areas.Admin.Controllers
{
    public class HomeController : Controller
    {
        private TravelDBEntities db = new TravelDBEntities();

        // GET: Admin/Home
        public ActionResult Index()
        {
            var paidOrders = db.Orders
                .Where(o => o.status == "paid")
                .ToList();

            // 1. Doanh thu tổng
            ViewBag.TotalRevenue = paidOrders.Any() ? paidOrders.Sum(o => o.totalValue) : 0;

            // 2. Doanh thu theo ngày (dùng createdAt)
            ViewBag.RevenueByDay = paidOrders
                .Where(o => o.createdAt.HasValue)
                .GroupBy(o => o.createdAt.Value.Date)
                .Select(g => new Tuple<DateTime, decimal>(g.Key, g.Sum(o => o.totalValue)))
                .OrderBy(g => g.Item1)
                .ToList();

            // 3. Doanh thu theo tuần (dùng createdAt)
            ViewBag.RevenueByWeek = paidOrders
                .Where(o => o.createdAt.HasValue)
                .GroupBy(o => System.Globalization.CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(
                    o.createdAt.Value, System.Globalization.CalendarWeekRule.FirstDay, DayOfWeek.Monday))
                .Select(g => new Tuple<int, decimal>(g.Key, g.Sum(o => o.totalValue)))
                .OrderBy(g => g.Item1)
                .ToList();

            // 4. Doanh thu theo tháng (dùng createdAt)
            ViewBag.RevenueByMonth = paidOrders
                .Where(o => o.createdAt.HasValue)
                .GroupBy(o => new { o.createdAt.Value.Year, o.createdAt.Value.Month })
                .Select(g => new Tuple<string, decimal>($"{g.Key.Year}-{g.Key.Month:D2}", g.Sum(o => o.totalValue)))
                .OrderBy(g => g.Item1)
                .ToList();

            // 5. Doanh thu theo năm (dùng createdAt)
            ViewBag.RevenueByYear = paidOrders
                .Where(o => o.createdAt.HasValue)
                .GroupBy(o => o.createdAt.Value.Year)
                .Select(g => new Tuple<int, decimal>(g.Key, g.Sum(o => o.totalValue)))
                .OrderBy(g => g.Item1)
                .ToList();

            // 6. Doanh thu theo Tour
            ViewBag.RevenueByTour = paidOrders
                  .GroupBy(o => o.tourId)
                  .Select(g => new Tuple<string, decimal>(
                      db.Tours.Where(t => t.id == g.Key).Select(t => t.title).FirstOrDefault() ?? $"Tour {g.Key}",
                      g.Sum(o => o.totalValue)))
                  .OrderBy(g => g.Item1)
                  .ToList();

            // 7. Doanh thu theo phương thức thanh toán
            ViewBag.RevenueByPaymentMethod = paidOrders
                .GroupBy(o => o.paymentMethod ?? "Không xác định")
                .Select(g => new Tuple<string, decimal>(g.Key, g.Sum(o => o.totalValue)))
                .ToList();

            return View(paidOrders);
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