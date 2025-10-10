using System;
using System.Collections.Generic;
using System.Text;

namespace DeepSigma.DataAccess.Utilities;

public static class CsvUtilities
{
    /// <summary>
    /// Deserializes a CSV string into a list of objects of type T.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="csvData"></param>
    /// <returns></returns>
    public static IEnumerable<T> LoadObjectsFromCSV<T>(string csvData) where T : class
    {
        using (var reader = new StringReader(csvData))
        using (var csv = new CsvHelper.CsvReader(reader, System.Globalization.CultureInfo.InvariantCulture))
        {
            IEnumerable<T> records = csv.GetRecords<T>();
            return records;
        }
    }
}   
