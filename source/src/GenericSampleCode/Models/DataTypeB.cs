namespace GenericSampleCode.DataParse.Models
{
    using System.Collections.Generic;

    public class DataTypeB
    {
        public DataTypeB()
        {
            InvalidDataList = new Dictionary<string, string>();
        }
        public string DeviceId { get; set; }
        public string MachineId { get; set; }
        public string ModeType { get; set; }
        public long duration { get; set; }
        public string Rate { get; set; }
        public bool IsValidData { get; set; }
        public IDictionary<string, string> InvalidDataList { get; set; }
    }
}
