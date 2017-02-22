using Microsoft.Azure.Search;
using Microsoft.Azure.Search.Models;
using System;
using System.ComponentModel.DataAnnotations;

namespace ChipsnCookies.SearchIndexer
{
    [SerializePropertyNamesAsCamelCase]
    public class Document
    {
        [IsSearchable, IsRetrievable(true), Scoring(1)]
        public string Content { get; set; }

        [IsFilterable, IsSortable, IsFacetable, IsRetrievable(true)]
        public DateTimeOffset Date { get; set; }

        [IsFilterable, IsSortable, IsSearchable, IsRetrievable(true), Scoring(8)]
        public string Section { get; set; }

        [IsFilterable, IsFacetable, IsSearchable, IsRetrievable(true), Scoring(9)]
        public string[] Tags { get; set; }

        [IsSortable, IsSearchable, IsRetrievable(true), Scoring(10)]
        public string Title { get; set; }
        
        [Key]
        public string Uid { get; set; }

        [IsRetrievable(true)]
        public string Url { get; set; }

        public override string ToString() => Title;
    }
}
