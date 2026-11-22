using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Mvc;
using DatBanQuanAn.Helpers;
using DatBanQuanAn.Models;

namespace DatBanQuanAn.Controllers
{
    public class AccountController : Controller
    {
        static readonly Regex PhoneRx = new Regex(@"^0\d{9,10}$");
        static readonly Regex MailRx = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");

        DatBanQuanAnEntities db = new DatBanQuanAnEntities();

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }

        public ActionResult Login(string returnUrl) { ViewBag.ReturnUrl = returnUrl; return View(); }

        [HttpPost]
        public ActionResult Login(string user, string password, string returnUrl)
        {
            ViewBag.ReturnUrl = returnUrl;
            if (string.IsNullOrWhiteSpace(user) || string.IsNullOrEmpty(password))
            { ViewBag.Error = "Vui lòng nhập đầy đủ SĐT/Email và mật khẩu."; return View(); }

            user = user.Trim();
            var u = db.TaiKhoans.FirstOrDefault(t => t.SDT == user || t.Email == user || t.TenDangNhap == user);
            if (u == null) { ViewBag.Error = "SĐT/Email hoặc mật khẩu không đúng."; return View(); }
            if (!u.TrangThai) { ViewBag.Error = "Tài khoản đang bị khóa. Vui lòng liên hệ Quản lý."; return View(); }
            if (u.KhoaDen != null && u.KhoaDen > DateTime.Now)
            { ViewBag.Error = "Bạn nhập sai quá 5 lần. Đăng nhập bị khóa tạm 15 phút."; return View(); }

            if (!Security.Verify(password, u.Salt, u.MatKhauHash))
            {
                u.SoLanSai++;
                if (u.SoLanSai >= 5) { u.SoLanSai = 0; u.KhoaDen = DateTime.Now.AddMinutes(15); }
                db.SaveChanges();
                ViewBag.Error = "SĐT/Email hoặc mật khẩu không đúng.";
                return View();
            }

            u.SoLanSai = 0;
            u.KhoaDen = null;
            db.SaveChanges();

            Session["UserId"] = u.Id;
            Session["Role"] = u.VaiTro;
            Session["Name"] = u.HoTen;

            if (u.BatDoiMatKhau)
            { TempData["Err"] = "Bạn đang dùng mật khẩu tạm. Vui lòng đổi mật khẩu mới."; return RedirectToAction("Profile"); }
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) return Redirect(returnUrl);
            switch (u.VaiTro)
            {
                case "QuanLy": return RedirectToAction("Index", "Admin");
                case "PhucVu": return RedirectToAction("Tables", "Staff");
                case "Bep": return RedirectToAction("Index", "Kitchen");
                default: return RedirectToAction("Index", "Home");
            }
        }

        [HttpPost]
        public ActionResult Logout() { Session.Clear(); Session.Abandon(); return RedirectToAction("Index", "Home"); }

        public ActionResult Register() { return View(); }

        [HttpPost]
        public ActionResult Register(string hoTen, string sdt, string email, string matKhau, string xacNhan)
        {
            ViewBag.HoTen = hoTen; ViewBag.Sdt = sdt; ViewBag.Email = email;
            hoTen = (hoTen ?? "").Trim(); sdt = (sdt ?? "").Trim(); email = (email ?? "").Trim();
            if (hoTen == "" || sdt == "" || email == "" || string.IsNullOrEmpty(matKhau))
            { ViewBag.Error = "Vui lòng nhập đủ Họ tên, SĐT, Email, Mật khẩu."; return View(); }
            if (!PhoneRx.IsMatch(sdt) || !MailRx.IsMatch(email)) { ViewBag.Error = "SĐT hoặc Email không đúng định dạng."; return View(); }
            if (matKhau.Length < 8) { ViewBag.Error = "Mật khẩu phải có ít nhất 8 ký tự."; return View(); }
            if (matKhau != xacNhan) { ViewBag.Error = "Mật khẩu xác nhận không khớp."; return View(); }
            if (db.TaiKhoans.Any(t => t.SDT == sdt || t.Email == email))
            { ViewBag.Error = "SĐT hoặc Email đã tồn tại. Hãy đăng nhập."; return View(); }

            var salt = Security.NewSalt();
            var tk = new TaiKhoan
            {
                HoTen = hoTen,
                SDT = sdt,
                Email = email,
                MatKhauHash = Security.Hash(matKhau, salt),
                Salt = salt,
                VaiTro = "KhachThanhVien",
                TrangThai = true,
                DiemTichLuy = 0,
                SoLanNoShow = 0,
                SoLanSai = 0,
                BatDoiMatKhau = false,
                NgayTao = DateTime.Now
            };
            db.TaiKhoans.Add(tk);
            db.SaveChanges();

            foreach (var d in db.DonDatBans.Where(x => x.SDT == sdt && x.KhachId == null).ToList())
                d.KhachId = tk.Id;
            db.SaveChanges();

            TempData["Ok"] = "Đăng ký thành công. Mời bạn đăng nhập.";
            return RedirectToAction("Login");
        }
        public ActionResult ChangePass() { return View(); }

        [HttpPost]
        public ActionResult ChangePass(string user, string cu, string moi, string xacNhan)
        {
            ViewBag.User = user;
            if (string.IsNullOrWhiteSpace(user) || string.IsNullOrEmpty(cu) || string.IsNullOrEmpty(moi))
            { ViewBag.Error = "Vui lòng nhập đầy đủ thông tin."; return View(); }
            if (moi.Length < 8) { ViewBag.Error = "Mật khẩu mới phải có ít nhất 8 ký tự."; return View(); }
            if (moi != xacNhan) { ViewBag.Error = "Mật khẩu xác nhận không khớp."; return View(); }
            if (moi == cu) { ViewBag.Error = "Mật khẩu mới phải khác mật khẩu cũ."; return View(); }

            user = user.Trim();
            var u = db.TaiKhoans.FirstOrDefault(t => t.SDT == user || t.Email == user || t.TenDangNhap == user);
            if (u == null) { ViewBag.Error = "Tài khoản hoặc mật khẩu cũ không đúng."; return View(); }
            if (!u.TrangThai) { ViewBag.Error = "Tài khoản đang bị khóa. Vui lòng liên hệ Quản lý."; return View(); }
            if (u.KhoaDen != null && u.KhoaDen > DateTime.Now)
            { ViewBag.Error = "Bạn nhập sai quá 5 lần. Tạm khóa 15 phút."; return View(); }

            if (!Security.Verify(cu, u.Salt, u.MatKhauHash))
            {
                u.SoLanSai++;
                if (u.SoLanSai >= 5) { u.SoLanSai = 0; u.KhoaDen = DateTime.Now.AddMinutes(15); }
                db.SaveChanges();
                ViewBag.Error = "Tài khoản hoặc mật khẩu cũ không đúng.";
                return View();
            }

            var salt = Security.NewSalt();
            u.MatKhauHash = Security.Hash(moi, salt);
            u.Salt = salt;
            u.BatDoiMatKhau = false;
            u.SoLanSai = 0; u.KhoaDen = null;
            db.SaveChanges();

            TempData["Ok"] = "Đã đổi mật khẩu. Mời bạn đăng nhập bằng mật khẩu mới.";
            return RedirectToAction("Login");
        }

        [Role("KhachThanhVien", "PhucVu", "Bep", "QuanLy")]
        public ActionResult Profile()
        {
            int uid = Convert.ToInt32(Cur.UserId);
            ViewBag.U = db.TaiKhoans.Find(uid);
            return View();
        }

        [HttpPost, Role("KhachThanhVien", "PhucVu", "Bep", "QuanLy")]
        public ActionResult Profile(string hoTen, string email)
        {
            hoTen = (hoTen ?? "").Trim(); email = (email ?? "").Trim();
            int uid = Convert.ToInt32(Cur.UserId);
            if (hoTen == "" || (email != "" && !MailRx.IsMatch(email)))
                TempData["Err"] = "Họ tên bắt buộc, Email phải đúng định dạng.";
            else if (email != "" && db.TaiKhoans.Any(t => t.Email == email && t.Id != uid))
                TempData["Err"] = "Email đã được tài khoản khác sử dụng.";
            else
            {
                var u = db.TaiKhoans.Find(uid);
                u.HoTen = hoTen;
                u.Email = email == "" ? null : email;
                db.SaveChanges();
                Session["Name"] = hoTen;
                TempData["Ok"] = "Đã cập nhật thông tin.";
            }
            return RedirectToAction("Profile");
        }

        [HttpPost, Role("KhachThanhVien", "PhucVu", "Bep", "QuanLy")]
        public ActionResult ChangePassword(string cu, string moi, string xacNhan)
        {
            int uid = Convert.ToInt32(Cur.UserId);
            var u = db.TaiKhoans.Find(uid);
            if (string.IsNullOrEmpty(cu) || !Security.Verify(cu, u.Salt, u.MatKhauHash)) TempData["Err"] = "Mật khẩu cũ không đúng.";
            else if (string.IsNullOrEmpty(moi) || moi.Length < 8) TempData["Err"] = "Mật khẩu mới phải có ít nhất 8 ký tự.";
            else if (moi != xacNhan) TempData["Err"] = "Mật khẩu xác nhận không khớp.";
            else
            {
                var salt = Security.NewSalt();
                u.MatKhauHash = Security.Hash(moi, salt);
                u.Salt = salt;
                u.BatDoiMatKhau = false;
                db.SaveChanges();
                TempData["Ok"] = "Đã đổi mật khẩu.";
            }
            return RedirectToAction("Profile");
        }
    }
}