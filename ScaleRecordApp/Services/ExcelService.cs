using ClosedXML.Excel;
using ScaleRecordApp.ViewModels;

namespace ScaleRecordApp.Services;

public class ExcelService
{
    // Создает xlsx в AppDataDirectory и возвращает полный путь
    public string CreateExcelFromHistory(IEnumerable<HistoryRecordDto> records, string fileName)
    {
        string folder = FileSystem.AppDataDirectory;
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, fileName);

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Report");

        // Заголовки
        ws.Cell(1, 1).Value = "Дата";
        ws.Cell(1, 2).Value = "Машина";
        ws.Cell(1, 3).Value = "Груз";
        ws.Cell(1, 4).Value = "Нетто (кг)";
        ws.Cell(1, 5).Value = "Откуда";
        ws.Cell(1, 6).Value = "Куда";
        ws.Cell(1, 7).Value = "Источник";
        ws.Cell(1, 8).Value = "Комментарий";

        // Стиль заголовков
        var headerRange = ws.Range(1, 1, 1, 8);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

        int row = 2;
        foreach (var r in records)
        {
            ws.Cell(row, 1).Value = r.Timestamp;
            ws.Cell(row, 1).Style.DateFormat.Format = "dd.MM.yyyy HH:mm";
            ws.Cell(row, 2).Value = r.VehicleName;
            ws.Cell(row, 3).Value = r.CargoDisplay;
            ws.Cell(row, 4).Value = r.NetWeight;
            ws.Cell(row, 5).Value = r.FromName;
            ws.Cell(row, 6).Value = r.ToName;
            ws.Cell(row, 7).Value = r.SourceName;
            ws.Cell(row, 8).Value = r.Comment;
            row++;
        }

        // ---- Сводка ----
        row += 2; // отступ от основной таблицы

        int summaryStartRow = row;

        // Общая сумма
        double totalNetKg = records.Sum(r => r.NetWeight);
        double totalNetTons = Math.Round(totalNetKg / 1000.0, 3);

        ws.Cell(row, 2).Value = "Общая сумма нетто (т)";
        ws.Cell(row, 2).Style.Font.Bold = true;
        ws.Cell(row, 3).Value = totalNetTons;
        ws.Cell(row, 3).Style.NumberFormat.Format = "0.000";
        row += 2;

        // Суммы по грузу
        foreach (var group in records.GroupBy(r => r.CargoDisplay))
        {
            double sumTons = Math.Round(group.Sum(r => r.NetWeight) / 1000.0, 3);
            ws.Cell(row, 2).Value = $"Сумма по грузу \"{group.Key}\" (т)";
            ws.Cell(row, 2).Style.Font.Bold = true;
            ws.Cell(row, 3).Value = sumTons;
            ws.Cell(row, 3).Style.NumberFormat.Format = "0.000";
            row++;
        }
        row++;

        // Суммы по машинам
        foreach (var group in records.GroupBy(r => r.VehicleName))
        {
            double sumTons = Math.Round(group.Sum(r => r.NetWeight) / 1000.0, 3);
            ws.Cell(row, 2).Value = $"Сумма по машине \"{group.Key}\" (т)";
            ws.Cell(row, 2).Style.Font.Bold = true;
            ws.Cell(row, 3).Value = sumTons;
            ws.Cell(row, 3).Style.NumberFormat.Format = "0.000";
            row++;
        }
        row++;

        // Суммы по направлениям (Откуда)
        foreach (var group in records.GroupBy(r => r.FromName))
        {
            double sumTons = Math.Round(group.Sum(r => r.NetWeight) / 1000.0, 3);
            ws.Cell(row, 2).Value = $"Сумма по месту отправки \"{group.Key}\" (т)";
            ws.Cell(row, 2).Style.Font.Bold = true;
            ws.Cell(row, 3).Value = sumTons;
            ws.Cell(row, 3).Style.NumberFormat.Format = "0.000";
            row++;
        }
        row++;

        // Суммы по направлениям (Куда)
        foreach (var group in records.GroupBy(r => r.ToName))
        {
            double sumTons = Math.Round(group.Sum(r => r.NetWeight) / 1000.0, 3);
            ws.Cell(row, 2).Value = $"Сумма по месту назначения \"{group.Key}\" (т)";
            ws.Cell(row, 2).Style.Font.Bold = true;
            ws.Cell(row, 3).Value = sumTons;
            ws.Cell(row, 3).Style.NumberFormat.Format = "0.000";
            row++;
        }

        // Отформатируем блок итогов
        int summaryEndRow = row - 1;
        var summaryRange = ws.Range(summaryStartRow, 2, summaryEndRow, 3);
        summaryRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        summaryRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        summaryRange.Style.Fill.BackgroundColor = XLColor.LightGray;

        ws.Columns().AdjustToContents();
        wb.SaveAs(path);

        return path;
    }
}

























//public class ExcelService
//{
//    // Создает xlsx в AppDataDirectory и возвращает полный путь
//    public string CreateExcelFromHistory(IEnumerable<HistoryRecordDto> records, string fileName)
//    {
//        string folder = FileSystem.AppDataDirectory;
//        Directory.CreateDirectory(folder);
//        string path = Path.Combine(folder, fileName);

//        using var wb = new XLWorkbook();
//        var ws = wb.Worksheets.Add("Report");

//        // Заголовки
//        ws.Cell(1, 1).Value = "Дата";
//        ws.Cell(1, 2).Value = "Машина";
//        ws.Cell(1, 3).Value = "Груз";
//        ws.Cell(1, 4).Value = "Нетто (кг)";
//        ws.Cell(1, 5).Value = "Откуда";
//        ws.Cell(1, 6).Value = "Куда";
//        ws.Cell(1, 7).Value = "Источник";
//        ws.Cell(1, 8).Value = "Комментарий";

//        int row = 2;
//        foreach (var r in records)
//        {
//            ws.Cell(row, 1).Value = r.Timestamp;
//            ws.Cell(row, 1).Style.DateFormat.Format = "dd.MM.yyyy HH:mm";
//            ws.Cell(row, 2).Value = r.VehicleName;
//            ws.Cell(row, 3).Value = r.CargoDisplay;
//            ws.Cell(row, 4).Value = r.NetWeight;
//            ws.Cell(row, 5).Value = r.FromName;
//            ws.Cell(row, 6).Value = r.ToName;
//            ws.Cell(row, 7).Value = r.SourceName;
//            ws.Cell(row, 8).Value = r.Comment;
//            row++;
//        }

//        // Сумма нетто
//        double totalNetKg = records.Sum(r => r.NetWeight);
//        double totalNetTons = Math.Round(totalNetKg / 1000.0, 3);

//        // Итоговая строка
//        ws.Cell(row, 3).Value = "ИТОГО (тонн)";
//        ws.Cell(row, 4).Value = totalNetTons;
//        ws.Cell(row, 4).Style.NumberFormat.Format = "0.000"; // три знака после запятой

//        ws.Columns().AdjustToContents();

//        wb.SaveAs(path);

//        return path;
//    }
//}







//public class ExcelService
//{
//    // Создает xlsx в AppDataDirectory и возвращает полный путь
//    public string CreateExcelFromHistory(IEnumerable<HistoryRecordDto> records, string fileName)
//    {
//        string folder = FileSystem.AppDataDirectory;
//        Directory.CreateDirectory(folder);
//        string path = Path.Combine(folder, fileName);

//        using var wb = new XLWorkbook();
//        var ws = wb.Worksheets.Add("Report");

//        // Заголовки
//        ws.Cell(1, 1).Value = "Дата";
//        ws.Cell(1, 2).Value = "Машина";
//        ws.Cell(1, 3).Value = "Груз";
//        ws.Cell(1, 4).Value = "Нетто";
//        ws.Cell(1, 5).Value = "Откуда";
//        ws.Cell(1, 6).Value = "Куда";
//        ws.Cell(1, 7).Value = "Источник";
//        ws.Cell(1, 8).Value = "Комментарий";

//        int row = 2;
//        foreach (var r in records)
//        {
//            ws.Cell(row, 1).Value = r.Timestamp;
//            ws.Cell(row, 1).Style.DateFormat.Format = "dd.MM.yyyy HH:mm";
//            ws.Cell(row, 2).Value = r.VehicleName;
//            ws.Cell(row, 3).Value = r.CargoDisplay;
//            ws.Cell(row, 4).Value = r.NetWeight;
//            ws.Cell(row, 5).Value = r.FromName;
//            ws.Cell(row, 6).Value = r.ToName;
//            ws.Cell(row, 7).Value = r.SourceName;
//            ws.Cell(row, 8).Value = r.Comment;
//            row++;
//        }

//        ws.Columns().AdjustToContents();

//        wb.SaveAs(path);

//        return path;
//    }
//}
