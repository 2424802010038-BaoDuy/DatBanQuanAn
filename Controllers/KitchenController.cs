using System;
using System.Linq;
using System.Web.Mvc;
using DatBanQuanAn.Helpers;
using DatBanQuanAn.Models;

namespace DatBanQuanAn.Controllers
{
    public class KitchenRow
    {
        public int Id { get; set; }
        public int SoLuong { get; set; }
        public string GhiChu { get; set; }
        public string TrangThai { get; set; }
        public string Ten { get; set; }
        public string NoiCheBien { get; set; }
        public string Ban { get; set; }
        public DateTime NgayTao { get; set; }
    }

    [Role("Bep", "QuanLy")]
    public class KitchenController : Controller
    {
        DatBanQuanAnEntities db = new DatBanQuanAnEntities();

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }

        public ActionResult Index(string loai)
        {
            loai = loai == "Bep" || loai == "Bar" ? loai : "";
            ViewBag.Loai = loai;
            string l = loai;
            ViewBag.Queue = (from c in db.ChiTietGoiMons
                             join g in db.DonGoiMons on c.DonGoiMonId equals g.Id
                             join m in db.MonAns on c.MonId equals m.Id
                             join b in db.Bans on g.BanId equals b.Id
                             where g.TrangThai == "DaXacNhan"
                                   && (c.TrangThai == "ChoCheBien" || c.TrangThai == "DangCheBien")
                                   && (l == "" || m.NoiCheBien == l)
                             orderby g.NgayTao, c.Id
                             select new KitchenRow
                             {
                                 Id = c.Id,
                                 SoLuong = c.SoLuong,
                                 GhiChu = c.GhiChu,
                                 TrangThai = c.TrangThai,
                                 Ten = m.Ten,
                                 NoiCheBien = m.NoiCheBien,
                                 Ban = b.Ten,
                                 NgayTao = g.NgayTao
                             }).ToList();
            ViewBag.Menu = db.MonAns.Where(x => !x.An).OrderBy(x => x.DanhMuc).ThenBy(x => x.Ten).ToList();
            return View();
        }

        [HttpPost]
        public ActionResult SetStatus(int id, string st, string loai)
        {
            if (st == "DangCheBien" || st == "DaXong")
            {
                var item = db.ChiTietGoiMons.FirstOrDefault(x => x.Id == id && (x.TrangThai == "ChoCheBien" || x.TrangThai == "DangCheBien"));
                if (item != null) { item.TrangThai = st; db.SaveChanges(); }
            }
            return RedirectToAction("Index", new { loai });
        }

        [HttpPost]
        public ActionResult SoldOut(int monId, string loai)
        {
            var mon = db.MonAns.Find(monId);
            if (mon != null) { mon.ConMon = !mon.ConMon; db.SaveChanges(); }
            return RedirectToAction("Index", new { loai });
        }
        [HttpPost]
        public ActionResult CancelItem(int id, string loai)
        {
            var item = db.ChiTietGoiMons.FirstOrDefault(x => x.Id == id && x.TrangThai == "ChoCheBien");
            if (item == null)
                TempData["Err"] = "Món đã bắt đầu chế biến hoặc không còn, không thể hủy.";
            else
            {
                item.TrangThai = "Huy";
                var g = db.DonGoiMons.Find(item.DonGoiMonId);
                if (g != null && g.DaThanhToan)
                {
                    db.ThanhToans.Add(new ThanhToan
                    {
                        DonGoiMonId = g.Id,
                        Loai = "HoanCoc",
                        SoTien = item.SoLuong * item.DonGia,
                        PhuongThuc = "GiaLap",
                        TrangThai = "ThanhCong",
                        Ngay = DateTime.Now
                    });
                }
                db.SaveChanges();
                TempData["Ok"] = "Đã hủy món.";
            }
            return RedirectToAction("Index", new { loai });
        }
    }
}