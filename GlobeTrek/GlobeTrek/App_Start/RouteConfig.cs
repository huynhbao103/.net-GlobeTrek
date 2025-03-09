using System.Web.Mvc;
using System.Web.Routing;

public class RouteConfig
{
    public static void RegisterRoutes(RouteCollection routes)
    {
        routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

        routes.MapRoute(
            name: "Default",
            url: "{controller}/{action}/{id}",
            defaults: new { controller = "getAllTour", action = "Index", id = UrlParameter.Optional },
            namespaces: new[] { "GlobeTrek.Controllers" } // Specify the primary namespace
        );
    }
}