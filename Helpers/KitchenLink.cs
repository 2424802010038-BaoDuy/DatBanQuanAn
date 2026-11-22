using System;
using System.Linq;
using DatBanQuanAn.Models;

namespace DatBanQuanAn.Helpers
{
    public static class KitchenLink
    {
        static DonGoiMon Create(DatBanQuanAnEntities db, DonDatBan d, bool gan)
        {
            var goi = new DonGoiMon
            {
                BanId = d.BanId,
                HinhThucTT = "Online",
                DaThanhToan = true,
                TrangThai = "DaXacNhan",
                DaHoaDon = !gan,
                NgayTao = DateTime.Now,
                DonDatBanId = d.Id
            };
            db.DonGoiMons.Add(goi);
            db.SaveChanges();
            foreach (var m in db.DatMonTruocs.Where(x => x.DonDatBanId == d.Id).ToList())
                db.ChiTietGoiMons.Add(new ChiTietGoiMon { DonGoiMonId = goi.Id, MonId = m.MonId, SoLuong = m.SoLuong, DonGia = m.DonGia, TrangThai = "ChoCheBien" });
            db.SaveChanges();
            return goi;
        }

        public static void SendEarly(DatBanQuanAnEntities db, DonDatBan d)
        {
            if (d.HinhThuc != "KemMon") return;
            if (db.DonGoiMons.Any(g => g.DonDatBanId == d.Id)) return;
            Create(db, d, false);
        }

        public static void AttachAtCheckIn(DatBanQuanAnEntities db, DonDatBan d)
        {
            var g = db.DonGoiMons.FirstOrDefault(x => x.DonDatBanId == d.Id);
            if (g == null) Create(db, d, true);
            else { g.BanId = d.BanId; g.DaHoaDon = false; }
        }

        public static bool CancelPending(DatBanQuanAnEntities db, int bookingId)
        {
            bool daLam = false;
            foreach (var g in db.DonGoiMons.Where(x => x.DonDatBanId == bookingId && x.HoaDonId == null).ToList())
            {
                foreach (var c in db.ChiTietGoiMons.Where(x => x.DonGoiMonId == g.Id).ToList())
                {
                    if (c.TrangThai == "ChoCheBien") c.TrangThai = "Huy";
                    else if (c.TrangThai != "Huy") daLam = true;
                }
                g.DaHoaDon = true;
            }
            return daLam;
        }
    }
}