namespace GenericSampleCode.DataParse.Calculations
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    public interface ICalculationService
    {
        double CaculateMedianValue(IEnumerable<double?> valueList);
        double FindPercentageValue(double minValue, int compareValue);
    }
}
