using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using DatBanQuanAn.Helpers;

namespace DatBanQuanAn
{
    public class MvcApplication : HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
            Seed.Run();               // tao tai khoan mau lan dau
        }
        protected void Application_BeginRequest()
        {
            Seed.Maintain();          // giai phong ban het han khoa tam
        }
    }
}
