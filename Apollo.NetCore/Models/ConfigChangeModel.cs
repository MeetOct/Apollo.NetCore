using Apollo.NetCore.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Apollo.NetCore.Models
{
    /// <summary>
    /// Holds the information for a config change.
    /// </summary>
    public class ConfigChange
    {
        private string namespaceName;
        private string propertyName;

        /// <summary>
        /// Constructor. </summary>
        /// <param name="namespace"> the namespace of the key </param>
        /// <param name="propertyName"> the key whose value is changed </param>
        /// <param name="oldValue"> the value before change </param>
        /// <param name="newValue"> the value after change </param>
        /// <param name="changeType"> the change type </param>
        public ConfigChange(string namespaceName, string propertyName, string oldValue, string newValue,
            PropertyChangeType changeType)
        {
            this.namespaceName = namespaceName;
            this.propertyName = propertyName;
            this.OldValue = oldValue;
            this.NewValue = newValue;
            this.ChangeType = changeType;
        }

        public string PropertyName
        {
            get
            {
                return propertyName;
            }
        }

        public string OldValue { get; set; }

        public string NewValue { get; set; }

        public PropertyChangeType ChangeType { get; set; }

        public string Namespace
        {
            get
            {
                return namespaceName;
            }
        }

        public override string ToString()
        {
            return "ConfigChange{" +
                "namespace='" + namespaceName + '\'' +
                ", propertyName='" + propertyName + '\'' +
                ", oldValue='" + OldValue + '\'' +
                ", newValue='" + NewValue + '\'' +
                ", changeType=" + ChangeType +
                '}';
        }
    }
}
