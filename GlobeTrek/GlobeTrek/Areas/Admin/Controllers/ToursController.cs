using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using GlobeTrek.Models;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using System.Configuration;
using System.IO;
using PagedList;
using System.Drawing.Printing;

namespace GlobeTrek.Areas.Admin.Controllers
{
    public class ToursController : Controller
    {
        private TravelDBEntities db = new TravelDBEntities();

        private string UploadImageToCloudinary(HttpPostedFileBase file)
        {
            if (file == null || file.ContentLength == 0)
            {
                return null;
            }

            var cloudinary = new Cloudinary(new Account(
                ConfigurationManager.AppSettings["CloudinaryCloudName"],
                ConfigurationManager.AppSettings["CloudinaryApiKey"],
                ConfigurationManager.AppSettings["CloudinaryApiSecret"]
            ));

            var uploadParams = new ImageUploadParams()
            {
                File = new FileDescription(file.FileName, file.InputStream),
                Folder = "tour_images"
            };

            var uploadResult = cloudinary.Upload(uploadParams);
            return uploadResult.SecureUrl.ToString();
        }

        // GET: Admin/Tours
        public ActionResult Index(int? page)
        {
            int pageSize = 5;
            int pageNumber = (page ?? 1);

            var tours = db.Tours.Include(t => t.Destination).Include(t => t.TourType)
                                .OrderBy(t => t.id);

            IPagedList<Tour> pagedTours = tours.ToPagedList(pageNumber, pageSize);

            return View(pagedTours);
        }

        // GET: Admin/Tours/Create
        public ActionResult Create()
        {
            ViewBag.destinationId = new SelectList(db.Destinations, "id", "name");
            ViewBag.tourTypeId = new SelectList(db.TourTypes, "id", "name");
            return View();
        }

        private decimal GetPriceMultiplier(DateTime date)
        {
            // Danh sách ngày đặc biệt (hard-coded hoặc từ config)
            var specialDates = new Dictionary<(DateTime Start, DateTime End), (decimal Multiplier, string Description)>
    {
        { (new DateTime(2025, 1, 28), new DateTime(2025, 2, 3)), (1.3m, "Tết Nguyên Đán") }, // Tết 2025
        { (new DateTime(2025, 12, 24), new DateTime(2025, 12, 25)), (1.2m, "Giáng Sinh") }, // Giáng Sinh
        { (new DateTime(2025, 4, 30), new DateTime(2025, 5, 1)), (1.25m, "Ngày Giải Phóng và Quốc Tế Lao Động") }
    };

            foreach (var specialDate in specialDates)
            {
                if (date >= specialDate.Key.Start && date <= specialDate.Key.End)
                {
                    return specialDate.Value.Multiplier; // Trả về hệ số giá
                }
            }

            return 1.0m; // Giá mặc định nếu không phải ngày đặc biệt
        }

        // POST: Admin/Tours/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(Tour tour, List<TourAvailability> availabilities, HttpPostedFileBase image, HttpPostedFileBase video)
        {
            if (ModelState.IsValid)
            {
                tour.isDeleted = false;
                tour.isApproved = false;
                tour.isDisabled = false;
                tour.deletionRequested = false;

                var cloudinary = new Cloudinary(new Account(
                    ConfigurationManager.AppSettings["CloudinaryCloudName"],
                    ConfigurationManager.AppSettings["CloudinaryApiKey"],
                    ConfigurationManager.AppSettings["CloudinaryApiSecret"]
                ));

                // Upload ảnh lên Cloudinary
                if (image != null && image.ContentLength > 0)
                {
                    var uploadParams = new ImageUploadParams()
                    {
                        File = new FileDescription(image.FileName, image.InputStream),
                        Folder = "tour_images"
                    };
                    var uploadResult = cloudinary.Upload(uploadParams);
                    if (uploadResult.Error != null)
                    {
                        ModelState.AddModelError("", "Lỗi upload ảnh: " + uploadResult.Error.Message);
                        return View(tour);
                    }

                    tour.imageUrls = uploadResult.SecureUrl.ToString();

                }

                // Upload video lên Cloudinary
                if (video != null)
                {
                    var uploadParams = new VideoUploadParams()
                    {
                        File = new FileDescription(video.FileName, video.InputStream),
                        Folder = "tour_videos"
                    };
                    var uploadResult = cloudinary.Upload(uploadParams);
                    tour.videoUrls = uploadResult.SecureUrl.ToString();
                }

                // Tính toán giá tour
                tour.childPrice = tour.price * 0.8m;
                tour.specialAdultPrice = tour.price * 1.5m;
                tour.specialChildPrice = tour.specialAdultPrice * 0.8m;

                db.Tours.Add(tour);
                db.SaveChanges();

                // Lưu tình trạng ghế (TourAvailability)
                if (availabilities != null && availabilities.Count > 0)
                {
                    foreach (var availability in availabilities)
                    {
                        availability.tourId = tour.id;
                        db.TourAvailabilities.Add(availability);
                    }
                }

                db.SaveChanges();
                return RedirectToAction("Index");
            }

            ViewBag.destinationId = new SelectList(db.Destinations, "id", "name", tour.destinationId);
            ViewBag.tourTypeId = new SelectList(db.TourTypes, "id", "name", tour.tourTypeId);
            return View(tour);
        }

        // GET: Admin/Tours/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Tour tour = db.Tours.Find(id);
            if (tour == null)
            {
                return HttpNotFound();
            }
            ViewBag.destinationId = new SelectList(db.Destinations, "id", "name", tour.destinationId);
            ViewBag.tourTypeId = new SelectList(db.TourTypes, "id", "name", tour.tourTypeId);
            return View(tour);
        }

        // POST: Admin/Tours/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(Tour tour, List<TourAvailability> availabilities)
        {
            if (ModelState.IsValid)
            {
                tour.childPrice = tour.price * 0.8m;
                tour.specialAdultPrice = tour.price * 1.5m;
                tour.specialChildPrice = tour.specialAdultPrice * 0.8m;
                db.Entry(tour).State = EntityState.Modified;
                db.SaveChanges();

                // Cập nhật tình trạng chỗ
                db.TourAvailabilities.RemoveRange(db.TourAvailabilities.Where(a => a.tourId == tour.id));
                if (availabilities != null)
                {
                    foreach (var availability in availabilities)
                    {
                        availability.tourId = tour.id;
                        db.TourAvailabilities.Add(availability);
                    }
                }

                db.SaveChanges();
                return RedirectToAction("Index");
            }
            ViewBag.destinationId = new SelectList(db.Destinations, "id", "name", tour.destinationId);
            ViewBag.tourTypeId = new SelectList(db.TourTypes, "id", "name", tour.tourTypeId);
            return View(tour);
        }
        // bật tắt 
        [HttpPost]

        public JsonResult ToggleStatus(int id)
        {
            var tour = db.Tours.Find(id);
            if (tour == null)
            {
                return Json(new { success = false, message = "Tour không tồn tại!" });
            }

            try
            {
                tour.isDisabled = !tour.isDisabled;
                db.SaveChanges();
                Console.WriteLine("Tour ID: " + id + ", Trạng thái mới: " + tour.isDisabled);

                return Json(new { success = true, tour.isDisabled });
            }
            catch (Exception ex)
            {
                // Log lỗi
                Console.WriteLine("Lỗi: " + ex.Message);
                return Json(new { success = false, message = "Lỗi khi lưu thay đổi: " + ex.Message });
            }


        }
        public ActionResult Details(int? id)
        {
            Tour tours = db.Tours.Find(id);
            Tour tour = db.Tours.Include(t => t.TourAvailabilities)
                     .FirstOrDefault(t => t.id == id);
            if (id == null)
            {
                System.Diagnostics.Debug.WriteLine("ID is null");
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest, "Invalid Tour ID");
            }

            System.Diagnostics.Debug.WriteLine("Tour ID: " + id);
            if (tours == null)
            {
                System.Diagnostics.Debug.WriteLine("Tour not found in database");
                return HttpNotFound();
            }

            return View(tours);
        }


        // GET: Admin/Tours/Delete/5
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest, "Invalid Tour ID");
            }

            Tour tour = db.Tours.Find(id);
            if (tour == null)
            {
                return HttpNotFound("Tour not found");
            }

            return View(tour);
        }

        // Xử lý khi nhấn "Gửi yêu cầu xóa"
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RequestDelete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest, "Invalid Tour ID");
            }

            var tour = db.Tours.Find(id);
            if (tour == null)
            {
                return HttpNotFound();
            }

            // Đánh dấu tour là đã yêu cầu xóa
            tour.deletionRequested = true;
            db.SaveChanges();

            TempData["SuccessMessage"] = "Yêu cầu xóa đã được gửi thành công!";
            return RedirectToAction("Index");
        }


        public ActionResult PendingTours(int? page)
        {
            int pageSize = 1;
            int pageNumber = (page ?? 1);

            var pendingTours = db.Tours
                .Where(t => t.isApproved == false)
                .OrderBy(t => t.id)
                .ToPagedList(pageNumber, pageSize);

            return View(pendingTours);
        }

        // Xác nhận tour
        [HttpPost]
        public ActionResult ApproveTour(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest, "Thiếu ID tour");
            }

            var tour = db.Tours.Find(id);
            if (tour == null)
            {
                return HttpNotFound();
            }

            tour.isApproved = true;
            db.SaveChanges();

            return RedirectToAction("PendingTours");
        }


        public ActionResult DeleteRequests()
        {
            var tours = db.Tours.Where(t => t.deletionRequested == true).ToList();
            Console.WriteLine("Số lượng tour chờ xóa: " + tours.Count);
            return View(tours);
        }

        [HttpPost]
        public ActionResult ConfirmDelete(int id, bool confirm)
        {
            var tour = db.Tours.Find(id);
            if (tour == null)
            {
                return HttpNotFound();
            }

            if (confirm)
            {
                db.Tours.Remove(tour); // Xóa khỏi DB nếu admin đồng ý
            }
            else
            {
                tour.deletionRequested = false; // Hủy yêu cầu xóa
            }

            db.SaveChanges();
            return RedirectToAction("DeleteRequests"); // Quay lại danh sách tour chờ xóa
        }
    }

}
    