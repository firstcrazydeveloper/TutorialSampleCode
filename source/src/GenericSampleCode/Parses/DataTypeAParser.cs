namespace GenericSampleCode.DataParse.Parses
{
    using GenericSampleCode.DataParse.Models;
    using System;
    using System.Collections.Generic;

    public class DataTypeAParser<T> : IProcessDataService<T> where T : DataTypeA
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
