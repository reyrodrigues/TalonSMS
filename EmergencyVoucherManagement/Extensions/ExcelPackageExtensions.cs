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
        /// <summary>
        /// Simple helper function that centralizes merging data from one source into another object
        /// </summary>
        /// <typeparam name="T">Type inferred from other</typeparam>
        /// <param name="self">JToken with the data to be ovewritten</param>
        /// <param name="other">Object with data</param>
        /// <returns>Other</returns>
        public static void MergeChangesInto<T>(this JToken self, T other)
        {
            var selfObject = self as JObject;
            if (selfObject == null)
                throw new ArgumentNullException("self");

            foreach (var prop in selfObject.Properties())
            {
                var otherPropertyQuery = other.GetType().GetProperties().Where(p => p.Name == prop.Name);
                // Check to see if the destination has the property
                if (otherPropertyQuery.Any())
                {
                    var otherProperty = otherPropertyQuery.First();
                    // If there is a mismatch like this it means that Excel didn't return the right date time
                    if ((otherProperty.PropertyType == typeof(DateTime) ||otherProperty.PropertyType == typeof(DateTime?))
                        && (prop.Value.Type == JTokenType.Float || prop.Value.Type == JTokenType.Integer))
                    {
                        // Convert float to date and continue the loop
                        otherProperty.SetValue(other, DateTime.FromOADate(prop.Value.ToObject<double>()));

                        continue;
                    }

                    // What do I do when this happens?
                    if (otherProperty.PropertyType.IsValueType &&  (prop.Value.Type == JTokenType.Null || String.IsNullOrEmpty(prop.Value.ToString()))) {
                        continue;
                    }

                    // Nulls will be null
                    if (otherProperty.PropertyType != typeof(string) && !otherProperty.PropertyType.IsValueType && 
                        (prop.Value.Type == JTokenType.String || String.IsNullOrEmpty(prop.Value.ToString())))
                    {
                        otherProperty.SetValue(other, null);

                        continue;
                    }

                    // Fill the existing object with the new data
                    otherProperty.SetValue(other, prop.Value.ToObject(otherProperty.PropertyType));
                }
            }
        }

        /// <summary>
        /// Helper method that allows a dataset to be encoded as an excel file with each data table being a separate sheet.
        /// </summary>
        /// <param name="self">The much hated DataSet</param>
        /// <returns>Byte array containing an xlsx file</returns>
        public static byte[] ToExcelSpreadsheet(this DataSet self)
        {
            using (var package = new ExcelPackage())
            {
                var outputStream = new System.IO.MemoryStream();


                foreach (var table in self.Tables.Cast<DataTable>())
                {
                    var sheet = package.Workbook.Worksheets.Add(table.TableName);
                    sheet.LoadFromDataTable(table);
                    sheet.Cells[sheet.Dimension.Address].AutoFilter = true;
                    sheet.Cells[sheet.Dimension.Address].AutoFitColumns();
                }

                package.SaveAs(outputStream);

                return outputStream.ToArray();
            }
        }

        /// <summary>
        /// Helper method that allows a datatable to be exported directly into an excel file
        /// </summary>
        /// <param name="self">Good old System.Data.DataTable</param>
        /// <returns>Byte array containing an xlsx file</returns>
        public static byte[] ToExcelSpreadsheet(this DataTable self)
        {
            using (var package = new ExcelPackage())
            {
                var outputStream = new System.IO.MemoryStream();

                var sheet = package.Workbook.Worksheets.Add(self.TableName);

                sheet.LoadFromDataTable(self);
                sheet.Cells[sheet.Dimension.Address].AutoFilter = true;
                sheet.Cells[sheet.Dimension.Address].AutoFitColumns();

                package.SaveAs(outputStream);

                return outputStream.ToArray();
            }
        }

        /// <summary>
        /// Wrapper for the native LoadFromDataTable but with formating for DateTime columns
        /// </summary>
        /// <param name="self">Sheet that will receive the data</param>
        /// <param name="dataTable">DataTable with the information to be exported</param>
        /// <param name="printColumns">Whether the first row will have columnNames</param>
        /// <param name="address">Location of the data points</param>
        public static void LoadFromDataTable(this ExcelWorksheet self, DataTable dataTable, bool printColumns = true, string address = "A1")
        {
            self.Cells[address].LoadFromDataTable(dataTable, printColumns);

            var dateColumns = from DataColumn d in dataTable.Columns
                              where d.DataType == typeof(DateTime) || d.ColumnName.Contains("Date")
                              select d.Ordinal + 1;

            dateColumns.ToList().ForEach(dc => self.Cells[2, dc, self.Dimension.End.Row, dc].Style.Numberformat.Format = "yyyy-dd-mm");
        }


        /// <summary>
        /// Function that takes in an excel file with sheets formatted like and csv file and returns a dictionary of sheet names and sheet data
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        public static Dictionary<string, IEnumerable<JObject>> ExtractData(this ExcelPackage self)
        {
            var returnDictionary = new Dictionary<string, IEnumerable<JObject>>();
            foreach (var sheet in self.Workbook.Worksheets)
            {
                returnDictionary[sheet.Name] = sheet.SheetToJson();
            }

            return returnDictionary;
        }

        /// <summary>
        /// Function that takes in a csv-like excel package and transforms it into an JArray of JObjects
        /// </summary>
        /// <param name="self">Worksheet</param>
        /// <returns>JArray of JObjects</returns>
        public static IEnumerable<JObject> SheetToJson(this ExcelWorksheet self)
        {
            var returnArray = new List<JObject>();
            var columns = Enumerable.Range(1, self.Dimension.End.Column)
                .Select(i => (self.Cells[1, i].Value ?? "").ToString())
                .ToArray();

            for (int i = 2; i <= self.Dimension.End.Row; i++)
            {
                var jObject = new JObject();

                columns.Select((c, z) => new { Index = z, ColumnName = c })
                    .ToList()
                    .ForEach(o => jObject.Add(o.ColumnName, JToken.FromObject(self.Cells[i, o.Index + 1].Value ?? "")));

                jObject["__RowNumber"] = i;

                returnArray.Add(jObject);
            }

            return returnArray;
        }
    }
}