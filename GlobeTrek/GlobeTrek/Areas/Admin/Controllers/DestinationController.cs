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
    public class DestinationController : Controller
    {
        private TravelDBEntities db = new TravelDBEntities();

        // GET: Admin/Destination
        public ActionResult Index()
        {
            return View(db.Destinations.ToList());
        }

        // GET: Admin/Destination/Create
        public ActionResult Create()
        {
            return View();
        }

        // POST: Admin/Destination/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "name")] Destination destination)
        {
            if (string.IsNullOrWhiteSpace(destination.name))
            {
                ModelState.AddModelError("name", "Tên điểm đến không được để trống.");
            }

            // Kiểm tra trùng tên điểm đến
            if (db.Destinations.Any(d => d.name == destination.name))
            {
                ModelState.AddModelError("name", "Tên điểm đến đã tồn tại.");
            }

            if (ModelState.IsValid)
            {
                db.Destinations.Add(destination);
                db.SaveChanges();
                TempData["SuccessMessage"] = "Thêm điểm đến thành công!";
                return RedirectToAction("Index");
            }

            return View(destination);
        }

        // GET: Admin/Destination/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                TempData["ErrorMessage"] = "ID không hợp lệ.";
                return RedirectToAction("Index");
            }
            Destination destination = db.Destinations.Find(id);
            if (destination == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy điểm đến.";
                return RedirectToAction("Index");
            }
            return View(destination);
        }

        // POST: Admin/Destination/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "id,name")] Destination destination)
        {
            if (string.IsNullOrWhiteSpace(destination.name))
            {
                ModelState.AddModelError("name", "Tên điểm đến không được để trống.");
            }

            // Kiểm tra trùng tên (không tính chính nó)
            if (db.Destinations.Any(d => d.name == destination.name && d.id != destination.id))
            {
                ModelState.AddModelError("name", "Tên điểm đến đã tồn tại.");
            }

            if (ModelState.IsValid)
            {
                db.Entry(destination).State = EntityState.Modified;
                db.SaveChanges();
                TempData["SuccessMessage"] = "Cập nhật điểm đến thành công!";
                return RedirectToAction("Index");
            }
            return View(destination);
        }

        // GET: Admin/Destination/Delete/5
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                TempData["ErrorMessage"] = "ID không hợp lệ.";
                return RedirectToAction("Index");
            }
            Destination destination = db.Destinations.Find(id);
            if (destination == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy điểm đến.";
                return RedirectToAction("Index");
            }
            return View(destination);
        }

        // POST: Admin/Destination/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            Destination destination = db.Destinations.Find(id);
            if (destination == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy điểm đến.";
                return RedirectToAction("Index");
            }

            db.Destinations.Remove(destination);
            db.SaveChanges();
            TempData["SuccessMessage"] = "Xóa điểm đến thành công!";
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
