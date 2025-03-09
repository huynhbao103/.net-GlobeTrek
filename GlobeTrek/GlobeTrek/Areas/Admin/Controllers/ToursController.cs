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
using System.Text.RegularExpressions;
using System.Globalization;
using System.Text;
using System.Data.Entity.Infrastructure;
using System.Data.SqlClient;

namespace GlobeTrek.Areas.Admin.Controllers
{
    public class ToursController : Controller
    {
        private TravelDBEntities db = new TravelDBEntities();

        // Hàm tạo slug từ title
        private string GenerateSlug(string title)
        {
            if (string.IsNullOrEmpty(title))
                return string.Empty;

            // Chuẩn hóa Unicode để tách dấu ra khỏi chữ cái
            string normalizedString = title.Normalize(NormalizationForm.FormD);
            StringBuilder slugBuilder = new StringBuilder();

            // Loại bỏ dấu thanh nhưng giữ lại chữ cái
            foreach (char c in normalizedString)
            {
                UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(c);
                if (category != UnicodeCategory.NonSpacingMark) // Giữ chữ cái, bỏ dấu
                {
                    slugBuilder.Append(c);
                }
            }

            string slug = slugBuilder.ToString().ToLowerInvariant()
                .Replace(" ", "-") // Thay khoảng trắng bằng dấu gạch ngang
                .Replace("đ", "d") // Thay 'đ' bằng 'd'
                .Replace("Đ", "d");

            // Loại bỏ các ký tự không phải chữ cái, số hoặc dấu gạch ngang
            slug = Regex.Replace(slug, @"[^a-z0-9-]", "");

            // Chuẩn hóa các dấu gạch ngang liên tiếp và loại bỏ ở đầu/cuối
            slug = Regex.Replace(slug, @"-+", "-").Trim('-');

            return slug;
        }

        // Hàm đảm bảo slug là duy nhất
        private string EnsureUniqueSlug(string baseSlug, int? excludeTourId = null)
        {
            string slug = baseSlug;
            int counter = 1;

            while (db.Tours.Any(t => t.slug == slug && t.id != excludeTourId))
            {
                slug = $"{baseSlug}-{counter}";
                counter++;
            }

            return slug;
        }

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
            var specialDates = new Dictionary<(DateTime Start, DateTime End), (decimal Multiplier, string Description)>
            {
                { (new DateTime(2025, 1, 28), new DateTime(2025, 2, 3)), (1.3m, "Tết Nguyên Đán") },
                { (new DateTime(2025, 12, 24), new DateTime(2025, 12, 25)), (1.2m, "Giáng Sinh") },
                { (new DateTime(2025, 4, 30), new DateTime(2025, 5, 1)), (1.25m, "Ngày Giải Phóng và Quốc Tế Lao Động") }
            };

            foreach (var specialDate in specialDates)
            {
                if (date >= specialDate.Key.Start && date <= specialDate.Key.End)
                {
                    return specialDate.Value.Multiplier;
                }
            }

            return 1.0m;
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

                // Tạo slug từ title
                string baseSlug = GenerateSlug(tour.title);
                tour.slug = EnsureUniqueSlug(baseSlug);

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
        // POST: Admin/Tours/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(Tour tour, List<TourAvailability> availabilities, HttpPostedFileBase image, HttpPostedFileBase video)
        {
            if (ModelState.IsValid)
            {
                // Tìm tour hiện tại trong cơ sở dữ liệu
                var existingTour = db.Tours.AsNoTracking().FirstOrDefault(t => t.id == tour.id);
                if (existingTour == null)
                {
                    return HttpNotFound();
                }

                // Tạo slug từ title nếu title thay đổi
                string baseSlug = GenerateSlug(tour.title);
                tour.slug = EnsureUniqueSlug(baseSlug, tour.id);

                // Giữ trạng thái isApproved từ existingTour nếu không có thay đổi rõ ràng
                tour.isApproved = existingTour.isApproved; // Giữ nguyên trạng thái duyệt trừ khi được thay đổi thủ công
                tour.isDeleted = existingTour.isDeleted;   // Giữ nguyên các trạng thái khác
                tour.isDisabled = existingTour.isDisabled;
                tour.deletionRequested = existingTour.deletionRequested;

                // Cập nhật ảnh nếu có file mới được upload
                if (image != null && image.ContentLength > 0)
                {
                    string imageUrl = UploadImageToCloudinary(image);
                    if (!string.IsNullOrEmpty(imageUrl))
                    {
                        tour.imageUrls = imageUrl;
                    }
                    else
                    {
                        ModelState.AddModelError("", "Không thể upload ảnh.");
                        return View(tour);
                    }
                }
                else
                {
                    // Giữ nguyên ảnh cũ nếu không upload ảnh mới
                    tour.imageUrls = existingTour.imageUrls;
                }

                // Cập nhật video nếu có file mới được upload
                if (video != null && video.ContentLength > 0)
                {
                    var cloudinary = new Cloudinary(new Account(
                        ConfigurationManager.AppSettings["CloudinaryCloudName"],
                        ConfigurationManager.AppSettings["CloudinaryApiKey"],
                        ConfigurationManager.AppSettings["CloudinaryApiSecret"]
                    ));

                    var uploadParams = new VideoUploadParams()
                    {
                        File = new FileDescription(video.FileName, video.InputStream),
                        Folder = "tour_videos"
                    };
                    var uploadResult = cloudinary.Upload(uploadParams);
                    if (uploadResult.Error != null)
                    {
                        ModelState.AddModelError("", "Lỗi upload video: " + uploadResult.Error.Message);
                        return View(tour);
                    }
                    tour.videoUrls = uploadResult.SecureUrl.ToString();
                }
                else
                {
                    // Giữ nguyên video cũ nếu không upload video mới
                    tour.videoUrls = existingTour.videoUrls;
                }

                // Tính toán lại giá
                tour.childPrice = tour.price * 0.8m;
                tour.specialAdultPrice = tour.price * 1.5m;
                tour.specialChildPrice = tour.specialAdultPrice * 0.8m;

                // Cập nhật tour vào cơ sở dữ liệu
                db.Entry(tour).State = EntityState.Modified;

                // Cập nhật TourAvailabilities
                db.TourAvailabilities.RemoveRange(db.TourAvailabilities.Where(a => a.tourId == tour.id));
                if (availabilities != null && availabilities.Count > 0)
                {
                    foreach (var availability in availabilities)
                    {
                        availability.tourId = tour.id;
                        db.TourAvailabilities.Add(availability);
                    }
                }

                try
                {
                    db.SaveChanges();
                    TempData["SuccessMessage"] = "Cập nhật tour thành công!";
                    return RedirectToAction("Index");
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Lỗi khi lưu dữ liệu: " + ex.Message);
                }
            }

            // Nếu có lỗi, trả lại view với dữ liệu hiện tại
            ViewBag.destinationId = new SelectList(db.Destinations, "id", "name", tour.destinationId);
            ViewBag.tourTypeId = new SelectList(db.TourTypes, "id", "name", tour.tourTypeId);
            return View(tour);
        }
        // Toggle status
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
                Console.WriteLine("Lỗi: " + ex.Message);
                return Json(new { success = false, message = "Lỗi khi lưu thay đổi: " + ex.Message });
            }
        }

        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                System.Diagnostics.Debug.WriteLine("ID is null");
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest, "Invalid Tour ID");
            }

            Tour tour = db.Tours.Include(t => t.TourAvailabilities)
                                .FirstOrDefault(t => t.id == id);
            if (tour == null)
            {
                System.Diagnostics.Debug.WriteLine("Tour not found in database");
                return HttpNotFound();
            }

            return View(tour);
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

            ViewBag.OrderCount = db.Orders.Count(o => o.tourId == id);
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
        [ValidateAntiForgeryToken]
        public ActionResult ConfirmDelete(int id, bool confirm)
        {
            var tour = db.Tours.Find(id);
            if (tour == null)
            {
                System.Diagnostics.Debug.WriteLine($"Tour with ID {id} not found");
                return HttpNotFound();
            }

            System.Diagnostics.Debug.WriteLine($"ConfirmDelete called with id: {id}, confirm: {confirm}");

            if (confirm)
            {
                var orderCount = db.Orders.Count(o => o.tourId == id);
                System.Diagnostics.Debug.WriteLine($"Tour {id} has {orderCount} dependent orders");

                if (orderCount > 0)
                {
                    TempData["ErrorMessage"] = $"Không thể xóa tour vì có {orderCount} đơn hàng liên quan.";
                    System.Diagnostics.Debug.WriteLine($"Deletion blocked due to {orderCount} orders");
                    return RedirectToAction("DeleteRequests");
                }

                try
                {
                    System.Diagnostics.Debug.WriteLine($"Attempting to delete tour {id}");
                    db.Tours.Remove(tour);
                    db.SaveChanges();
                    System.Diagnostics.Debug.WriteLine($"Tour {id} deleted successfully");
                    TempData["SuccessMessage"] = "Tour đã được xóa thành công!";
                }
                catch (DbUpdateException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"DB Update Exception: {ex.Message}");
                    TempData["ErrorMessage"] = $"Lỗi khi xóa tour: {ex.Message}";
                    return RedirectToAction("DeleteRequests");
                }
            }
            else
            {
                try
                {
                    tour.deletionRequested = false;
                    db.SaveChanges();
                    System.Diagnostics.Debug.WriteLine($"Deletion request cancelled for tour {id}");
                    TempData["SuccessMessage"] = "Yêu cầu xóa đã được hủy!";
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error cancelling deletion request for tour {id}: {ex.Message}");
                    TempData["ErrorMessage"] = $"Đã xảy ra lỗi khi hủy yêu cầu xóa: {ex.Message}";
                }
            }

            return RedirectToAction("DeleteRequests");
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