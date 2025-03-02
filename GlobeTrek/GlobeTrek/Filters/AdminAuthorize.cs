using System;
using System.Web;
using System.Web.Mvc;

namespace GlobeTrek.Filters
{
    public class AdminAuthorize : AuthorizeAttribute
    {
        protected override bool AuthorizeCore(HttpContextBase httpContext)
        {
            // Kiểm tra xem Session có AdminID hay không
            return httpContext.Session["AdminID"] != null;
        }

        protected override void HandleUnauthorizedRequest(AuthorizationContext filterContext)
        {
            var urlHelper = new UrlHelper(filterContext.RequestContext);
            string loginUrl = urlHelper.Action("Login", "Auth", new { area = "Admin" });

            // Tránh vòng lặp chuyển hướng khi đang ở trang Login
            if (filterContext.HttpContext.Request.Url.AbsolutePath != loginUrl)
            {
                filterContext.Result = new RedirectResult(loginUrl);
            }
        }
    }
}
