namespace GenericSampleCode.DataParse.Calculations
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    public class CalculationService : ICalculationService
    {
        public double CaculateMedianValue(IEnumerable<double?> valueList)
        {
            var orderedValueList = valueList.OrderBy(val => val).ToList();
            int size = valueList.Count();
            int mid = size / 2;
            double median = (size % 2 != 0) ? (double)orderedValueList[mid] : ((double)orderedValueList[mid] +
                (double)orderedValueList[mid - 1]) / 2;

            return median;
        }



        public double FindPercentageValue(double minValue, int compareValue)
        {
            double percentageVal = ((double)(compareValue * minValue) / 100);
            return percentageVal;
        }
    }
}
