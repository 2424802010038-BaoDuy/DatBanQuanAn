using System.Web.Mvc;
using DatBanQuanAn.Helpers;

namespace DatBanQuanAn
{
    public class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            filters.Add(new HandleErrorAttribute());
            filters.Add(new CsrfFilter());   // chong CSRF cho moi POST
        }
    }
}
