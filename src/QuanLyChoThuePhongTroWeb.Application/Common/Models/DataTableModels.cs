namespace QuanLyChoThuePhongTroWeb.Application.Common.Models
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

    public class DataTableSearch
    {
        public string Value { get; set; } = string.Empty;
        public bool Regex { get; set; }
    }

    public class DataTableOrder
    {
        public int Column { get; set; }
        public string Dir { get; set; } = string.Empty;
    }

    public class DataTableColumn
    {
        public string Data { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool Searchable { get; set; }
        public bool Orderable { get; set; }
        public DataTableSearch? Search { get; set; }
    }

    public class DataTableResponse<T>
    {
        public int draw { get; set; }
        public int recordsTotal { get; set; }
        public int recordsFiltered { get; set; }
        public IEnumerable<T> data { get; set; } = Enumerable.Empty<T>();
    }
}
