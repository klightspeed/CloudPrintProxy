using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;

namespace TSVCEO.CloudPrint
{
    public class PrinterConfiguration : ConfigurationElement
    {
        [ConfigurationProperty("name", IsKey = true, IsRequired = true)]
        public string Name { get { return (string)base["name"]; } set { base["name"] = value; } }

        [ConfigurationProperty("jobPrinter", IsRequired = false, DefaultValue = "Ghostscript")]
        public string JobPrinter { get { return (string)base["name"]; } set { base["name"] = value; } }
    }

    [ConfigurationCollection(typeof(PrinterConfiguration))]
    public class PrinterConfigurationCollection : ConfigurationElementCollection
    {
        public override ConfigurationElementCollectionType CollectionType { get { return ConfigurationElementCollectionType.BasicMapAlternate; } }

        protected override string ElementName { get { return "printer"; } }
        protected override bool IsElementName(string elementName) { return elementName == ElementName; }
        public override bool IsReadOnly() { return false; }
        protected override ConfigurationElement CreateNewElement() { return new PrinterConfiguration(); }
        protected override object GetElementKey(ConfigurationElement element) { return ((PrinterConfiguration)element).Name; }
        public PrinterConfiguration this[int idx] { get { return (PrinterConfiguration)base.BaseGet(idx); } }
    }

    public class PrinterConfigurationSection : ConfigurationSection
    {
        [ConfigurationProperty("printers")]
        public PrinterConfigurationCollection Printers { get { return (PrinterConfigurationCollection)base["printers"]; } set { base["printers"] = value; } }
    }
}
