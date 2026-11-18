namespace DatBanQuanAn.Helpers
{
    public class PostBtn
    {
        public string Action, Controller, Label, Css, Confirm;
        public object Id;
        public PostBtn(string action, string controller, object id, string label, string css = "btn-default", string confirm = null)
        { Action = action; Controller = controller; Id = id; Label = label; Css = css; Confirm = confirm; }
    }
}
