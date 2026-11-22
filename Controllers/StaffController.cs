using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Mvc;
using DatBanQuanAn.Helpers;
using DatBanQuanAn.Models;

namespace DatBanQuanAn.Controllers
{

    public class StaffTableRow
    {
        public int Id { get; set; }
        public string Ten { get; set; }
        public int SucChua { get; set; }
        public string TrangThai { get; set; }
        public string Loai { get; set; }
        public int? DonSapDen { get; set; }
    }

    public class StaffBookingRow : DonDatBanView
    {
        public int Tre { get; set; }
    }

    public class TableOption
    {
        public int Id { get; set; }
        public string Ten { get; set; }
        public int SucChua { get; set; }
        public string KhuVuc { get; set; }
    }

    public class PendingOrderRow
    {
        public int Id { get; set; }
        public string HinhThucTT { get; set; }
        public bool DaThanhToan { get; set; }
        public DateTime NgayTao { get; set; }
        public string Ban { get; set; }
    }

    public class PendingItemRow
    {
        public int DonGoiMonId { get; set; }
        public string Ten { get; set; }
        public int SoLuong { get; set; }
        public string GhiChu { get; set; }
    }

    public class ReadyItemRow
    {
        public int Id { get; set; }
        public string Ten { get; set; }
        public int SoLuong { get; set; }
        public string Ban { get; set; }
    }

    public class BillItemRow
    {
        public string Ten { get; set; }
        public int SoLuong { get; set; }
        public decimal DonGia { get; set; }
        public bool DaThanhToan { get; set; }
    }

    public class BillInfo
    {
        public List<BillItemRow> Items;
        public decimal Tong, DaTra, Coc, Giam, ConPhai;
        public DonDatBan Booking;
        public string Msg;
        public int Pending;
    }

    [Role("PhucVu", "QuanLy")]
    public class StaffController : Controller
    {
        DatBanQuanAnEntities db = new DatBanQuanAnEntities();

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }

        static readonly string[] Reserved = { "DaCoc", "DaThanhToan", "ChoThanhToan" };

        public ActionResult Tables(int? khuVucId)
        {
            var areas = db.KhuVucs.OrderBy(x => x.Id).ToList();
            int kv = khuVucId ?? (areas.Count > 0 ? areas[0].Id : 0);
            ViewBag.Areas = areas; ViewBag.Kv = kv;

            DateTime now = DateTime.Now;
            DateTime tu = now.AddMinutes(-15), den = now.AddMinutes(90);
            var sapDen = db.DonDatBans
                .Where(x => (x.TrangThai == "DaCoc" || x.TrangThai == "DaThanhToan") && x.NgayGio >= tu && x.NgayGio <= den)
                .OrderBy(x => x.NgayGio).Select(x => new { x.BanId, x.Id }).ToList();
            var rows = (from b in db.Bans
                        join lb in db.LoaiBans on b.LoaiBanId equals lb.Id
                        where !b.Xoa && b.KhuVucId == kv
                        orderby b.Ten
                        select new { b.Id, b.Ten, b.SucChua, b.TrangThai, Loai = lb.Ten }).ToList();
            var tables = new List<StaffTableRow>();
            foreach (var r in rows)
            {
                var don = sapDen.FirstOrDefault(x => x.BanId == r.Id);
                tables.Add(new StaffTableRow
                {
                    Id = r.Id,
                    Ten = r.Ten,
                    SucChua = r.SucChua,
                    TrangThai = r.TrangThai,
                    Loai = r.Loai,
                    DonSapDen = don == null ? (int?)null : don.Id
                });
            }
            ViewBag.Tables = tables;

            DateTime fin = now.AddMinutes(120);
            var res = Reserved;
            var busy = new HashSet<int>(db.DonDatBans
                .Where(x => res.Contains(x.TrangThai) && x.NgayGio >= now && x.NgayGio <= fin)
                .Select(x => x.BanId).Distinct().ToList());
            ViewBag.Free = db.Bans.Where(x => !x.Xoa && x.TrangThai == "Trong").OrderBy(x => x.Ten).ToList()
                .Where(x => !busy.Contains(x.Id)).ToList();
            return View();
        }

        [HttpPost]
        public ActionResult OpenTable(int banId, int? kv)
        {
            var ban = db.Bans.FirstOrDefault(x => x.Id == banId && x.TrangThai == "Trong" && !x.Xoa);
            if (ban == null) TempData["Err"] = "Không thể mở bàn này.";
            else { ban.TrangThai = "DangCoKhach"; db.SaveChanges(); }
            return RedirectToAction("Tables", new { khuVucId = kv });
        }

        [HttpPost]
        public ActionResult Clean(int banId, int? kv)
        {
            var ban = db.Bans.FirstOrDefault(x => x.Id == banId && x.TrangThai == "DangDon");
            if (ban != null) { ban.TrangThai = "Trong"; db.SaveChanges(); }
            return RedirectToAction("Tables", new { khuVucId = kv });
        }

        [HttpPost]
        public ActionResult Move(int banId, int newBanId, int? kv)
        {
            var old = db.Bans.Find(banId);
            if (old == null || old.TrangThai != "DangCoKhach")
            { TempData["Err"] = "Bàn hiện tại không có khách."; return RedirectToAction("Tables", new { khuVucId = kv }); }

            using (var tx = db.Database.BeginTransaction(IsolationLevel.Serializable))
            {
                var nb = db.Bans.FirstOrDefault(x => x.Id == newBanId && !x.Xoa && x.TrangThai == "Trong");
                int can = db.DonDatBans.Where(x => x.BanId == banId && x.TrangThai == "DaCheckIn")
                                       .Select(x => (int?)x.SoNguoi).Max() ?? 1;
                DateTime now = DateTime.Now, fin = now.AddMinutes(120);
                var res = Reserved;
                bool conLich = nb != null && db.DonDatBans.Any(x => x.BanId == newBanId && res.Contains(x.TrangThai) && x.NgayGio >= now && x.NgayGio <= fin);
                if (nb == null || nb.SucChua < can || conLich)
                {
                    tx.Rollback();
                    TempData["Err"] = "Bàn đích không trống, đã có lịch đặt hoặc không đủ sức chứa.";
                    return RedirectToAction("Tables", new { khuVucId = kv });
                }
                foreach (var don in db.DonDatBans.Where(x => x.BanId == banId && x.TrangThai == "DaCheckIn").ToList()) don.BanId = newBanId;
                foreach (var goi in db.DonGoiMons.Where(x => x.BanId == banId && !x.DaHoaDon).ToList()) goi.BanId = newBanId;
                old.TrangThai = "DangDon";
                nb.TrangThai = "DangCoKhach";
                db.SaveChanges();
                tx.Commit();
            }
            TempData["Ok"] = "Đã chuyển bàn. Tiền cọc và món đã gọi được giữ nguyên.";
            return RedirectToAction("Tables", new { khuVucId = kv });
        }

        public ActionResult Bookings()
        {
            DateTime lim = DateTime.Now.AddDays(14);
            var st = new[] { "DaCoc", "DaThanhToan", "MangVe" };
            var list = (from d in db.DonDatBans
                        join b in db.Bans on d.BanId equals b.Id
                        where st.Contains(d.TrangThai) && d.NgayGio < lim
                        orderby d.NgayGio
                        select new StaffBookingRow
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
                        }).ToList();
            foreach (var r in list) r.Tre = (int)(DateTime.Now - r.NgayGio).TotalMinutes;
            ViewBag.List = list;
            ViewBag.GiuBan = Cfg.Int("GiuBanPhut", 15);
            ViewBag.AllTables = (from b in db.Bans
                                 join kv in db.KhuVucs on b.KhuVucId equals kv.Id
                                 where !b.Xoa
                                 orderby kv.Id, b.Ten
                                 select new TableOption { Id = b.Id, Ten = b.Ten, SucChua = b.SucChua, KhuVuc = kv.Ten }).ToList();
            return View();
        }

        [HttpPost]
        public ActionResult CheckIn(int id)
        {
            var d = db.DonDatBans.FirstOrDefault(x => x.Id == id && (x.TrangThai == "DaCoc" || x.TrangThai == "DaThanhToan"));
            if (d == null) { TempData["Err"] = "Đơn không hợp lệ."; return RedirectToAction("Bookings"); }
            var ban = db.Bans.Find(d.BanId);
            if (ban.TrangThai != "Trong") { TempData["Err"] = "Bàn chưa sẵn sàng (" + Fmt.StatusTable(ban.TrangThai) + ")."; return RedirectToAction("Bookings"); }

            using (var tx = db.Database.BeginTransaction())
            {
                d.TrangThai = "DaCheckIn";
                ban.TrangThai = "DangCoKhach";
                if (d.HinhThuc == "KemMon") KitchenLink.AttachAtCheckIn(db, d);
                db.SaveChanges();
                tx.Commit();
            }
            TempData["Ok"] = "Đã check-in và mở bàn cho đơn " + d.MaDon + ".";
            return RedirectToAction("Bookings");
        }

        [HttpPost]
        public ActionResult NoShow(int id)
        {
            var d = db.DonDatBans.FirstOrDefault(x => x.Id == id && (x.TrangThai == "DaCoc" || x.TrangThai == "DaThanhToan"));
            if (d == null) { TempData["Err"] = "Đơn không hợp lệ."; return RedirectToAction("Bookings"); }
            if ((DateTime.Now - d.NgayGio).TotalMinutes < Cfg.Int("GiuBanPhut", 15)) { TempData["Err"] = "Chưa quá thời gian giữ bàn."; return RedirectToAction("Bookings"); }
            bool kemMon = d.HinhThuc == "KemMon";
            d.TrangThai = kemMon ? "MangVe" : "NoShow";
            if (d.KhachId != null)
            {
                var kh = db.TaiKhoans.Find(d.KhachId.Value);
                if (kh != null) kh.SoLanNoShow++;
            }
            db.SaveChanges();
            TempData["Ok"] = kemMon ? "Đã chuyển sang 'Đóng gói mang về' (giữ món tối đa " + Cfg.Int("GiuMonMangVeGio", 2) + " giờ)." : "Đã ghi nhận no-show, khách mất tiền cọc. Bàn được giải phóng.";
            return RedirectToAction("Bookings");
        }

        [HttpPost]
        public ActionResult TakeAwayDone(int id)
        {
            var d = db.DonDatBans.FirstOrDefault(x => x.Id == id && x.TrangThai == "MangVe");
            if (d != null) { d.TrangThai = "HoanThanh"; db.SaveChanges(); }
            return RedirectToAction("Bookings");
        }

        [HttpPost]
        public ActionResult CounterBooking(int banId, string ngay, string gio, int soNguoi, string hoTen, string sdt, decimal coc)
        {
            DateTime dt;
            if (!DateTime.TryParseExact(ngay + " " + gio, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out dt) || dt < DateTime.Now.AddMinutes(-5))
            { TempData["Err"] = "Ngày giờ không hợp lệ."; return RedirectToAction("Bookings"); }
            if (string.IsNullOrWhiteSpace(hoTen) || !Regex.IsMatch(sdt ?? "", @"^0\d{9,10}$") || coc < 0) { TempData["Err"] = "Họ tên, SĐT hoặc tiền cọc không hợp lệ."; return RedirectToAction("Bookings"); }
            string phone = sdt;
            int? acc = db.TaiKhoans.Where(x => x.SDT == phone && x.VaiTro == "KhachThanhVien").Select(x => (int?)x.Id).FirstOrDefault();
            int id; string ma, err;
            if (!BookingService.TryCreate(db, banId, dt, soNguoi, acc, sdt, hoTen.Trim(), null, "CocBan", coc, new List<int[]>(), "DaCoc", out id, out ma, out err))
            { TempData["Err"] = err; return RedirectToAction("Bookings"); }
            if (coc > 0)
            {
                db.ThanhToans.Add(new ThanhToan { DonDatBanId = id, Loai = "Coc", SoTien = coc, PhuongThuc = "TienMat", TrangThai = "ThanhCong", Ngay = DateTime.Now });
                db.SaveChanges();
            }
            TempData["Ok"] = "Đã tạo đơn " + ma + " cho khách " + hoTen.Trim() + ".";
            return RedirectToAction("Bookings");
        }

        [HttpPost]
        public ActionResult CancelBooking(int id)
        {
            var d = db.DonDatBans.FirstOrDefault(x => x.Id == id && (x.TrangThai == "DaCoc" || x.TrangThai == "DaThanhToan"));
            if (d != null)
            {
                d.TrangThai = "DaHuy";
                KitchenLink.CancelPending(db, d.Id);
                db.SaveChanges();
                TempData["Ok"] = "Đã hủy đơn " + d.MaDon + ". Hoàn tiền xử lý theo thỏa thuận với khách.";
            }
            return RedirectToAction("Bookings");
        }

        public ActionResult Orders()
        {
            ViewBag.Pending = (from g in db.DonGoiMons
                               join b in db.Bans on g.BanId equals b.Id
                               where g.TrangThai == "ChoXacNhan"
                               orderby g.NgayTao
                               select new PendingOrderRow { Id = g.Id, HinhThucTT = g.HinhThucTT, DaThanhToan = g.DaThanhToan, NgayTao = g.NgayTao, Ban = b.Ten }).ToList();
            ViewBag.PendingItems = (from c in db.ChiTietGoiMons
                                    join m in db.MonAns on c.MonId equals m.Id
                                    join g in db.DonGoiMons on c.DonGoiMonId equals g.Id
                                    where g.TrangThai == "ChoXacNhan"
                                    select new PendingItemRow { DonGoiMonId = c.DonGoiMonId, Ten = m.Ten, SoLuong = c.SoLuong, GhiChu = c.GhiChu }).ToList();
            ViewBag.Ready = (from c in db.ChiTietGoiMons
                             join g in db.DonGoiMons on c.DonGoiMonId equals g.Id
                             join m in db.MonAns on c.MonId equals m.Id
                             join b in db.Bans on g.BanId equals b.Id
                             where c.TrangThai == "DaXong" && g.TrangThai == "DaXacNhan"
                             orderby c.Id
                             select new ReadyItemRow { Id = c.Id, Ten = m.Ten, SoLuong = c.SoLuong, Ban = b.Ten }).ToList();
            return View();
        }

        [HttpPost]
        public ActionResult ConfirmOrder(int id)
        {
            var g = db.DonGoiMons.FirstOrDefault(x => x.Id == id && x.TrangThai == "ChoXacNhan");
            if (g != null) { g.TrangThai = "DaXacNhan"; db.SaveChanges(); }
            return RedirectToAction("Orders");
        }

        [HttpPost]
        public ActionResult RejectOrder(int id)
        {
            var g = db.DonGoiMons.FirstOrDefault(x => x.Id == id && x.TrangThai == "ChoXacNhan");
            if (g != null)
            {
                g.TrangThai = "Huy";
                if (g.DaThanhToan)
                {
                    decimal tong = db.ChiTietGoiMons.Where(x => x.DonGoiMonId == id).Sum(x => (decimal?)(x.SoLuong * x.DonGia)) ?? 0m;
                    if (tong > 0)
                        db.ThanhToans.Add(new ThanhToan { DonGoiMonId = id, Loai = "HoanCoc", SoTien = tong, PhuongThuc = "GiaLap", TrangThai = "ThanhCong", Ngay = DateTime.Now });
                }
                db.SaveChanges();
            }
            return RedirectToAction("Orders");
        }

        [HttpPost]
        public ActionResult Served(int id)
        {
            var item = db.ChiTietGoiMons.FirstOrDefault(x => x.Id == id && x.TrangThai == "DaXong");
            if (item != null) { item.TrangThai = "DaPhucVu"; db.SaveChanges(); }
            return RedirectToAction("Orders");
        }

        BillInfo Calc(int banId, string ma)
        {
            var bi = new BillInfo();
            bi.Items = (from c in db.ChiTietGoiMons
                        join g in db.DonGoiMons on c.DonGoiMonId equals g.Id
                        join m in db.MonAns on c.MonId equals m.Id
                        where g.BanId == banId && !g.DaHoaDon && g.TrangThai == "DaXacNhan" && c.TrangThai != "Huy"
                        orderby g.NgayTao, c.Id
                        select new BillItemRow { Ten = m.Ten, SoLuong = c.SoLuong, DonGia = c.DonGia, DaThanhToan = g.DaThanhToan }).ToList();
            foreach (var r in bi.Items)
            {
                decimal line = r.SoLuong * r.DonGia;
                bi.Tong += line;
                if (r.DaThanhToan) bi.DaTra += line;
            }
            bi.Pending = db.DonGoiMons.Count(x => x.BanId == banId && !x.DaHoaDon && x.TrangThai == "ChoXacNhan");
            bi.Booking = db.DonDatBans.Where(x => x.BanId == banId && x.TrangThai == "DaCheckIn").OrderByDescending(x => x.NgayGio).FirstOrDefault();
            if (bi.Booking != null && bi.Booking.HinhThuc == "CocBan") bi.Coc = bi.Booking.TienCoc;
            if (!string.IsNullOrWhiteSpace(ma))
            {
                string code = ma.Trim().ToUpper();
                DateTime today = DateTime.Today;
                var cp = db.MaGiamGias.FirstOrDefault(x => x.Ma == code && x.HoatDong && x.HanDung >= today);
                if (cp == null) bi.Msg = "Mã giảm giá không tồn tại hoặc đã hết hạn.";
                else
                {
                    bi.Giam = cp.PhanTram != null ? Math.Round(bi.Tong * cp.PhanTram.Value / 100m) : (cp.SoTien ?? 0m);
                    if (bi.Giam > bi.Tong) bi.Giam = bi.Tong;
                }
            }
            bi.ConPhai = Math.Max(0m, bi.Tong - bi.Giam - bi.DaTra - bi.Coc);
            return bi;
        }

        public ActionResult Bill(int banId, string ma)
        {
            var ban = db.Bans.Find(banId);
            if (ban == null) return HttpNotFound();
            var bi = Calc(banId, ma);
            ViewBag.Ban = ban;
            ViewBag.KhuVuc = db.KhuVucs.Find(ban.KhuVucId).Ten;
            ViewBag.Bi = bi; ViewBag.Ma = ma;
            ViewBag.Items = bi.Items; ViewBag.Tong = bi.Tong; ViewBag.DaTra = bi.DaTra; ViewBag.Coc = bi.Coc; ViewBag.Giam = bi.Giam; ViewBag.ConPhai = bi.ConPhai;
            ViewBag.Msg = bi.Msg; ViewBag.Pending = bi.Pending; ViewBag.Booking = bi.Booking;
            return View();
        }

        [HttpPost]
        public ActionResult PayBill(int banId, string ma, string sdt, string phuongThuc)
        {
            var ban = db.Bans.Find(banId);
            if (ban == null || ban.TrangThai != "DangCoKhach") { TempData["Err"] = "Bàn không ở trạng thái đang có khách."; return RedirectToAction("Tables"); }
            var bi = Calc(banId, ma);
            if (bi.Pending > 0) { TempData["Err"] = "Còn " + bi.Pending + " đơn QR chờ xác nhận. Hãy xử lý trước khi lập hóa đơn."; return RedirectToAction("Bill", new { banId, ma }); }
            if (bi.Msg != null) { TempData["Err"] = bi.Msg; return RedirectToAction("Bill", new { banId, ma }); }
            if (bi.Items.Count == 0 && bi.Booking == null) { TempData["Err"] = "Bàn chưa có món nào để tính tiền."; return RedirectToAction("Bill", new { banId, ma }); }
            if (phuongThuc != "TienMat" && phuongThuc != "Online") phuongThuc = "TienMat";

            int diemMoiDong = Math.Max(1, Cfg.Int("DiemTrenMoiDong", 10000));
            int hid;
            using (var tx = db.Database.BeginTransaction())
            {
                var hd = new HoaDon
                {
                    MaHD = "HD" + DateTime.Now.ToString("yyMMddHHmmss") + banId,
                    BanId = banId,
                    DonDatBanId = bi.Booking == null ? (int?)null : bi.Booking.Id,
                    TongTien = bi.Tong,
                    DaTra = bi.DaTra,
                    TienCoc = bi.Coc,
                    GiamGia = bi.Giam,
                    ConPhaiTra = bi.ConPhai,
                    PhuongThuc = phuongThuc,
                    MaGiamGia = string.IsNullOrWhiteSpace(ma) ? null : ma.Trim().ToUpper(),
                    NhanVienId = Cur.UserId,
                    NgayTao = DateTime.Now
                };
                db.HoaDons.Add(hd);
                db.SaveChanges();
                hid = hd.Id;

                foreach (var goi in db.DonGoiMons.Where(x => x.BanId == banId && !x.DaHoaDon && x.TrangThai == "DaXacNhan").ToList())
                { goi.DaHoaDon = true; goi.DaThanhToan = true; goi.HoaDonId = hid; }
                if (bi.ConPhai > 0)
                    db.ThanhToans.Add(new ThanhToan { HoaDonId = hid, Loai = "HoaDon", SoTien = bi.ConPhai, PhuongThuc = phuongThuc, TrangThai = "ThanhCong", Ngay = DateTime.Now });
                if (bi.Booking != null) bi.Booking.TrangThai = "HoanThanh";
                ban.TrangThai = "DangDon";

                int? acc = bi.Booking != null ? bi.Booking.KhachId : null;
                if (acc == null && !string.IsNullOrWhiteSpace(sdt))
                {
                    string phone = sdt.Trim();
                    acc = db.TaiKhoans.Where(x => x.SDT == phone && x.VaiTro == "KhachThanhVien").Select(x => (int?)x.Id).FirstOrDefault();
                }
                if (acc != null)
                {
                    int diem = (int)((bi.Tong - bi.Giam) / diemMoiDong);
                    var tk = db.TaiKhoans.Find(acc.Value);
                    if (diem > 0 && tk != null) tk.DiemTichLuy += diem;
                }
                db.SaveChanges();
                tx.Commit();
            }
            return RedirectToAction("Invoice", new { id = hid });
        }

        public ActionResult Invoice(int id)
        {
            var h = db.HoaDons.Find(id);
            if (h == null) return HttpNotFound();
            ViewBag.H = h;
            ViewBag.Ban = db.Bans.Find(h.BanId).Ten;
            ViewBag.Items = (from c in db.ChiTietGoiMons
                             join g in db.DonGoiMons on c.DonGoiMonId equals g.Id
                             join m in db.MonAns on c.MonId equals m.Id
                             where g.HoaDonId == id && c.TrangThai != "Huy"
                             orderby c.Id
                             select new BillItemRow { Ten = m.Ten, SoLuong = c.SoLuong, DonGia = c.DonGia }).ToList();
            return View();
        }
    }
}