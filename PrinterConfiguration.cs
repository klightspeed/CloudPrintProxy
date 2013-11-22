using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;

namespace TSVCEO.CloudPrint
{
    public class PrinterConfiguration : ConfigurationElement
    {
        public const string DefaultJobPrinter = "Ghostscript";
        public static readonly Type DefaultJobPrinterType = typeof(Printing.Ghostscript);

        [ConfigurationProperty("name", IsKey = true, IsRequired = true)]
        public string Name { get { return (string)base["name"]; } set { base["name"] = value; } }

        [ConfigurationProperty("jobPrinter", IsRequired = false, DefaultValue = DefaultJobPrinter)]
        public string JobPrinter { get { return (string)base["jobPrinter"]; } set { base["jobPrinter"] = value; } }
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
        [ConfigurationProperty("defaultJobPrinter", IsRequired = false, DefaultValue = PrinterConfiguration.DefaultJobPrinter)]
        public string DefaultJobPrinter { get { return (string)base["defaultJobPrinter"]; } set { base["defaultJobPrinter"] = value; } }

        [ConfigurationProperty("printers")]
        public PrinterConfigurationCollection Printers { get { return (PrinterConfigurationCollection)base["printers"]; } set { base["printers"] = value; } }
    }
}
