using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace DatBanQuanAn.Helpers
{
    public static class Mailer
    {
        const string fromEmail = "2424802010038@student.tdmu.edu.vn";
        const string fromPassword = "123456789012";
        const string tenQuan = "Quán Ăn";

        public static string LastError { get; private set; }

        public static bool Enabled
        {
            get
            {
                return !string.IsNullOrWhiteSpace(fromEmail) && !fromEmail.StartsWith("email-gmail")
                    && !string.IsNullOrWhiteSpace(fromPassword) && fromPassword != "abcdefghijklmnop";
            }
        }

        public static bool SendAsync(string to, string subject, string body)
        {
            if (!Enabled || string.IsNullOrWhiteSpace(to)) return false;

            Task.Run(() =>
            {
                try
                {
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                    string html = "<div style=\"font-family:Arial,sans-serif;font-size:14px;line-height:1.5\">"
                                + System.Net.WebUtility.HtmlEncode(body ?? "").Replace("\n", "<br/>")
                                + "<hr/><small style=\"color:#888\">" + tenQuan + "</small></div>";

                    using (var smtp = new SmtpClient("smtp.gmail.com", 587))
                    {
                        smtp.EnableSsl = true;
                        smtp.DeliveryMethod = SmtpDeliveryMethod.Network;
                        smtp.UseDefaultCredentials = false;
                        smtp.Credentials = new NetworkCredential(fromEmail, fromPassword);

                        using (var msg = new MailMessage())
                        {
                            msg.From = new MailAddress(fromEmail, tenQuan, System.Text.Encoding.UTF8);
                            msg.To.Add(to.Trim());
                            msg.Subject = subject;
                            msg.SubjectEncoding = System.Text.Encoding.UTF8;
                            msg.Body = html;
                            msg.BodyEncoding = System.Text.Encoding.UTF8;
                            msg.IsBodyHtml = true;
                            smtp.Send(msg);
                        }
                    }
                    LastError = null;
                }
                catch (Exception ex)
                {
                    LastError = ex.ToString();
                    System.Diagnostics.Debug.WriteLine("MAIL LOI: " + ex);
                }
            });
            return true;
        }
    }
}