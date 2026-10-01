namespace QuanLyChoThuePhongTroWeb.Models.ModelsOther
{
    public class MenuItem
    {
        public string Name { get; set; }
        public string Icon { get; set; }
        public string Url { get; set; }
        public List<MenuItem> Children { get; set; } = new List<MenuItem>();
        public List<string>? Roles { get; set; }

        public bool IsVisibleFor(System.Security.Claims.ClaimsPrincipal user) => Roles == null || Roles.Any(user.IsInRole);
    }
}
