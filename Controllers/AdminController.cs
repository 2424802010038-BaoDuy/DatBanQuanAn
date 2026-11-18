using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Mvc;
using DatBanQuanAn.Helpers;
using DatBanQuanAn.Models;

namespace DatBanQuanAn.Controllers
{
    public class AdminTableRow
    {
        public int Id { get; set; }
        public string Ten { get; set; }
        public int SucChua { get; set; }
        public int KhuVucId { get; set; }
        public int LoaiBanId { get; set; }
        public string TrangThai { get; set; }
        public string KhuVuc { get; set; }
        public string Loai { get; set; }
    }

    public class CatalogRow
    {
        public int Id { get; set; }
        public string Ten { get; set; }
        public int SoBan { get; set; }
    }

    public class CocRow
    {
        public int Id { get; set; }
        public decimal SoTien { get; set; }
        public string Loai { get; set; }
    }

    public class RevenueRow
    {
        public DateTime Ngay { get; set; }
        public int SoHD { get; set; }
        public decimal DoanhThu { get; set; }
    }

    public class CocSummary { public decimal Thu { get; set; } public decimal Hoan { get; set; } public decimal Mat { get; set; } }
    public class NoShowSummary { public int Tong { get; set; } public int NoShow { get; set; } }
    public class NameCount { public string Ten { get; set; } public int SoLuong { get; set; } }
    public class PeakRow { public int Gio { get; set; } public int SoDon { get; set; } }
    public class FillRow { public string Ten { get; set; } public int SoDon { get; set; } }

    [Role("QuanLy")]
    public class AdminController : Controller
    {
        DatBanQuanAnEntities db = new DatBanQuanAnEntities();

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }

        public ActionResult Index() { return View(); }

        public ActionResult Tables(int? edit)
        {
            ViewBag.List = (from b in db.Bans
                            join kv in db.KhuVucs on b.KhuVucId equals kv.Id
                            join lb in db.LoaiBans on b.LoaiBanId equals lb.Id
                            where !b.Xoa
                            orderby kv.Id, b.Ten
                            select new AdminTableRow
                            {
                                Id = b.Id,
                                Ten = b.Ten,
                                SucChua = b.SucChua,
                                KhuVucId = b.KhuVucId,
                                LoaiBanId = b.LoaiBanId,
                                TrangThai = b.TrangThai,
                                KhuVuc = kv.Ten,
                                Loai = lb.Ten
                            }).ToList();
            ViewBag.Areas = db.KhuVucs.OrderBy(x => x.Id).ToList();
            ViewBag.Types = db.LoaiBans.OrderBy(x => x.Id).ToList();
            if (edit.HasValue)
            {
                int eid = edit.Value;
                ViewBag.Edit = db.Bans.FirstOrDefault(x => x.Id == eid && !x.Xoa);
            }
            return View();
        }

        [HttpPost]
        public ActionResult SaveTable(int? id, string ten, int sucChua, int khuVucId, int loaiBanId)
        {
            ten = (ten ?? "").Trim();
            if (ten == "" || sucChua <= 0) { TempData["Err"] = "Tên bàn bắt buộc và sức chứa phải là số dương."; return RedirectToAction("Tables", new { edit = id }); }
            int excl = id ?? 0;
            if (db.Bans.Any(x => x.Ten == ten && x.KhuVucId == khuVucId && !x.Xoa && x.Id != excl))
            { TempData["Err"] = "Tên bàn bị trùng trong cùng khu vực."; return RedirectToAction("Tables", new { edit = id }); }
            if (id.HasValue)
            {
                var ban = db.Bans.Find(id.Value);
                if (ban == null) { TempData["Err"] = "Bàn không tồn tại."; return RedirectToAction("Tables"); }
                ban.Ten = ten; ban.SucChua = sucChua; ban.KhuVucId = khuVucId; ban.LoaiBanId = loaiBanId;
            }
            else
            {
                db.Bans.Add(new Ban
                {
                    Ten = ten,
                    SucChua = sucChua,
                    KhuVucId = khuVucId,
                    LoaiBanId = loaiBanId,
                    TrangThai = "Trong",
                    MaQR = Guid.NewGuid().ToString("N").ToUpper(),
                    Xoa = false
                });
            }
            db.SaveChanges();
            TempData["Ok"] = "Đã lưu bàn.";
            return RedirectToAction("Tables");
        }

        [HttpPost]
        public ActionResult DeleteTable(int id)
        {
            DateTime now = DateTime.Now;
            int dang = db.DonDatBans.Count(x => x.BanId == id && (x.TrangThai == "DaCoc" || x.TrangThai == "DaThanhToan" || x.TrangThai == "DaCheckIn"
                                                                  || (x.TrangThai == "ChoThanhToan" && x.KhoaDen > now)))
                     + db.Bans.Count(x => x.Id == id && x.TrangThai == "DangCoKhach");
            if (dang > 0) TempData["Err"] = "Bàn còn lịch đặt dở dang hoặc đang có khách, không thể xóa.";
            else
            {
                var ban = db.Bans.Find(id);
                if (ban != null) { ban.Xoa = true; db.SaveChanges(); }
                TempData["Ok"] = "Đã xóa bàn.";
            }
            return RedirectToAction("Tables");
        }

        [HttpPost]
        public ActionResult ToggleTable(int id)
        {
            var ban = db.Bans.FirstOrDefault(x => x.Id == id && (x.TrangThai == "Trong" || x.TrangThai == "TamNgung"));
            if (ban == null) TempData["Err"] = "Chỉ tạm ngưng được bàn đang trống.";
            else { ban.TrangThai = ban.TrangThai == "TamNgung" ? "Trong" : "TamNgung"; db.SaveChanges(); }
            return RedirectToAction("Tables");
        }

        [HttpPost]
        public ActionResult RegenQr(int id)
        {
            var ban = db.Bans.Find(id);
            if (ban != null)
            {
                ban.MaQR = Guid.NewGuid().ToString("N").ToUpper();
                db.SaveChanges();
                TempData["Ok"] = "Đã tạo lại mã QR. Mã cũ không còn dùng được.";
            }
            return RedirectToAction("Tables");
        }

        public ActionResult Qr(int id)
        {
            var b = db.Bans.FirstOrDefault(x => x.Id == id && !x.Xoa);
            if (b == null) return HttpNotFound();
            string url = Request.Url.GetLeftPart(UriPartial.Authority) + Url.Action("Table", "Menu", new { token = b.MaQR });
            using (var gen = new QRCoder.QRCodeGenerator())
            using (var data = gen.CreateQrCode(url, QRCoder.QRCodeGenerator.ECCLevel.Q))
            {
                var png = new QRCoder.PngByteQRCode(data).GetGraphic(10);
                return File(png, "image/png");
            }
        }

        public ActionResult QrSheet()
        {
            ViewBag.List = (from b in db.Bans
                            join kv in db.KhuVucs on b.KhuVucId equals kv.Id
                            where !b.Xoa
                            orderby kv.Id, b.Ten
                            select new AdminTableRow { Id = b.Id, Ten = b.Ten, KhuVuc = kv.Ten }).ToList();
            return View();
        }

        public ActionResult Catalog()
        {
            var cntArea = db.Bans.Where(x => !x.Xoa).GroupBy(x => x.KhuVucId).Select(g => new { Key = g.Key, N = g.Count() }).ToList();
            var cntType = db.Bans.Where(x => !x.Xoa).GroupBy(x => x.LoaiBanId).Select(g => new { Key = g.Key, N = g.Count() }).ToList();
            ViewBag.Areas = db.KhuVucs.OrderBy(x => x.Id).ToList()
                .Select(k => new CatalogRow { Id = k.Id, Ten = k.Ten, SoBan = cntArea.Where(c => c.Key == k.Id).Select(c => c.N).FirstOrDefault() }).ToList();
            ViewBag.Types = db.LoaiBans.OrderBy(x => x.Id).ToList()
                .Select(l => new CatalogRow { Id = l.Id, Ten = l.Ten, SoBan = cntType.Where(c => c.Key == l.Id).Select(c => c.N).FirstOrDefault() }).ToList();
            return View();
        }

        [HttpPost]
        public ActionResult AddCatalog(string kind, string ten)
        {
            ten = (ten ?? "").Trim();
            bool area = kind == "area";
            if (ten == "") TempData["Err"] = "Vui lòng nhập tên.";
            else if (area ? db.KhuVucs.Any(x => x.Ten == ten) : db.LoaiBans.Any(x => x.Ten == ten)) TempData["Err"] = "Tên đã tồn tại.";
            else
            {
                if (area) db.KhuVucs.Add(new KhuVuc { Ten = ten });
                else db.LoaiBans.Add(new LoaiBan { Ten = ten });
                db.SaveChanges();
                TempData["Ok"] = "Đã thêm.";
            }
            return RedirectToAction("Catalog");
        }

        [HttpPost]
        public ActionResult DeleteCatalog(string kind, int id)
        {
            bool area = kind == "area";
            int nBan = area ? db.Bans.Count(x => x.KhuVucId == id && !x.Xoa) : db.Bans.Count(x => x.LoaiBanId == id && !x.Xoa);
            if (nBan > 0) TempData["Err"] = "Còn bàn thuộc mục này, không thể xóa.";
            else if (!area && db.MucCocs.Any(x => x.LoaiBanId == id)) TempData["Err"] = "Loại bàn còn mức cọc, hãy xóa mức cọc trước.";
            else
            {
                try
                {
                    if (area) { var k = db.KhuVucs.Find(id); if (k != null) db.KhuVucs.Remove(k); }
                    else { var l = db.LoaiBans.Find(id); if (l != null) db.LoaiBans.Remove(l); }
                    db.SaveChanges();
                    TempData["Ok"] = "Đã xóa.";
                }
                catch { TempData["Err"] = "Mục này đã phát sinh dữ liệu (bàn đã xóa mềm), không thể xóa."; }
            }
            return RedirectToAction("Catalog");
        }

        public ActionResult Menu(int? edit)
        {
            ViewBag.List = db.MonAns.Where(x => !x.An).OrderBy(x => x.DanhMuc).ThenBy(x => x.Ten).ToList();
            if (edit.HasValue)
            {
                int eid = edit.Value;
                ViewBag.Edit = db.MonAns.FirstOrDefault(x => x.Id == eid && !x.An);
            }
            return View();
        }

        [HttpPost]
        public ActionResult SaveMon(int? id, string ten, string danhMuc, decimal donGia, string moTa, string noiCheBien, HttpPostedFileBase hinh)
        {
            ten = (ten ?? "").Trim(); danhMuc = (danhMuc ?? "").Trim();
            if (ten == "" || danhMuc == "" || donGia <= 0) { TempData["Err"] = "Tên, danh mục bắt buộc và đơn giá phải lớn hơn 0."; return RedirectToAction("Menu", new { edit = id }); }
            int excl = id ?? 0;
            if (db.MonAns.Any(x => x.Ten == ten && !x.An && x.Id != excl)) { TempData["Err"] = "Tên món đã tồn tại."; return RedirectToAction("Menu", new { edit = id }); }
            if (noiCheBien != "Bar") noiCheBien = "Bep";

            string file = null;
            if (hinh != null && hinh.ContentLength > 0)
            {
                string ext = Path.GetExtension(hinh.FileName ?? "").ToLowerInvariant();
                if (!new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" }.Contains(ext) || hinh.ContentLength > 2 * 1024 * 1024 || !hinh.ContentType.StartsWith("image/"))
                { TempData["Err"] = "Hình ảnh phải là jpg/png/gif/webp và tối đa 2MB."; return RedirectToAction("Menu", new { edit = id }); }
                string dir = Server.MapPath("~/Content/img/menu");
                Directory.CreateDirectory(dir);
                file = Guid.NewGuid().ToString("N") + ext;
                hinh.SaveAs(Path.Combine(dir, file));
            }
            if (id.HasValue)
            {
                var mon = db.MonAns.Find(id.Value);
                if (mon == null) { TempData["Err"] = "Món không tồn tại."; return RedirectToAction("Menu"); }
                mon.Ten = ten; mon.DanhMuc = danhMuc; mon.DonGia = donGia; mon.MoTa = moTa; mon.NoiCheBien = noiCheBien;
                if (file != null) mon.HinhAnh = file;
            }
            else
            {
                db.MonAns.Add(new MonAn { Ten = ten, DanhMuc = danhMuc, DonGia = donGia, MoTa = moTa, NoiCheBien = noiCheBien, HinhAnh = file, ConMon = true, An = false });
            }
            db.SaveChanges();
            TempData["Ok"] = "Đã lưu món.";
            return RedirectToAction("Menu");
        }

        [HttpPost]
        public ActionResult ToggleMon(int id)
        {
            var mon = db.MonAns.Find(id);
            if (mon != null) { mon.ConMon = !mon.ConMon; db.SaveChanges(); }
            return RedirectToAction("Menu");
        }

        [HttpPost]
        public ActionResult DeleteMon(int id)
        {
            var mon = db.MonAns.Find(id);
            if (mon == null) return RedirectToAction("Menu");
            int used = db.ChiTietGoiMons.Count(x => x.MonId == id) + db.DatMonTruocs.Count(x => x.MonId == id);
            if (used > 0) { mon.An = true; TempData["Ok"] = "Món đã phát sinh đơn nên chỉ được ẩn (không xóa dữ liệu)."; }
            else { db.MonAns.Remove(mon); TempData["Ok"] = "Đã xóa món."; }
            db.SaveChanges();
            return RedirectToAction("Menu");
        }

        public ActionResult Staff(string role)
        {
            ViewBag.Role = role;
            string rl = role ?? "";
            ViewBag.List = db.TaiKhoans.Where(x => rl == "" || x.VaiTro == rl).OrderBy(x => x.VaiTro).ThenBy(x => x.HoTen).ToList();
            return View();
        }

        [HttpPost]
        public ActionResult AddStaff(string hoTen, string sdt, string tenDangNhap, string matKhau, string vaiTro)
        {
            hoTen = (hoTen ?? "").Trim(); sdt = (sdt ?? "").Trim(); tenDangNhap = (tenDangNhap ?? "").Trim();
            if (hoTen == "" || tenDangNhap == "" || !Regex.IsMatch(sdt, @"^0\d{9,10}$") || string.IsNullOrEmpty(matKhau) || matKhau.Length < 8 || !new[] { "PhucVu", "Bep", "QuanLy" }.Contains(vaiTro))
                TempData["Err"] = "Nhập đủ thông tin hợp lệ (SĐT đúng định dạng, mật khẩu tạm từ 8 ký tự).";
            else if (db.TaiKhoans.Any(x => x.TenDangNhap == tenDangNhap || x.SDT == sdt))
                TempData["Err"] = "Tên đăng nhập hoặc SĐT đã tồn tại.";
            else
            {
                var salt = Security.NewSalt();
                db.TaiKhoans.Add(new TaiKhoan
                {
                    HoTen = hoTen,
                    SDT = sdt,
                    TenDangNhap = tenDangNhap,
                    MatKhauHash = Security.Hash(matKhau, salt),
                    Salt = salt,
                    VaiTro = vaiTro,
                    BatDoiMatKhau = true,
                    TrangThai = true,
                    DiemTichLuy = 0,
                    SoLanNoShow = 0,
                    SoLanSai = 0,
                    NgayTao = DateTime.Now
                });
                db.SaveChanges();
                TempData["Ok"] = "Đã tạo tài khoản " + tenDangNhap + ".";
            }
            return RedirectToAction("Staff");
        }

        [HttpPost]
        public ActionResult SetLock(int id)
        {
            if (id == Cur.UserId) TempData["Err"] = "Không thể khóa chính tài khoản đang đăng nhập.";
            else
            {
                var tk = db.TaiKhoans.Find(id);
                if (tk != null) { tk.TrangThai = !tk.TrangThai; tk.SoLanSai = 0; tk.KhoaDen = null; db.SaveChanges(); }
            }
            return RedirectToAction("Staff");
        }

        [HttpPost]
        public ActionResult SetRole(int id, string vaiTro)
        {
            if (id == Cur.UserId || !new[] { "PhucVu", "Bep", "QuanLy" }.Contains(vaiTro)) TempData["Err"] = "Không thể đổi vai trò này.";
            else
            {
                var tk = db.TaiKhoans.FirstOrDefault(x => x.Id == id && x.VaiTro != "KhachThanhVien");
                if (tk != null) { tk.VaiTro = vaiTro; db.SaveChanges(); }
            }
            return RedirectToAction("Staff");
        }

        [HttpPost]
        public ActionResult ResetPw(int id)
        {
            var tk = db.TaiKhoans.Find(id);
            if (tk == null) return RedirectToAction("Staff");
            string tmp = Security.RandomCode(10) + "a1";
            var salt = Security.NewSalt();
            tk.MatKhauHash = Security.Hash(tmp, salt); tk.Salt = salt; tk.BatDoiMatKhau = true; tk.SoLanSai = 0; tk.KhoaDen = null;
            db.SaveChanges();
            TempData["Ok"] = "Mật khẩu tạm mới: " + tmp + " (người dùng sẽ được yêu cầu đổi khi đăng nhập).";
            return RedirectToAction("Staff");
        }

        public ActionResult Config()
        {
            ViewBag.Cfg = db.CauHinhs.OrderBy(x => x.Khoa).ToList();
            ViewBag.Coc = (from m in db.MucCocs
                           join l in db.LoaiBans on m.LoaiBanId equals l.Id
                           orderby l.Id, m.SoTien
                           select new CocRow { Id = m.Id, SoTien = m.SoTien, Loai = l.Ten }).ToList();
            ViewBag.Types = db.LoaiBans.OrderBy(x => x.Id).ToList();
            ViewBag.Coupons = db.MaGiamGias.OrderByDescending(x => x.Id).ToList();
            return View();
        }

        [HttpPost]
        public ActionResult SaveCfg(string khoa, int giaTri)
        {
            if (giaTri < 1) TempData["Err"] = "Giá trị phải lớn hơn 0.";
            else
            {
                var c = db.CauHinhs.Find(khoa);
                if (c != null) { c.GiaTri = giaTri.ToString(); db.SaveChanges(); }
                TempData["Ok"] = "Đã lưu cấu hình.";
            }
            return RedirectToAction("Config");
        }

        [HttpPost]
        public ActionResult AddCoc(int loaiBanId, decimal soTien)
        {
            if (soTien <= 0) TempData["Err"] = "Mức cọc phải lớn hơn 0.";
            else { db.MucCocs.Add(new MucCoc { LoaiBanId = loaiBanId, SoTien = soTien }); db.SaveChanges(); }
            return RedirectToAction("Config");
        }

        [HttpPost]
        public ActionResult DelCoc(int id)
        {
            var m = db.MucCocs.Find(id);
            if (m != null) { db.MucCocs.Remove(m); db.SaveChanges(); }
            return RedirectToAction("Config");
        }

        [HttpPost]
        public ActionResult AddCoupon(string ma, int? phanTram, decimal? soTien, string hanDung)
        {
            DateTime hd;
            ma = (ma ?? "").Trim().ToUpper();
            if (ma == "" || !DateTime.TryParse(hanDung, out hd) || (phanTram.HasValue == soTien.HasValue) ||
                (phanTram.HasValue && (phanTram < 1 || phanTram > 100)) || (soTien.HasValue && soTien <= 0))
                TempData["Err"] = "Nhập mã, hạn dùng và đúng một trong hai: % giảm hoặc số tiền giảm.";
            else if (db.MaGiamGias.Any(x => x.Ma == ma)) TempData["Err"] = "Mã đã tồn tại.";
            else
            {
                db.MaGiamGias.Add(new MaGiamGia { Ma = ma, PhanTram = phanTram, SoTien = soTien, HanDung = hd.Date, HoatDong = true });
                db.SaveChanges();
                TempData["Ok"] = "Đã tạo mã.";
            }
            return RedirectToAction("Config");
        }

        [HttpPost]
        public ActionResult ToggleCoupon(int id)
        {
            var c = db.MaGiamGias.Find(id);
            if (c != null) { c.HoatDong = !c.HoatDong; db.SaveChanges(); }
            return RedirectToAction("Config");
        }

        public ActionResult Bookings(string q, string st)
        {
            ViewBag.Q = q; ViewBag.St = st;
            string key = (q ?? "").Trim().ToUpper();
            string s = st ?? "";
            ViewBag.List = (from d in db.DonDatBans
                            join b in db.Bans on d.BanId equals b.Id
                            where (key == "" || d.MaDon == key || d.SDT == key) && (s == "" || d.TrangThai == s)
                            orderby d.NgayGio descending
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
                            }).Take(200).ToList();
            return View();
        }

        [HttpPost]
        public ActionResult AdminCancel(int id, bool hoanCoc)
        {
            var d = db.DonDatBans.Find(id);
            if (d == null || !(d.TrangThai == "ChoThanhToan" || d.TrangThai == "DaCoc" || d.TrangThai == "DaThanhToan"))
            { TempData["Err"] = "Chỉ hủy được đơn chưa check-in."; return RedirectToAction("Bookings"); }

            d.TrangThai = "DaHuy";
            if (hoanCoc && d.TienCoc > 0)
            {
                db.ThanhToans.Add(new ThanhToan
                {
                    DonDatBanId = d.Id,
                    Loai = "HoanCoc",
                    SoTien = d.TienCoc,
                    PhuongThuc = "Online",
                    TrangThai = "ThanhCong",
                    Ngay = DateTime.Now
                });
            }
            db.SaveChanges();
            TempData["Ok"] = "Đã hủy đơn " + d.MaDon + (hoanCoc ? " và hoàn cọc." : " (không hoàn cọc).");
            return RedirectToAction("Bookings");
        }

        public ActionResult Report(string tu, string den)
        {
            DateTime d1, d2;
            if (!DateTime.TryParse(tu, out d1)) d1 = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            if (!DateTime.TryParse(den, out d2)) d2 = DateTime.Now.Date;
            ViewBag.Tu = d1.ToString("yyyy-MM-dd"); ViewBag.Den = d2.ToString("yyyy-MM-dd");
            DateTime endEx = d2.AddDays(1);
            var st4 = new[] { "DaCoc", "DaThanhToan", "DaCheckIn", "HoanThanh" };
            var st6 = new[] { "DaCoc", "DaThanhToan", "DaCheckIn", "HoanThanh", "NoShow", "MangVe" };

            var hds = db.HoaDons.Where(x => x.NgayTao >= d1 && x.NgayTao < endEx).Select(x => new { x.NgayTao, x.TongTien, x.GiamGia }).ToList();
            ViewBag.Revenue = hds.GroupBy(h => h.NgayTao.Date).OrderBy(g => g.Key)
                .Select(g => new RevenueRow { Ngay = g.Key, SoHD = g.Count(), DoanhThu = g.Sum(h => h.TongTien - h.GiamGia) }).ToList();

            ViewBag.Coc = new CocSummary
            {
                Thu = db.ThanhToans.Where(x => x.Loai == "Coc" && x.Ngay >= d1 && x.Ngay < endEx).Sum(x => (decimal?)x.SoTien) ?? 0m,
                Hoan = db.ThanhToans.Where(x => x.Loai == "HoanCoc" && x.Ngay >= d1 && x.Ngay < endEx).Sum(x => (decimal?)x.SoTien) ?? 0m,
                Mat = db.DonDatBans.Where(x => x.TrangThai == "NoShow" && x.NgayGio >= d1 && x.NgayGio < endEx).Sum(x => (decimal?)x.TienCoc) ?? 0m
            };

            var trangThai = db.DonDatBans.Where(x => st6.Contains(x.TrangThai) && x.NgayGio >= d1 && x.NgayGio < endEx).Select(x => x.TrangThai).ToList();
            ViewBag.NoShow = new NoShowSummary { Tong = trangThai.Count, NoShow = trangThai.Count(s => s == "NoShow" || s == "MangVe") };

            ViewBag.Top = (from c in db.ChiTietGoiMons
                           join g in db.DonGoiMons on c.DonGoiMonId equals g.Id
                           join m in db.MonAns on c.MonId equals m.Id
                           where g.TrangThai == "DaXacNhan" && g.NgayTao >= d1 && g.NgayTao < endEx
                           group c by m.Ten into grp
                           orderby grp.Sum(x => x.SoLuong) descending
                           select new NameCount { Ten = grp.Key, SoLuong = grp.Sum(x => x.SoLuong) }).Take(10).ToList();

            var gios = db.DonDatBans.Where(x => st4.Contains(x.TrangThai) && x.NgayGio >= d1 && x.NgayGio < endEx).Select(x => x.NgayGio).ToList();
            ViewBag.Peak = gios.GroupBy(h => h.Hour).Select(g => new PeakRow { Gio = g.Key, SoDon = g.Count() }).OrderByDescending(p => p.SoDon).ToList();

            var khuVucs = db.KhuVucs.OrderBy(x => x.Id).ToList();
            var banKv = db.Bans.Select(x => new { x.Id, x.KhuVucId }).ToList();
            var donBan = db.DonDatBans.Where(x => st4.Contains(x.TrangThai) && x.NgayGio >= d1 && x.NgayGio < endEx).Select(x => x.BanId).ToList();
            ViewBag.Fill = khuVucs.Select(k => new FillRow
            {
                Ten = k.Ten,
                SoDon = donBan.Count(bid => banKv.Any(bk => bk.Id == bid && bk.KhuVucId == k.Id))
            }).ToList();
            return View();
        }
        [HttpPost]
        public ActionResult EditStaff(int id, string hoTen, string sdt)
        {
            hoTen = (hoTen ?? "").Trim(); sdt = (sdt ?? "").Trim();
            if (hoTen == "" || !Regex.IsMatch(sdt, @"^0\d{9,10}$")) TempData["Err"] = "Họ tên bắt buộc, SĐT sai định dạng.";
            else if (db.TaiKhoans.Any(x => x.SDT == sdt && x.Id != id)) TempData["Err"] = "SĐT đã được dùng.";
            else
            {
                var tk = db.TaiKhoans.Find(id);
                if (tk != null) { tk.HoTen = hoTen; tk.SDT = sdt; db.SaveChanges(); TempData["Ok"] = "Đã cập nhật."; }
            }
            return RedirectToAction("Staff");
        }
        [HttpPost]
        public ActionResult RenameCatalog(string kind, int id, string ten)
        {
            ten = (ten ?? "").Trim();
            bool area = kind == "area";
            if (ten == "") TempData["Err"] = "Vui lòng nhập tên.";
            else if (area ? db.KhuVucs.Any(x => x.Ten == ten && x.Id != id) : db.LoaiBans.Any(x => x.Ten == ten && x.Id != id))
                TempData["Err"] = "Tên đã tồn tại.";
            else
            {
                if (area) { var k = db.KhuVucs.Find(id); if (k != null) k.Ten = ten; }
                else { var l = db.LoaiBans.Find(id); if (l != null) l.Ten = ten; }
                db.SaveChanges();
                TempData["Ok"] = "Đã đổi tên.";
            }
            return RedirectToAction("Catalog");
        }
    }
}