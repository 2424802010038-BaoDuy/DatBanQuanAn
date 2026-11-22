using System;
using System.Linq;
using DatBanQuanAn.Models;

namespace DatBanQuanAn.Helpers
{
    public static class Seed
    {
        public static void Run()
        {
            try
            {
                using (var db = new DatBanQuanAnEntities())
                {
                    if (db.TaiKhoans.Any()) return;
                    Add(db, "Quản lý", "0900000001", "quanly@demo.vn", "quanly", "Admin@123", "QuanLy");
                    Add(db, "Nhân viên phục vụ", "0900000002", null, "phucvu", "Phucvu@123", "PhucVu");
                    Add(db, "Nhân viên bếp", "0900000003", null, "bep", "Bep@12345", "Bep");
                    Add(db, "Khách demo", "0900000004", "khach@demo.vn", null, "Khach@123", "KhachThanhVien");
                    db.SaveChanges();
                }
            }
            catch { /* CSDL chua tao: se bao loi o trang dau tien */ }
        }

        static void Add(DatBanQuanAnEntities db, string ten, string sdt, string email, string user, string pw, string role)
        {
            var salt = Security.NewSalt();
            db.TaiKhoans.Add(new TaiKhoan
            {
                HoTen = ten,
                SDT = sdt,
                Email = email,
                TenDangNhap = user,
                MatKhauHash = Security.Hash(pw, salt),
                Salt = salt,
                VaiTro = role,
                TrangThai = true,
                DiemTichLuy = 0,
                SoLanNoShow = 0,
                SoLanSai = 0,
                BatDoiMatKhau = false,
                NgayTao = DateTime.Now
            });
        }

        static DateTime last = DateTime.MinValue;
        public static void Maintain()
        {
            if ((DateTime.Now - last).TotalSeconds < 30) return;
            last = DateTime.Now;
            try
            {
                using (var db = new DatBanQuanAnEntities())
                {
                    DateTime now = DateTime.Now;
                    int giu = Cfg.Int("GiuBanPhut", 15);
                    DateTime han = now.AddMinutes(-giu);

                    foreach (var d in db.DonDatBans.Where(x => x.TrangThai == "ChoThanhToan" && x.KhoaDen < now).ToList())
                        d.TrangThai = "DaHuy";
                    db.SaveChanges();

                    int lead = Cfg.Int("BepTruocPhut", 20);
                    if (lead > 0)
                    {
                        DateTime toi = now.AddMinutes(lead);
                        foreach (var d in db.DonDatBans.Where(x => x.HinhThuc == "KemMon" && x.TrangThai == "DaThanhToan"
                                                                   && x.NgayGio >= han && x.NgayGio <= toi).ToList())
                            KitchenLink.SendEarly(db, d);
                    }

                    foreach (var d in db.DonDatBans.Where(x => (x.TrangThai == "DaCoc" || x.TrangThai == "DaThanhToan") && x.NgayGio < han).ToList())
                    {
                        d.TrangThai = d.HinhThuc == "KemMon" ? "MangVe" : "NoShow";
                        if (d.KhachId != null)
                        {
                            var tk = db.TaiKhoans.Find(d.KhachId.Value);
                            if (tk != null) tk.SoLanNoShow++;
                        }
                    }
                    db.SaveChanges();

                    int nhac = Cfg.Int("NhacTruocPhut", 60);
                    DateTime toiNhac = now.AddMinutes(nhac);
                    foreach (var d in db.DonDatBans.Where(x => (x.TrangThai == "DaCoc" || x.TrangThai == "DaThanhToan") && x.NgayGio > now && x.NgayGio <= toiNhac).ToList())
                    {
                        string dau = "[NHAC " + d.MaDon + "]";
                        if (db.ThongBaos.Any(t => t.NoiDung.StartsWith(dau))) continue;
                        BookingService.Notify(db, d.SDT, dau + " Nhắc lịch: đơn " + d.MaDon + " lúc " + d.NgayGio.ToString("HH:mm dd/MM") + ", " + d.SoNguoi + " người. Hẹn gặp bạn!");
                    }
                }
            }
            catch { }
        }
    }
}