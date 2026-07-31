using ClosedXML.Excel;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace TalonAdmin.Extensions
{
    /// <summary>
    /// Spreadsheet import and export.
    /// </summary>
    /// <remarks>
    /// Built on ClosedXML. It was EPPlus, which since version 5 requires a licence declaration
    /// before it will open a package — so every deployment inherited a non-commercial declaration
    /// naming an organisation that was almost certainly not theirs. ClosedXML is MIT, which removes
    /// the obligation rather than passing it on.
    /// </remarks>
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

                    // A date arriving as a number. ClosedXML hands back a typed DateTime for a
                    // date-formatted cell, so this is no longer the normal path the way it was
                    // under EPPlus — but a column formatted as plain number still carries a serial
                    // date, and that is what this catches.
                    if ((otherProperty.PropertyType == typeof(DateTime) || otherProperty.PropertyType == typeof(DateTime?))
                        && (prop.Value.Type == JTokenType.Float || prop.Value.Type == JTokenType.Integer))
                    {
                        // Convert float to date and continue the loop
                        var oaDate = DateTime.FromOADate(prop.Value.ToObject<double>());
                        var date = DateTime.SpecifyKind(oaDate, DateTimeKind.Utc);

                        if (oaDate.Hour + oaDate.Minute + oaDate.Second + oaDate.Millisecond == 0) // This is a date, not a datetime
                        {
                            date = new DateTime(oaDate.Year, oaDate.Month, oaDate.Day, 0, 0, 0, DateTimeKind.Utc);
                        }

                        otherProperty.SetValue(other, date);

                        continue;
                    }

                    // A date that arrived as one. Kind is unspecified coming out of a spreadsheet,
                    // and the rest of the model treats its dates as UTC.
                    if ((otherProperty.PropertyType == typeof(DateTime) || otherProperty.PropertyType == typeof(DateTime?))
                        && prop.Value.Type == JTokenType.Date)
                    {
                        var value = prop.Value.ToObject<DateTime>();

                        otherProperty.SetValue(other, DateTime.SpecifyKind(value, DateTimeKind.Utc));

                        continue;
                    }

                    // What do I do when this happens?
                    if (otherProperty.PropertyType.IsValueType && (prop.Value.Type == JTokenType.Null || String.IsNullOrEmpty(prop.Value.ToString())))
                    {
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
            using (var workbook = new XLWorkbook())
            {
                foreach (var table in self.Tables.Cast<DataTable>())
                {
                    workbook.Worksheets.Add(SheetName(table.TableName)).LoadFromDataTable(table);
                }

                // A workbook with no sheets is not a valid xlsx and ClosedXML refuses to save one.
                // An empty DataSet is an ordinary outcome here — a deployment whose metadata
                // collections are all empty — so it gets an empty sheet rather than an exception.
                if (!workbook.Worksheets.Any())
                {
                    workbook.Worksheets.Add("Export");
                }

                return Save(workbook);
            }
        }

        /// <summary>
        /// Helper method that allows a datatable to be exported directly into an excel file
        /// </summary>
        /// <param name="self">Good old System.Data.DataTable</param>
        /// <returns>Byte array containing an xlsx file</returns>
        public static byte[] ToExcelSpreadsheet(this DataTable self)
        {
            using (var workbook = new XLWorkbook())
            {
                if (String.IsNullOrEmpty(self.TableName)) {
                    self.TableName = "Export";
                }

                workbook.Worksheets.Add(SheetName(self.TableName)).LoadFromDataTable(self);

                return Save(workbook);
            }
        }

        /// <summary>
        /// Writes a data table as a header row plus its rows, with dates formatted and columns sized
        /// to their contents.
        /// </summary>
        /// <param name="self">Sheet that will receive the data</param>
        /// <param name="dataTable">DataTable with the information to be exported</param>
        /// <param name="printColumns">Whether the first row will have columnNames</param>
        /// <param name="address">Location of the data points</param>
        public static void LoadFromDataTable(this IXLWorksheet self, DataTable dataTable, bool printColumns = true, string address = "A1")
        {
            // A table is column-less whenever it came from an empty JSON array, which happens for
            // any metadata collection that has no rows yet. Nothing to write, and asking for a
            // range on an empty sheet below would throw.
            if (dataTable.Columns.Count == 0)
            {
                return;
            }

            var origin = self.Cell(address);
            var firstRow = origin.Address.RowNumber;
            var firstColumn = origin.Address.ColumnNumber;

            if (printColumns)
            {
                for (var i = 0; i < dataTable.Columns.Count; i++)
                {
                    self.Cell(firstRow, firstColumn + i).Value = dataTable.Columns[i].ColumnName;
                }
            }

            var firstDataRow = printColumns ? firstRow + 1 : firstRow;

            for (var r = 0; r < dataTable.Rows.Count; r++)
            {
                for (var c = 0; c < dataTable.Columns.Count; c++)
                {
                    self.Cell(firstDataRow + r, firstColumn + c).Value = ToCellValue(dataTable.Rows[r][c]);
                }
            }

            var dateColumns = from DataColumn d in dataTable.Columns
                              where d.DataType == typeof(DateTime) || d.ColumnName.EndsWith("Date")
                              select firstColumn + d.Ordinal;

            if (dataTable.Rows.Count > 0)
            {
                var lastRow = firstDataRow + dataTable.Rows.Count - 1;

                // NOTE: this was "yyyy-dd-mm", which renders the 31st of July as 2026-31-07. The
                // stored value was always correct — only the display was — so nothing that reads
                // these files needs to change.
                dateColumns.ToList().ForEach(dc =>
                    self.Range(firstDataRow, dc, lastRow, dc).Style.DateFormat.Format = "yyyy-mm-dd");
            }

            var used = self.RangeUsed();
            if (used != null)
            {
                used.SetAutoFilter();
                self.Columns().AdjustToContents();
            }
        }

        /// <summary>
        /// Function that takes in an excel file with sheets formatted like and csv file and returns a dictionary of sheet names and sheet data
        /// </summary>
        public static Dictionary<string, IEnumerable<JObject>> ExtractData(this XLWorkbook self)
        {
            var returnDictionary = new Dictionary<string, IEnumerable<JObject>>();
            foreach (var sheet in self.Worksheets)
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
        public static IEnumerable<JObject> SheetToJson(this IXLWorksheet self)
        {
            var returnArray = new List<JObject>();

            // An untouched sheet has no used range at all; the old code would have thrown
            // dereferencing its dimension.
            var used = self.RangeUsed();
            if (used == null)
            {
                return returnArray;
            }

            var lastRow = used.LastRow().RowNumber();
            var lastColumn = used.LastColumn().ColumnNumber();

            var columns = Enumerable.Range(1, lastColumn)
                .Select(i => self.Cell(1, i).GetString() ?? "")
                .ToArray();

            for (int i = 2; i <= lastRow; i++)
            {
                var jObject = new JObject();

                columns.Select((c, z) => new { Index = z, ColumnName = c })
                    .ToList()
                    .Where(o => !String.IsNullOrEmpty(o.ColumnName))
                    .ToList()
                    .ForEach(o => jObject.Add(o.ColumnName, self.Cell(i, o.Index + 1).ToJToken()));

                jObject["__RowNumber"] = i;

                returnArray.Add(jObject);
            }

            return returnArray;
        }

        /// <summary>
        /// A cell as the closest JSON type. Unlike EPPlus, ClosedXML hands back a typed value, so a
        /// date-formatted cell arrives as a DateTime rather than as the serial number underneath it
        /// — which <see cref="MergeChangesInto"/> handles either way.
        /// </summary>
        public static JToken ToJToken(this IXLCell cell)
        {
            var value = cell.Value;

            object returnValue = value.Type switch
            {
                XLDataType.Blank => "",
                XLDataType.Boolean => value.GetBoolean(),
                XLDataType.Number => value.GetNumber(),
                XLDataType.DateTime => value.GetDateTime(),
                XLDataType.TimeSpan => value.GetTimeSpan().ToString(),
                XLDataType.Text => value.GetText(),
                // A formula that evaluated to #REF! or similar. The text of the error is more use
                // downstream than an exception in the middle of an import.
                XLDataType.Error => value.GetError().ToString(),
                _ => cell.GetString()
            };

            return JToken.FromObject(returnValue ?? "");
        }

        /// <summary>
        /// Excel allows 31 characters and forbids <c>[]:*?/\</c> in a sheet name; ClosedXML enforces
        /// both. Table names here are ours rather than a user's, but a rejected name would fail the
        /// whole export rather than one sheet.
        /// </summary>
        private static string SheetName(string name)
        {
            var cleaned = new string((name ?? "").Where(c => !"[]:*?/\\".Contains(c)).ToArray()).Trim();

            if (String.IsNullOrEmpty(cleaned))
            {
                cleaned = "Export";
            }

            return cleaned.Length > 31 ? cleaned.Substring(0, 31) : cleaned;
        }

        private static XLCellValue ToCellValue(object value) => value switch
        {
            null or DBNull => Blank.Value,
            string s => s,
            bool b => b,
            DateTime d => d,
            TimeSpan t => t,
            sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal
                => Convert.ToDouble(value),
            _ => value.ToString()
        };

        private static byte[] Save(XLWorkbook workbook)
        {
            using (var outputStream = new System.IO.MemoryStream())
            {
                workbook.SaveAs(outputStream);

                return outputStream.ToArray();
            }
        }
    }
}
