using System;
using System.Globalization;

namespace DatBanQuanAn.Helpers
{
    public static class Fmt
    {
        static readonly CultureInfo vi = new CultureInfo("vi-VN");
        public static string Money(object o) { return o == null || o == DBNull.Value ? "0đ" : Convert.ToDecimal(o).ToString("N0", vi) + "đ"; }
        public static string Date(object o) { return o == null || o == DBNull.Value ? "" : Convert.ToDateTime(o).ToString("dd/MM/yyyy HH:mm", vi); }
        public static string StatusBooking(string s)
        {
            switch (s)
            {
                case "ChoThanhToan": return "Chờ thanh toán";
                case "DaCoc": return "Đã cọc";
                case "DaThanhToan": return "Đã thanh toán món";
                case "DaCheckIn": return "Đang dùng bữa";
                case "HoanThanh": return "Hoàn thành";
                case "DaHuy": return "Đã hủy";
                case "NoShow": return "No-show (mất cọc)";
                case "MangVe": return "Đóng gói mang về";
                default: return s;
            }
        }
        public static string StatusTable(string s)
        {
            switch (s)
            {
                case "Trong": return "Trống";
                case "DaDat": return "Đã đặt trước";
                case "DangCoKhach": return "Đang có khách";
                case "DangDon": return "Đang dọn dẹp";
                case "TamNgung": return "Tạm ngưng";
                default: return s;
            }
        }
    }
}
