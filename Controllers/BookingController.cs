using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Mvc;
using DatBanQuanAn.Helpers;
using DatBanQuanAn.Models;
using System.Linq;

namespace DatBanQuanAn.Controllers
{
    public class AreaInfo
    {
        public int Id { get; set; }
        public string Ten { get; set; }
        public int SoTrong { get; set; }
    }

    public class DonDatBanView
    {
        public int Id { get; set; }
        public string MaDon { get; set; }
        public int? KhachId { get; set; }
        public string SDT { get; set; }
        public string HoTen { get; set; }
        public int BanId { get; set; }
        public DateTime NgayGio { get; set; }
        public int SoNguoi { get; set; }
        public string GhiChu { get; set; }
        public string HinhThuc { get; set; }
        public decimal TienCoc { get; set; }
        public decimal TienMon { get; set; }
        public string TrangThai { get; set; }
        public DateTime? KhoaDen { get; set; }
        public DateTime NgayTao { get; set; }
        public string Ban { get; set; }
        public int ConLai { get; set; }
    }

    public class BookingController : Controller
    {
        static readonly Regex PhoneRx = new Regex(@"^0\d{9,10}$");

        DatBanQuanAnEntities db = new DatBanQuanAnEntities();

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }

        static bool TryDt(string ngay, string gio, out DateTime dt)
        {
            return DateTime.TryParseExact((ngay ?? "") + " " + (gio ?? ""), "yyyy-MM-dd HH:mm",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out dt);
        }

        IQueryable<DonDatBanView> DonViews()
        {
            return from d in db.DonDatBans
                   join b in db.Bans on d.BanId equals b.Id
                   select new DonDatBanView
                   {
                       Id = d.Id,
                       MaDon = d.MaDon,
                       KhachId = d.KhachId,
                       SDT = d.SDT,
                       HoTen = d.HoTen,
                       BanId = d.BanId,
                       NgayGio = d.NgayGio,
                       SoNguoi = d.SoNguoi,
                       GhiChu = d.GhiChu,
                       HinhThuc = d.HinhThuc,
                       TienCoc = d.TienCoc,
                       TienMon = d.TienMon,
                       TrangThai = d.TrangThai,
                       KhoaDen = d.KhoaDen,
                       NgayTao = d.NgayTao,
                       Ban = b.Ten
                   };
        }

        public ActionResult Index(string ngay, string gio, int? soNguoi, int? khuVucId, int? loaiBanId)
        {
            if (string.IsNullOrEmpty(ngay)) ngay = DateTime.Now.ToString("yyyy-MM-dd");
            if (string.IsNullOrEmpty(gio)) gio = DateTime.Now.AddHours(1).ToString("HH") + ":00";
            int n = soNguoi ?? 2;
            ViewBag.Ngay = ngay; ViewBag.Gio = gio; ViewBag.SoNguoi = n; ViewBag.KhuVucId = khuVucId; ViewBag.LoaiBanId = loaiBanId;
            ViewBag.Types = db.LoaiBans.OrderBy(x => x.Id).ToList();
            DateTime dt;
            if (!TryDt(ngay, gio, out dt)) { ViewBag.Err = "Ngày giờ không hợp lệ."; return View(); }
            if (dt < DateTime.Now) { ViewBag.Err = "Vui lòng chọn thời điểm trong tương lai."; return View(); }
            if (n < 1 || n > 50) { ViewBag.Err = "Số người từ 1 đến 50."; return View(); }

            string loaiTen = null;
            if (loaiBanId.HasValue)
            {
                var lb = db.LoaiBans.Find(loaiBanId.Value);
                if (lb != null) loaiTen = lb.Ten;
            }

            var areas = new List<AreaInfo>();
            foreach (var a in db.KhuVucs.OrderBy(x => x.Id).ToList())
            {
                var t = BookingService.Tables(db, dt, n, a.Id);
                if (loaiTen != null) t = t.Where(x => x.Loai == loaiTen).ToList();
                areas.Add(new AreaInfo { Id = a.Id, Ten = a.Ten, SoTrong = t.Count(x => x.Trong) });
                if (khuVucId.HasValue && a.Id == khuVucId.Value) ViewBag.Tables = t;
            }
            ViewBag.Areas = areas;
            return View();
        }

        public ActionResult Create(int banId, string ngay, string gio, int soNguoi)
        {
            DateTime dt;
            if (!TryDt(ngay, gio, out dt) || dt < DateTime.Now) { TempData["Err"] = "Thời gian không hợp lệ."; return RedirectToAction("Index"); }
            var ban = db.Bans.FirstOrDefault(x => x.Id == banId && !x.Xoa);
            if (ban == null || ban.SucChua < soNguoi)
            { TempData["Err"] = "Bàn không hợp lệ."; return RedirectToAction("Index", new { ngay, gio, soNguoi }); }

            var free = BookingService.Tables(db, dt, soNguoi, ban.KhuVucId).Any(x => x.Id == banId && x.Trong);
            if (!free) { TempData["Err"] = "Bàn này đã có người đặt. Vui lòng chọn bàn khác."; return RedirectToAction("Index", new { ngay, gio, soNguoi, khuVucId = ban.KhuVucId }); }

            ViewBag.Ban = ban;
            ViewBag.KhuVuc = db.KhuVucs.Find(ban.KhuVucId).Ten;
            ViewBag.Loai = db.LoaiBans.Find(ban.LoaiBanId).Ten;
            ViewBag.Dt = dt; ViewBag.SoNguoi = soNguoi; ViewBag.Ngay = ngay; ViewBag.Gio = gio;
            int loaiId = ban.LoaiBanId;
            ViewBag.Coc = db.MucCocs.Where(x => x.LoaiBanId == loaiId).OrderBy(x => x.SoTien).ToList();
            if (Cur.Role == "KhachThanhVien")
            {
                ViewBag.User = db.TaiKhoans.Find(Cur.UserId.Value);
                ViewBag.Menu = db.MonAns.Where(x => x.ConMon && !x.An).OrderBy(x => x.DanhMuc).ThenBy(x => x.Ten).ToList();
            }
            return View();
        }

        [HttpPost]
        public ActionResult Create(int banId, string ngay, string gio, int soNguoi, string hoTen, string sdt,
            string ghiChu, string hinhThuc, int? mucCocId)
        {
            DateTime dt;
            if (!TryDt(ngay, gio, out dt) || dt < DateTime.Now) { TempData["Err"] = "Thời gian không hợp lệ."; return RedirectToAction("Index"); }
            Func<string, ActionResult> back = msg => { TempData["Err"] = msg; return RedirectToAction("Create", new { banId, ngay, gio, soNguoi }); };

            hoTen = (hoTen ?? "").Trim(); sdt = (sdt ?? "").Trim();
            if (hoTen == "" || !PhoneRx.IsMatch(sdt)) return back("Vui lòng nhập Họ tên và SĐT (Zalo) hợp lệ.");
            if (ghiChu != null && ghiChu.Length > 300) return back("Ghi chú tối đa 300 ký tự.");

            var ban = db.Bans.FirstOrDefault(x => x.Id == banId && !x.Xoa);
            if (ban == null) return back("Bàn không hợp lệ.");

            decimal coc = 0; var mon = new List<int[]>();
            if (hinhThuc == "KemMon")
            {
                if (Cur.Role != "KhachThanhVien") return back("Đặt kèm món yêu cầu đăng nhập.");
                foreach (string k in Request.Form.AllKeys)
                {
                    if (k == null || !k.StartsWith("qty_")) continue;
                    int mid, q;
                    if (int.TryParse(k.Substring(4), out mid) && int.TryParse(Request.Form[k], out q) && q > 0)
                        mon.Add(new[] { mid, Math.Min(q, 50) });
                }
                if (mon.Count == 0) return back("Vui lòng chọn ít nhất một món.");
            }
            else
            {
                hinhThuc = "CocBan";
                if (!mucCocId.HasValue) return back("Vui lòng chọn mức đặt cọc.");
                int cocId = mucCocId.Value;
                int loaiId = ban.LoaiBanId;
                var tien = db.MucCocs.Where(x => x.Id == cocId && x.LoaiBanId == loaiId)
                                     .Select(x => (decimal?)x.SoTien).FirstOrDefault();
                if (tien == null) return back("Mức cọc không hợp lệ.");
                coc = tien.Value;
            }

            int id; string ma, err;
            if (!BookingService.TryCreate(db, banId, dt, soNguoi, Cur.UserId, sdt, hoTen, ghiChu, hinhThuc, coc, mon, "ChoThanhToan", out id, out ma, out err))
                return back(err);
            Session["PayingBooking"] = id;
            return RedirectToAction("Pay", new { id });
        }

        public ActionResult Pay(int id)
        {
            if (!(Session["PayingBooking"] is int) || (int)Session["PayingBooking"] != id) return RedirectToAction("Index");
            var d = DonViews().FirstOrDefault(x => x.Id == id);
            int conLai = 0;
            if (d != null && d.KhoaDen != null) conLai = (int)(d.KhoaDen.Value - DateTime.Now).TotalSeconds;
            if (d == null || d.TrangThai != "ChoThanhToan" || d.KhoaDen == null || conLai <= 0)
            { TempData["Err"] = "Phiên giữ bàn đã hết hạn. Vui lòng đặt lại."; return RedirectToAction("Index"); }
            d.ConLai = conLai;
            ViewBag.D = d;
            return View();
        }

        [HttpPost]
        public ActionResult PayResult(int id, string result)
        {
            if (!(Session["PayingBooking"] is int) || (int)Session["PayingBooking"] != id) return RedirectToAction("Index");
            var d = db.DonDatBans.Find(id);
            if (result == "ok")
            {
                if (d == null || d.TrangThai != "ChoThanhToan" || d.KhoaDen == null || d.KhoaDen <= DateTime.Now)
                { TempData["Err"] = "Phiên thanh toán đã hết hạn, bàn đã được mở khóa."; return RedirectToAction("Index"); }

                bool kemMon = d.HinhThuc == "KemMon";
                d.TrangThai = kemMon ? "DaThanhToan" : "DaCoc";
                d.KhoaDen = null;
                db.ThanhToans.Add(new ThanhToan
                {
                    DonDatBanId = id,
                    Loai = kemMon ? "Mon" : "Coc",
                    SoTien = kemMon ? d.TienMon : d.TienCoc,
                    MaGiaoDich = "MOCK" + Security.RandomCode(10),
                    PhuongThuc = "GiaLap",
                    TrangThai = "ThanhCong",
                    Ngay = DateTime.Now
                });
                db.SaveChanges();
                BookingService.Notify(db, d.SDT, "Dat ban thanh cong. Ma don " + d.MaDon + " luc " + Fmt.Date(d.NgayGio));
                Session.Remove("PayingBooking");
                return RedirectToAction("Result", new { ma = d.MaDon, sdt = d.SDT });
            }
            if (d != null && d.TrangThai == "ChoThanhToan")
            {
                d.TrangThai = "DaHuy"; d.KhoaDen = null;
                db.SaveChanges();
            }
            Session.Remove("PayingBooking");
            TempData["Err"] = "Thanh toán không thành công, bàn đã được mở khóa.";
            return RedirectToAction("Index");
        }

        public ActionResult Result(string ma, string sdt)
        {
            ViewBag.D = DonViews().FirstOrDefault(x => x.MaDon == ma && x.SDT == sdt);
            return View();
        }

        public ActionResult Lookup(string sdt, string ma)
        {
            ViewBag.Sdt = sdt; ViewBag.Ma = ma;
            if (Cur.Role == "KhachThanhVien")
            {
                int uid = Cur.UserId.Value;
                ViewBag.List = DonViews().Where(x => x.KhachId == uid).OrderByDescending(x => x.NgayGio).ToList();
            }
            else if (!string.IsNullOrWhiteSpace(sdt) && !string.IsNullOrWhiteSpace(ma))
            {
                string sdtQ = sdt.Trim(), maQ = ma.Trim().ToUpper();
                ViewBag.List = DonViews().Where(x => x.SDT == sdtQ && x.MaDon == maQ).ToList();
            }
            return View();
        }

        [HttpPost]
        public ActionResult Cancel(int id, string sdt, string ma)
        {
            int userId = Cur.UserId ?? -1;
            string sdtQ = sdt ?? "", maQ = ma ?? "";
            var d = db.DonDatBans.FirstOrDefault(x => x.Id == id
                        && (x.TrangThai == "DaCoc" || x.TrangThai == "DaThanhToan")
                        && (x.KhachId == userId || (x.SDT == sdtQ && x.MaDon == maQ)));
            if (d == null) { TempData["Err"] = "Không tìm thấy đơn hoặc đơn không thể hủy."; return RedirectToAction("Lookup", new { sdt, ma }); }

            double hours = (d.NgayGio - DateTime.Now).TotalHours;
            int pct = hours >= 24 ? 100 : (hours >= 2 ? 50 : 0);
            decimal paid = d.HinhThuc == "KemMon" ? d.TienMon : d.TienCoc;
            decimal refund = Math.Round(paid * pct / 100m);
            d.TrangThai = "DaHuy";
            if (refund > 0)
                db.ThanhToans.Add(new ThanhToan
                {
                    DonDatBanId = id,
                    Loai = "HoanCoc",
                    SoTien = refund,
                    PhuongThuc = "GiaLap",
                    TrangThai = "ThanhCong",
                    Ngay = DateTime.Now
                });
            db.SaveChanges();
            BookingService.Notify(db, d.SDT, "Don " + d.MaDon + " da huy. Hoan " + refund.ToString("N0") + "d");
            TempData["Ok"] = "Đã hủy đơn. Mức hoàn: " + pct + "% (" + Fmt.Money(refund) + ").";
            return RedirectToAction("Lookup", new { sdt, ma });
        }

        [HttpPost]
        public ActionResult ChangeTime(int id, string ngay, string gio, string sdt, string ma)
        {
            DateTime dt;
            int userId = Cur.UserId ?? -1;
            string sdtQ = sdt ?? "", maQ = ma ?? "";
            var d = db.DonDatBans.FirstOrDefault(x => x.Id == id
                        && (x.TrangThai == "DaCoc" || x.TrangThai == "DaThanhToan")
                        && (x.KhachId == userId || (x.SDT == sdtQ && x.MaDon == maQ)));
            if (d == null) TempData["Err"] = "Không tìm thấy đơn hoặc đơn không thể đổi.";
            else if (!TryDt(ngay, gio, out dt) || dt < DateTime.Now) TempData["Err"] = "Thời gian mới không hợp lệ.";
            else if ((d.NgayGio - DateTime.Now).TotalHours < 2) TempData["Err"] = "Chỉ được đổi giờ trước giờ hẹn ít nhất 2 giờ.";
            else
            {
                using (var tx = db.Database.BeginTransaction(IsolationLevel.Serializable))
                {
                    if (BookingService.IsConflict(db, d.BanId, dt, id)) { tx.Rollback(); TempData["Err"] = "Bàn đã có người đặt ở khung giờ mới."; }
                    else { d.NgayGio = dt; db.SaveChanges(); tx.Commit(); TempData["Ok"] = "Đã đổi giờ đặt bàn."; }
                }
            }
            return RedirectToAction("Lookup", new { sdt, ma });
        }
        DonDatBan FindOwn(int id, string sdt, string ma)
        {
            int userId = Cur.UserId ?? -1;
            string sdtQ = sdt ?? "", maQ = ma ?? "";
            return db.DonDatBans.FirstOrDefault(x => x.Id == id
                        && (x.TrangThai == "DaCoc" || x.TrangThai == "DaThanhToan")
                        && (x.KhachId == userId || (x.SDT == sdtQ && x.MaDon == maQ)));
        }

        public ActionResult ChangeTable(int id, string sdt, string ma)
        {
            var d = FindOwn(id, sdt, ma);
            if (d == null) { TempData["Err"] = "Không tìm thấy đơn hoặc đơn không thể đổi bàn."; return RedirectToAction("Lookup", new { sdt, ma }); }
            if ((d.NgayGio - DateTime.Now).TotalHours < 2)
            { TempData["Err"] = "Chỉ được đổi bàn trước giờ hẹn ít nhất 2 giờ."; return RedirectToAction("Lookup", new { sdt, ma }); }

            var cur = db.Bans.Find(d.BanId);
            var options = new List<TableOption>();
            foreach (var a in db.KhuVucs.OrderBy(x => x.Id).ToList())
            {
                foreach (var t in BookingService.Tables(db, d.NgayGio, d.SoNguoi, a.Id).Where(x => x.Trong && x.Id != d.BanId))
                {
                    var b = db.Bans.Find(t.Id);
                    if (b != null && !b.Xoa && b.LoaiBanId == cur.LoaiBanId)
                        options.Add(new TableOption { Id = b.Id, KhuVuc = a.Ten, Ten = b.Ten, SucChua = b.SucChua });
                }
            }
            ViewBag.D = DonViews().FirstOrDefault(x => x.Id == id);
            ViewBag.Options = options;
            ViewBag.Sdt = sdt; ViewBag.Ma = ma;
            return View();
        }

        [HttpPost]
        public ActionResult ChangeTable(int id, int newBanId, string sdt, string ma)
        {
            var d = FindOwn(id, sdt, ma);
            if (d == null) TempData["Err"] = "Không tìm thấy đơn hoặc đơn không thể đổi bàn.";
            else if ((d.NgayGio - DateTime.Now).TotalHours < 2) TempData["Err"] = "Chỉ được đổi bàn trước giờ hẹn ít nhất 2 giờ.";
            else
            {
                var cur = db.Bans.Find(d.BanId);
                var nb = db.Bans.FirstOrDefault(x => x.Id == newBanId && !x.Xoa);
                if (nb == null || nb.LoaiBanId != cur.LoaiBanId || nb.SucChua < d.SoNguoi || nb.TrangThai == "TamNgung")
                    TempData["Err"] = "Bàn mới không hợp lệ (phải cùng loại, đủ chỗ và đang hoạt động).";
                else
                {
                    using (var tx = db.Database.BeginTransaction(IsolationLevel.Serializable))
                    {
                        if (BookingService.IsConflict(db, newBanId, d.NgayGio, id))
                        { tx.Rollback(); TempData["Err"] = "Bàn mới vừa có người đặt, vui lòng chọn bàn khác."; }
                        else
                        {
                            d.BanId = newBanId; db.SaveChanges(); tx.Commit();
                            BookingService.Notify(db, d.SDT, "Don " + d.MaDon + " da doi sang ban " + nb.Ten + " luc " + Fmt.Date(d.NgayGio));
                            TempData["Ok"] = "Đã đổi sang bàn " + nb.Ten + ".";
                        }
                    }
                }
            }
            return RedirectToAction("Lookup", new { sdt, ma });
        }
    }
}