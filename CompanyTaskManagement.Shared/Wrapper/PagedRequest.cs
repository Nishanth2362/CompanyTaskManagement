namespace CompanyTaskManagement.Shared.Wrapper
{
    public class PagedRequest
    {
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string Search { get; set; } = string.Empty;
        public string[]? OrderBy { get; set; }
        public bool IsDeleted { get; set; } = false;
    }
}
