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
            var orders = db.Orders.Include(o => o.Tour).Include(o => o.User);
            return View(orders.ToList());
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
