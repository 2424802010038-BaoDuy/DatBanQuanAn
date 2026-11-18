using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using DatBanQuanAn.Models;

namespace DatBanQuanAn.Helpers
{
    public class TableInfo
    {
        public int Id { get; set; }
        public string Ten { get; set; }
        public int SucChua { get; set; }
        public string Loai { get; set; }
        public bool Trong { get; set; }
    }

    public static class BookingService
    {
        static readonly string[] Active = { "ChoThanhToan", "DaCoc", "DaThanhToan", "DaCheckIn" };
        const int SlotMinutes = 120;

        const string ConflictSql =
            @"SELECT COUNT(*) FROM DonDatBan WITH (UPDLOCK, HOLDLOCK)
              WHERE BanId=@p0 AND TrangThai IN ('ChoThanhToan','DaCoc','DaThanhToan','DaCheckIn')
              AND (TrangThai<>'ChoThanhToan' OR KhoaDen>GETDATE())
              AND ABS(DATEDIFF(MINUTE,NgayGio,@p1))<120 AND Id<>@p2";

        public static List<TableInfo> Tables(DatBanQuanAnEntities db, DateTime dt, int soNguoi, int khuVucId)
        {
            DateTime now = DateTime.Now;
            DateTime tu = dt.AddMinutes(-SlotMinutes);
            DateTime den = dt.AddMinutes(SlotMinutes);
            var act = Active;

            var busy = new HashSet<int>(db.DonDatBans
                .Where(d => act.Contains(d.TrangThai)
                            && (d.TrangThai != "ChoThanhToan" || d.KhoaDen > now)
                            && d.NgayGio > tu && d.NgayGio < den)
                .Select(d => d.BanId).Distinct().ToList());

            var rows = (from b in db.Bans
                        join lb in db.LoaiBans on b.LoaiBanId equals lb.Id
                        where !b.Xoa && b.KhuVucId == khuVucId
                        orderby b.Ten
                        select new { b.Id, b.Ten, b.SucChua, Loai = lb.Ten, b.TrangThai }).ToList();

            bool ganBayGio = Math.Abs((dt - now).TotalMinutes) < SlotMinutes;
            var result = new List<TableInfo>();
            foreach (var r in rows)
            {
                bool ok = r.TrangThai != "TamNgung"
                          && r.SucChua >= soNguoi
                          && !(r.TrangThai == "DangCoKhach" && ganBayGio)
                          && !busy.Contains(r.Id);
                result.Add(new TableInfo { Id = r.Id, Ten = r.Ten, SucChua = r.SucChua, Loai = r.Loai, Trong = ok });
            }
            return result;
        }

        public static bool IsConflict(DatBanQuanAnEntities db, int banId, DateTime dt, int excludeId)
        {
            return db.Database.SqlQuery<int>(ConflictSql, banId, dt, excludeId).First() > 0;
        }

        public static bool TryCreate(DatBanQuanAnEntities db, int banId, DateTime dt, int soNguoi, int? khachId, string sdt, string hoTen,
            string ghiChu, string hinhThuc, decimal coc, List<int[]> mon, string trangThai,
            out int id, out string ma, out string err)
        {
            id = 0; ma = null; err = null;
            int lockMin = Cfg.Int("KhoaBanPhut", 5);
            using (var tx = db.Database.BeginTransaction(IsolationLevel.Serializable))
            {
                try
                {
                    var ban = db.Bans.Where(x => x.Id == banId && !x.Xoa).Select(x => new { x.SucChua, x.TrangThai }).FirstOrDefault();
                    if (ban == null) { err = "Bàn không tồn tại."; tx.Rollback(); return false; }
                    if (ban.TrangThai == "TamNgung") { err = "Bàn đang tạm ngưng phục vụ."; tx.Rollback(); return false; }
                    if (ban.SucChua < soNguoi) { err = "Bàn không đủ sức chứa."; tx.Rollback(); return false; }
                    if (IsConflict(db, banId, dt, 0)) { err = "Bàn vừa có người khác đặt trong khung giờ này. Vui lòng chọn bàn khác."; tx.Rollback(); return false; }

                    decimal tienMon = 0;
                    var lines = new List<DatMonTruoc>();
                    foreach (var line in mon)
                    {
                        int monId = line[0];
                        var gia = db.MonAns.Where(x => x.Id == monId && x.ConMon && !x.An)
                                           .Select(x => (decimal?)x.DonGia).FirstOrDefault();
                        if (gia == null) { err = "Có món đã hết hoặc không còn trong thực đơn."; tx.Rollback(); return false; }
                        tienMon += gia.Value * line[1];
                        lines.Add(new DatMonTruoc { MonId = monId, SoLuong = line[1], DonGia = gia.Value });
                    }

                    string code = Security.RandomCode(8);
                    var don = new DonDatBan
                    {
                        MaDon = code,
                        KhachId = khachId,
                        SDT = sdt,
                        HoTen = hoTen,
                        BanId = banId,
                        NgayGio = dt,
                        SoNguoi = soNguoi,
                        GhiChu = ghiChu,
                        HinhThuc = hinhThuc,
                        TienCoc = coc,
                        TienMon = tienMon,
                        TrangThai = trangThai,
                        KhoaDen = trangThai == "ChoThanhToan" ? DateTime.Now.AddMinutes(lockMin) : (DateTime?)null,
                        NgayTao = DateTime.Now
                    };
                    db.DonDatBans.Add(don);
                    db.SaveChanges();

                    foreach (var l in lines) { l.DonDatBanId = don.Id; db.DatMonTruocs.Add(l); }
                    db.SaveChanges();
                    tx.Commit();
                    id = don.Id; ma = code;
                    return true;
                }
                catch (Exception ex) { try { tx.Rollback(); } catch { } err = "Lỗi hệ thống: " + ex.Message; return false; }
            }
        }

        public static void Notify(DatBanQuanAnEntities db, string sdt, string noiDung)
        {
            string email = db.TaiKhoans.Where(x => x.SDT == sdt && x.Email != null).Select(x => x.Email).FirstOrDefault();
            bool daGuiMail = email != null && Mailer.SendAsync(email, "Quán Ăn – Thông báo đặt bàn", noiDung);

            db.ThongBaos.Add(new ThongBao
            {
                SDT = sdt,
                Kenh = daGuiMail ? "Email" : "SMS",
                NoiDung = noiDung,
                TrangThai = "DaGui",
                Ngay = DateTime.Now
            });
            db.SaveChanges();
        }
    }
}