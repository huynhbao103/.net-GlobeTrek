using GlobeTrek.Models;
using System;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace GlobeTrek.Areas.Admin.Controllers
{
    public class PromotionController : Controller
    {
        private readonly TravelDBEntities db = new TravelDBEntities();

        // GET: Admin/Promotion
        public async Task<ActionResult> Index()
        {
            var promotions = await db.Promotions.Include(p => p.Tour).ToListAsync();
            return View(promotions);
        }

        // GET: Admin/Promotion/Create
        public ActionResult Create()
        {
            ViewBag.Tours = new SelectList(db.Tours, "id", "title");
            return View();
        }

        // POST: Admin/Promotion/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(Promotion promotion)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    promotion.IsActive = true; // Default value
                    db.Promotions.Add(promotion);
                    await db.SaveChangesAsync();
                    return RedirectToAction("Index");
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Lỗi khi tạo khuyến mãi: " + ex.Message);
                }
            }
            ViewBag.Tours = new SelectList(db.Tours, "id", "title");
            return View(promotion);
        }

        // GET: Admin/Promotion/Edit/5
        public async Task<ActionResult> Edit(int id)
        {
            var promotion = await db.Promotions.FindAsync(id);
            if (promotion == null)
            {
                return HttpNotFound();
            }
            ViewBag.Tours = new SelectList(db.Tours, "id", "title");
            return View(promotion);
        }

        // POST: Admin/Promotion/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(Promotion promotion)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    db.Entry(promotion).State = EntityState.Modified;
                    await db.SaveChangesAsync();
                    return RedirectToAction("Index");
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Lỗi khi cập nhật khuyến mãi: " + ex.Message);
                }
            }
            ViewBag.Tours = new SelectList(db.Tours, "id", "title");
            return View(promotion);
        }

        // GET: Admin/Promotion/Delete/5
        public async Task<ActionResult> Delete(int id)
        {
            var promotion = await db.Promotions.FindAsync(id);
            if (promotion == null)
            {
                return HttpNotFound();
            }
            return View(promotion);
        }

        // POST: Admin/Promotion/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var promotion = await db.Promotions.FindAsync(id);
                db.Promotions.Remove(promotion);
                await db.SaveChangesAsync();
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Lỗi khi xóa khuyến mãi: " + ex.Message);
                return RedirectToAction("Delete", new { id });
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