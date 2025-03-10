namespace WpfApp1.Models
{
    public class RequestHistory
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public DateTime RequestTime { get; set; } = DateTime.Now;
        public string Name { get; set; } = "";
        public string Method { get; set; } = "";
        public string Url { get; set; } = "";
        public List<RequestParameter> Parameters { get; set; } = new();
        public List<RequestHeader> Headers { get; set; } = new();
        public string Body { get; set; } = "";
        public string Response { get; set; } = "";
        public int StatusCode { get; set; }
        public long ExecutionTime { get; set; } // 执行时间(毫秒)
    }
} 