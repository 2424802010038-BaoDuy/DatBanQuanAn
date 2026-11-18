using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using System.Web.Script.Serialization;
using DatBanQuanAn.Helpers;
using DatBanQuanAn.Models;

namespace DatBanQuanAn.Controllers
{
    public class MenuOrderRow
    {
        public string Ten { get; set; }
        public int SoLuong { get; set; }
        public string TrangThai { get; set; }
        public string TrangThaiDon { get; set; }
        public bool DaThanhToan { get; set; }
    }

    public class MenuController : Controller
    {
        class CartItem { public int id { get; set; } public int qty { get; set; } public string note { get; set; } }

        DatBanQuanAnEntities db = new DatBanQuanAnEntities();

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }

        Ban FindTable(string token)
        {
            token = token ?? "";
            return db.Bans.FirstOrDefault(b => b.MaQR == token && !b.Xoa);
        }

        public ActionResult Table(string token)
        {
            var b = FindTable(token);
            if (b == null) return HttpNotFound();
            ViewBag.Ban = b;
            ViewBag.KhuVuc = b.KhuVuc.Ten;
            ViewBag.Token = token;
            if (b.TrangThai == "DangCoKhach")
            {
                ViewBag.Menu = db.MonAns.Where(m => m.ConMon && !m.An)
                                        .OrderBy(m => m.DanhMuc).ThenBy(m => m.Ten).ToList();
                int banId = b.Id;
                ViewBag.Orders = (from c in db.ChiTietGoiMons
                                  join g in db.DonGoiMons on c.DonGoiMonId equals g.Id
                                  join m in db.MonAns on c.MonId equals m.Id
                                  where g.BanId == banId && !g.DaHoaDon && g.TrangThai != "Huy"
                                  orderby g.NgayTao descending, c.Id
                                  select new MenuOrderRow
                                  {
                                      Ten = m.Ten,
                                      SoLuong = c.SoLuong,
                                      TrangThai = c.TrangThai,
                                      TrangThaiDon = g.TrangThai,
                                      DaThanhToan = g.DaThanhToan
                                  }).ToList();
            }
            return View();
        }

        [HttpPost]
        public ActionResult Submit(string token, string cart, string hinhThuc)
        {
            var b = FindTable(token);
            if (b == null) return HttpNotFound();
            if (b.TrangThai != "DangCoKhach") { TempData["Err"] = "Bàn chưa được mở. Vui lòng báo nhân viên."; return RedirectToAction("Table", new { token }); }
            List<CartItem> items;
            try { items = new JavaScriptSerializer().Deserialize<List<CartItem>>(cart ?? "[]"); } catch { items = null; }
            if (items == null || items.Count == 0) { TempData["Err"] = "Giỏ hàng trống."; return RedirectToAction("Table", new { token }); }
            if (hinhThuc != "Online" && hinhThuc != "TienMat") hinhThuc = "TienMat";

            int orderId; decimal tong = 0;
            using (var tx = db.Database.BeginTransaction())
            {
                var don = new DonGoiMon
                {
                    BanId = b.Id,
                    HinhThucTT = hinhThuc,
                    DaThanhToan = false,
                    TrangThai = "ChoXacNhan",
                    DaHoaDon = false,
                    NgayTao = DateTime.Now
                };
                db.DonGoiMons.Add(don);
                db.SaveChanges();
                orderId = don.Id;

                foreach (var it in items)
                {
                    if (it.qty < 1 || it.qty > 50) continue;
                    int monId = it.id;
                    var mon = db.MonAns.FirstOrDefault(m => m.Id == monId && m.ConMon && !m.An);
                    if (mon == null) { TempData["Err"] = "Có món vừa hết. Vui lòng chọn lại."; return RedirectToAction("Table", new { token }); }
                    var note = it.note == null ? null : (it.note.Length > 200 ? it.note.Substring(0, 200) : it.note);
                    db.ChiTietGoiMons.Add(new ChiTietGoiMon
                    {
                        DonGoiMonId = orderId,
                        MonId = mon.Id,
                        SoLuong = it.qty,
                        DonGia = mon.DonGia,
                        GhiChu = note,
                        TrangThai = "ChoCheBien"
                    });
                    tong += mon.DonGia * it.qty;
                }
                if (tong == 0) { TempData["Err"] = "Giỏ hàng không hợp lệ."; return RedirectToAction("Table", new { token }); }
                db.SaveChanges();
                tx.Commit();
            }
            if (hinhThuc == "Online") { Session["PayingQR"] = orderId; return RedirectToAction("Pay", new { id = orderId, token }); }
            TempData["Ok"] = "Đã gửi đơn. Nhân viên sẽ xác nhận và chuyển cho Bếp/Bar. Bạn thanh toán tiền mặt cho thu ngân sau bữa ăn.";
            return RedirectToAction("Table", new { token });
        }

        public ActionResult Pay(int id, string token)
        {
            if (!(Session["PayingQR"] is int) || (int)Session["PayingQR"] != id) return RedirectToAction("Table", new { token });
            ViewBag.Token = token; ViewBag.Id = id;
            ViewBag.Total = db.ChiTietGoiMons.Where(c => c.DonGoiMonId == id).Sum(c => (decimal?)(c.SoLuong * c.DonGia)) ?? 0m;
            return View();
        }

        [HttpPost]
        public ActionResult PayResult(int id, string token, string result)
        {
            if (!(Session["PayingQR"] is int) || (int)Session["PayingQR"] != id) return RedirectToAction("Table", new { token });
            var don = db.DonGoiMons.Find(id);
            if (don != null)
            {
                if (result == "ok")
                {
                    don.DaThanhToan = true;
                    decimal tong = db.ChiTietGoiMons.Where(c => c.DonGoiMonId == id).Sum(c => (decimal?)(c.SoLuong * c.DonGia)) ?? 0m;
                    db.ThanhToans.Add(new ThanhToan
                    {
                        DonGoiMonId = id,
                        Loai = "QR",
                        SoTien = tong,
                        MaGiaoDich = "MOCK" + Security.RandomCode(10),
                        PhuongThuc = "GiaLap",
                        TrangThai = "ThanhCong",
                        Ngay = DateTime.Now
                    });
                    TempData["Ok"] = "Thanh toán thành công. Đơn của bạn đang chờ nhân viên xác nhận.";
                }
                else
                {
                    don.TrangThai = "Huy";
                    TempData["Err"] = "Thanh toán không thành công, đơn đã được hủy.";
                }
                db.SaveChanges();
            }
            Session.Remove("PayingQR");
            return RedirectToAction("Table", new { token });
        }
    }
}