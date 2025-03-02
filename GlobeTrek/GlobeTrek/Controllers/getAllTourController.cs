using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using System.Windows.Forms;
using GlobeTrek.Models;

namespace GlobeTrek.Controllers
{
    public class getAllTourController : Controller
    {
        private TravelDBEntities db = new TravelDBEntities();




        // GET: getAllTour
        public ActionResult Index()
        {
            var tours = db.Tours
                .Include(t => t.Destination)
                .Include(t => t.TourType)
                .Where(t => t.isDisabled == true && t.isApproved == true)
                .ToList();
            return View(tours.ToList());
        }


        public ActionResult GetToursByTypeAndDestination(int tourTypeId, int destinationId)
        {
            var tours = db.Tours
                .Include(t => t.TourType)
                .Include(t => t.Destination)
                .Where(t => t.TourType.id == tourTypeId &&
                            t.Destination.id == destinationId &&
                            t.isDisabled == true &&
                            t.isApproved == true)
                .ToList();

            return View("Index", tours);
        }

        public ActionResult Details(int id)
        {
            if (id == 0)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            var tour = db.Tours
                .Include(t => t.TourType)
                .Include(t => t.Destination)
                .Include(t => t.TourAvailabilities)
                .FirstOrDefault(t => t.id == id);

            if (tour == null)
            {
                return HttpNotFound();
            }

            // Chuyển danh sách ngày có sẵn thành danh sách tĩnh
            var availableDates = tour.TourAvailabilities
                .Where(ta => ta.availableSeats > 0)
                .OrderBy(ta => ta.date)
                .ToList();

            ViewBag.AvailableDates = availableDates;

            return View(tour);
        }

        public ActionResult GetAllTours(string search, int? tourTypeId, decimal? minPrice, decimal? maxPrice)
        {
            var tours = db.Tours
                .Include(t => t.TourType)
                .ToList();
            var tourTypes = db.TourTypes.ToList();
            // Lọc theo tên
            if (!string.IsNullOrEmpty(search))
            {
                string searchLower = search.ToLower();
                tours = tours.Where(t => t.title.ToLower().Contains(searchLower)).ToList();
            }

            // Lọc theo loại tour
            if (tourTypeId.HasValue)
            {
                tours = tours.Where(t => t.tourTypeId == tourTypeId.Value).ToList();
            }

            // Lọc theo khoảng giá
            if (minPrice.HasValue)
            {
                tours = tours.Where(t => t.price >= minPrice.Value).ToList();
            }

            if (maxPrice.HasValue)
            {
                tours = tours.Where(t => t.price <= maxPrice.Value).ToList();
            }

            ViewBag.TourTypes = db.TourTypes.ToList();
            return View(tours);
        }

        public ActionResult Booking(int? id)
        {
            if (id == null || id == 0)
            {
                return RedirectToAction("Index", "Tour"); // Điều hướng nếu thiếu tourId
            }

            var tour = db.Tours.Find(id.Value); // Lấy 1 tour theo ID

            if (tour == null)
            {
                return HttpNotFound();
            }

            return View(tour); // Trả về object Tour cho view
        }


        // POST: FavoriteTours/AddFavorite
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddFavorite(int tourId)
        {
            var userIdString = Session["UserID"]?.ToString();
            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId))
            {
                return Json(new { success = false, message = "Vui lòng đăng nhập để thêm tour yêu thích." });
            }

            var existingFavorite = db.FavoriteTours
                .FirstOrDefault(ft => ft.userId == userId && ft.tourId == tourId);

            if (existingFavorite != null)
            {
                return Json(new { success = false, message = "Tour này đã có trong danh sách yêu thích." });
            }

            var newFavorite = new FavoriteTour
            {
                userId = userId,
                tourId = tourId,
                createdAt = DateTime.Now,
                updatedAt = DateTime.Now
            };

            db.FavoriteTours.Add(newFavorite);
            db.SaveChanges();

            return Json(new { success = true, message = "Đã thêm tour vào danh sách yêu thích." });
        }

    }
}