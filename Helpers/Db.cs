using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;

namespace DatBanQuanAn.Helpers
{
    public static class Db
    {
        static string Cs { get { return ConfigurationManager.ConnectionStrings["DB"].ConnectionString; } }
        public static SqlConnection Open() { var cn = new SqlConnection(Cs); cn.Open(); return cn; }

        static SqlCommand Cmd(SqlConnection cn, SqlTransaction tx, string sql, object[] p)
        {
            var cm = new SqlCommand(sql, cn, tx);
            for (int i = 0; i < p.Length; i++) cm.Parameters.AddWithValue("@p" + i, p[i] ?? DBNull.Value);
            return cm;
        }
        public static DataTable Q(string sql, params object[] p)
        {
            using (var cn = Open()) using (var cm = Cmd(cn, null, sql, p))
            { var dt = new DataTable(); new SqlDataAdapter(cm).Fill(dt); return dt; }
        }
        public static DataRow R(string sql, params object[] p) { var t = Q(sql, p); return t.Rows.Count > 0 ? t.Rows[0] : null; }
        public static object S(string sql, params object[] p) { using (var cn = Open()) using (var cm = Cmd(cn, null, sql, p)) return cm.ExecuteScalar(); }
        public static int X(string sql, params object[] p) { using (var cn = Open()) using (var cm = Cmd(cn, null, sql, p)) return cm.ExecuteNonQuery(); }
        public static object S(SqlConnection cn, SqlTransaction tx, string sql, params object[] p) { using (var cm = Cmd(cn, tx, sql, p)) return cm.ExecuteScalar(); }
        public static int X(SqlConnection cn, SqlTransaction tx, string sql, params object[] p) { using (var cm = Cmd(cn, tx, sql, p)) return cm.ExecuteNonQuery(); }
        public static DataTable Q(SqlConnection cn, SqlTransaction tx, string sql, params object[] p)
        { using (var cm = Cmd(cn, tx, sql, p)) { var dt = new DataTable(); new SqlDataAdapter(cm).Fill(dt); return dt; } }
    }
}
