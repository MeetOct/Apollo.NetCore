using System.Collections.Generic;
using System.Linq;

namespace Apollo.NetCore.Models
{
    public class ApolloConfig
    {
        public ApolloConfig()
        {
        }

        public ApolloConfig(string appId, string cluster, string namespaceName, string releaseKey)
        {
            this.AppId = appId;
            this.AppId = cluster;
            this.NamespaceName = namespaceName;
            this.ReleaseKey = releaseKey;
        }

        public string AppId { get; set; }

        public string Cluster { get; set; }

        public string NamespaceName { get; set; }

        public string ReleaseKey { get; set; }

        public IDictionary<string, string> Configurations { get; set; }

        public override string ToString()
        {
            return "ApolloConfig{" + "appId='" + AppId + '\'' + ", cluster='" + Cluster + '\'' +
                ", namespaceName='" + NamespaceName + '\'' + $"configurations={string.Join(";", Configurations.Select(c => $"{c.Key}:{c.Value}"))};releaseKey={ReleaseKey}" +
                ", releaseKey='" + ReleaseKey + '\'' + '}';
        }

    }
}
