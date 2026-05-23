namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels
{
    public class DataTableRequest
    {
        public int Draw { get; set; }
        public int Start { get; set; }
        public int Length { get; set; }
        public string? SearchValue { get; set; }
        public string? SortColumnName { get; set; }
        public string? SortDirection { get; set; }
    }

    public class Search
    {
        public string Value { get; set; }
        public bool Regex { get; set; }
    }

    public class Order
    {
        public int Column { get; set; }
        public string Dir { get; set; }
    }

    public class Column
    {
        public string Data { get; set; }
        public string Name { get; set; }
        public bool Searchable { get; set; }
        public bool Orderable { get; set; }
        public Search Search { get; set; }
    }

    // Class trả dữ liệu về Frontend
    public class DataTableResponse<T>
    {
        public int draw { get; set; } // Phải viết thường để jQuery DataTables hiểu
        public int recordsTotal { get; set; }
        public int recordsFiltered { get; set; }
        public IEnumerable<T> data { get; set; }
    }
}
