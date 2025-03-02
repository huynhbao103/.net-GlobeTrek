using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using GlobeTrek.Filters;
using GlobeTrek.Models;

namespace GlobeTrek.Areas.Admin.Controllers
{
    [AdminAuthorize]
    public class TourTypesController : Controller
    {
        private TravelDBEntities db = new TravelDBEntities();

        // GET: Admin/TourTypes
        public ActionResult Index()
        {
            return View(db.TourTypes.ToList());
        }

        // GET: Admin/TourTypes/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest, "Invalid TourType ID");
            }
            TourType tourType = db.TourTypes.Find(id);
            if (tourType == null)
            {
                return HttpNotFound("TourType not found");
            }
            return View(tourType);
        }

        // GET: Admin/TourTypes/Create
        public ActionResult Create()
        {
            return View();
        }

        // POST: Admin/TourTypes/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "id,name")] TourType tourType)
        {
            if (string.IsNullOrWhiteSpace(tourType.name))
            {
                ModelState.AddModelError("name", "Tên loại tour không được để trống.");
            }

            // Kiểm tra trùng tên loại tour
            if (db.TourTypes.Any(t => t.name == tourType.name))
            {
                ModelState.AddModelError("name", "Tên loại tour đã tồn tại.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    db.TourTypes.Add(tourType);
                    db.SaveChanges();
                    TempData["SuccessMessage"] = "Loại tour đã được tạo thành công!";
                    return RedirectToAction("Index");
                }
                catch (Exception)
                {
                    TempData["ErrorMessage"] = "Đã có lỗi xảy ra! Vui lòng thử lại.";
                }
            }

            return View(tourType);
        }
        // GET: Admin/TourTypes/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest, "ID không hợp lệ");
            }
            TourType tourType = db.TourTypes.Find(id);
            if (tourType == null)
            {
                return HttpNotFound("TourType not found");
            }
            return View(tourType);
        }

        // POST: Admin/TourTypes/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "id,name")] TourType tourType)
        {
            if (ModelState.IsValid)
            {
                if (db.TourTypes.Any(t => t.id != tourType.id && t.name == tourType.name))
                {
                    ModelState.AddModelError("name", "TourType name already exists.");
                    return View(tourType);
                }
                db.Entry(tourType).State = EntityState.Modified;
                db.SaveChanges();
                TempData["SuccessMessage"] = "TourType updated successfully.";
                return RedirectToAction("Index");
            }
            return View(tourType);
        }

        // GET: Admin/TourTypes/Delete/5
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest, "Invalid TourType ID");
            }
            TourType tourType = db.TourTypes.Find(id);
            if (tourType == null)
            {
                return HttpNotFound("TourType not found");
            }
            return View(tourType);
        }

        // POST: Admin/TourTypes/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            TourType tourType = db.TourTypes.Find(id);
            if (tourType == null)
            {
                return HttpNotFound("TourType not found");
            }
            db.TourTypes.Remove(tourType);
            db.SaveChanges();
            TempData["SuccessMessage"] = "TourType deleted successfully.";
            return RedirectToAction("Index");
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
