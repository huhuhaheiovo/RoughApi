using System.Text.Json;
using System.IO;
using WpfApp1.Models;

namespace WpfApp1.Services
{
    public class HistoryManager
    {
        private readonly string _historyFile;
        private List<RequestHistory> _histories;

        public HistoryManager()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appFolder = Path.Combine(appData, "WpfApp1");
            Directory.CreateDirectory(appFolder);
            _historyFile = Path.Combine(appFolder, "request_history.json");
            // aaa
            _histories = LoadHistories();
        }

        private List<RequestHistory> LoadHistories()
        {
            if (!File.Exists(_historyFile))
                return new List<RequestHistory>();

            try
            {
                var json = File.ReadAllText(_historyFile);
                return JsonSerializer.Deserialize<List<RequestHistory>>(json) ?? new List<RequestHistory>();
            }
            catch
            {
                return new List<RequestHistory>();
            }
        }

        public void SaveHistory(RequestHistory history)
        {
            _histories.Insert(0, history); // 在开头插入新记录
            
            // 只保留最近100条记录
            if (_histories.Count > 100)
                _histories = _histories.Take(100).ToList();

            SaveToFile();
        }

        private void SaveToFile()
        {
            try
            {
                var json = JsonSerializer.Serialize(_histories, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                File.WriteAllText(_historyFile, json);
            }
            catch (Exception ex)
            {
                // 在实际应用中，你可能想要记录日志
                System.Diagnostics.Debug.WriteLine($"保存历史记录失败: {ex.Message}");
            }
        }

        public List<RequestHistory> GetHistories()
        {
            return _histories;
        }

        public void ClearHistories()
        {
            _histories.Clear();
            SaveToFile();
        }
    }
} 