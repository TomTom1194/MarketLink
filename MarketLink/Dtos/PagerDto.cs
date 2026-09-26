namespace MarketLink.Dtos
{
    public class PagerDto
    {
        public int Page { get; set; } = 1;

        public int PageSize { get; set; }

        public int TotalCount { get; set; }

        public int TotalPages { get; set; }

        public string PageParam { get; set; } = "page";

        public string Anchor { get; set; } = "";

        public static PagerDto Create(int page, int totalCount, int pageSize, string pageParam, string anchor)
        {
            int totalPages = (totalCount + pageSize - 1) / pageSize;
            if (page > totalPages)
            {
                page = totalPages;
            }
            if (page < 1)
            {
                page = 1;
            }

            return new PagerDto
            {
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalPages,
                PageParam = pageParam,
                Anchor = anchor
            };
        }

        public int Skip()
        {
            return (Page - 1) * PageSize;
        }
    }
}
