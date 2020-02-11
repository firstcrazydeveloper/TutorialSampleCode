namespace GenericSampleCode.DataParse.Parses
{
    using GenericSampleCode.DataParse.FileReaders;
    using GenericSampleCode.DataParse.Models;
    using System;
    using System.Collections.Generic;
    using System.Text;

    public class DataTypeBParser<T> : IProcessDataService<T> where T : DataTypeB
    {
        public IEnumerable<T> FindAboveAndBelowGivenValue(IEnumerable<T> dataList, double minValue, int compareValue)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<T> FindAboveGivenValue(IEnumerable<T> dataList, double minValue, int compareValue)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<T> FindBelowGivenValue(IEnumerable<T> dataList, double minValue, int compareValue)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<T> FindInvalidData(IEnumerable<T> dataList)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<T> GenerateData()
        {
            throw new NotImplementedException();
        }

        public double GetMedianValue(IEnumerable<T> dataList)
        {
            throw new NotImplementedException();
        }
    }
}
