using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc.Rendering;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;

namespace QuanLyChoThuePhongTroWeb.Web.Mapping
{
    public static class SelectOptionMappingExtensions
    {
        public static SelectListItem ToSelectListItem(this SelectOptionDto dto)
        {
            return new SelectListItem
            {
                Value = dto.Value,
                Text = dto.Text
            };
        }

        public static List<SelectListItem> ToSelectListItems(this IEnumerable<SelectOptionDto> dtoList)
        {
            return dtoList.Select(ToSelectListItem).ToList();
        }
    }
}
