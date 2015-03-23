using Newtonsoft.Json.Linq;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Web;

namespace EmergencyVoucherManagement.Extensions
{
    public static class ExcelPackageExtensions
    {
        public static ExcelWorksheet NewSheet(this ExcelPackage package, string sheetName)
        {
            return package.Workbook.Worksheets.Add(sheetName);
        }

        public static void LoadJson(this ExcelWorksheet sheet, string address, JArray json)
        {
            var dataTable = json.ToObject<DataTable>();

            sheet.Cells[address].LoadFromDataTable(dataTable, true);
        }
        public static JArray ExtractJson(this ExcelWorksheet sheet)
        {
            return JToken.FromObject(new object[0]) as JArray;
        }
    }
}