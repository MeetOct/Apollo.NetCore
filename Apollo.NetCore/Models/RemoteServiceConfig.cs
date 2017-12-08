namespace Apollo.NetCore.Models
{
    public class RemoteServiceConfig
    {
        public string AppName { get; set; }

        public string HomepageUrl { get; set; }

        public string InstanceId { get; set; }

        public override string ToString()
        {
            return "ServiceDTO{" + "appName='" + AppName + '\'' + ", instanceId='" + InstanceId +
                '\'' + ", homepageUrl='" + HomepageUrl + '\'' + '}';
        }

    }
}
