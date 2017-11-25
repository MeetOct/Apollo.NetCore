using Apollo.NetCore.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Apollo.NetCore.Internals
{
    public delegate void ConfigChangeEvent(object sender, ConfigChangeEventArgs args);
    public interface IConfig
    {
        string GetProperty(string key, string defaultValue);

        /// <summary>
        /// Config change event subscriber
        /// </summary>
        event ConfigChangeEvent ConfigChanged;
    }
}
