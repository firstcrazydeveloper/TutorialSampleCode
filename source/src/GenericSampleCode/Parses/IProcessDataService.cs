namespace GenericSampleCode.DataParse.Parses
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    public interface IProcessDataService<T>
    {
        double GetMedianValue(IEnumerable<T> dataList);
        IEnumerable<T> FindAboveGivenValue(IEnumerable<T> dataList, double minValue, int compareValue);
        IEnumerable<T> FindBelowGivenValue(IEnumerable<T> dataList, double minValue, int compareValue);
        IEnumerable<T> FindInvalidData(IEnumerable<T> dataList);
        IEnumerable<T> FindAboveAndBelowGivenValue(IEnumerable<T> dataList, double minValue, int compareValue);
    }
}
