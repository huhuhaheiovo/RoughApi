namespace WpfApp1.Models
{
    public class RequestModel
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string Method { get; set; } = "GET";
        public string Url { get; set; } = "";
        public List<RequestParameter> Parameters { get; set; } = new();
        public List<RequestHeader> Headers { get; set; } = new();
        public string Body { get; set; } = "";
    }

    public enum ParameterType
    {
        Query,
        Path
    }

    public class RequestParameter
    {
        public bool IsEnabled { get; set; } = true;
        public string Key { get; set; } = "";
        public string Value { get; set; } = "";
        public string Type { get; set; } = "string";
        public string Description { get; set; } = "";
        public ParameterType ParameterType { get; set; } = ParameterType.Query;
        public bool IsRequired { get; set; } = false;
    }

    public class RequestHeader
    {
        public bool IsEnabled { get; set; } = true;
        public string Key { get; set; } = "";
        public string Value { get; set; } = "";
        public string Description { get; set; } = "";
    }
} 