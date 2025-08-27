using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScaleRecordApp.Helpers
{
    public class ReportSettings
    {
        public bool AutoReportEnabled { get; set; } = true;
        public int AutoReportHour { get; set; } = 7;
        public List<string> ChatIds { get; set; } = new();
        public string TelegramBotToken { get; set; } = "";
    }

}
