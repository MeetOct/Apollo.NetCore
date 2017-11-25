namespace Apollo.NetCore.Models
{
    public class ApolloSettings
    {
        public string AppID { get; set; }

        public string Cluster { get; set; }

        public string Url { get; set; }

        public int Timeout { get; set; }

        public int RefreshInterval { get; set; }
    }
}
