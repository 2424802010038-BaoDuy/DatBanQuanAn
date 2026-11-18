using System;
using System.Configuration;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace DatBanQuanAn.Helpers
{
    public static class Mailer
    {
        static string Cfg(string k) { return ConfigurationManager.AppSettings[k]; }

        public static bool Enabled { get { return !string.IsNullOrEmpty(Cfg("Mail:User")); } }

        public static bool SendAsync(string to, string subject, string body)
        {
            if (!Enabled || string.IsNullOrWhiteSpace(to)) return false;
            string host = Cfg("Mail:Host"), user = Cfg("Mail:User"), pass = Cfg("Mail:Pass"), from = Cfg("Mail:From") ?? user;
            int port; if (!int.TryParse(Cfg("Mail:Port"), out port)) port = 587;

            Task.Run(() =>
            {
                try
                {
                    using (var smtp = new SmtpClient(host, port) { EnableSsl = true, Credentials = new NetworkCredential(user, pass) })
                    using (var msg = new MailMessage())
                    {
                        msg.From = new MailAddress(from);
                        msg.To.Add(to);
                        msg.Subject = subject;
                        msg.Body = body;
                        msg.IsBodyHtml = false;
                        smtp.Send(msg);
                    }
                }
                catch { /* gửi lỗi không được làm hỏng nghiệp vụ; log đã nằm ở bảng ThongBao */ }
            });
            return true;
        }
    }
}