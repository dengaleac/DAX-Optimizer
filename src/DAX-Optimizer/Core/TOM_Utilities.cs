using Microsoft.AnalysisServices.Tabular;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace DAX_Optimizer.Core
{
    public static class TOM_Utilities
    {
        public static string GetTableMScript(Table table)
        {
            try
            {
                if (table.Partitions.Count == 0)
                    return "No partitions found";

                var partition = table.Partitions[0];

                switch (partition.Source)
                {
                    case MPartitionSource mSource:
                        return mSource.Expression;
                    case QueryPartitionSource querySource:
                        return $"SQL Query: {querySource.Query}";
                    case CalculatedPartitionSource calcSource:
                        return $"Calculated Table: {calcSource.Expression}";
                    default:
                        return $"Unsupported source type: {partition.Source.GetType().Name}";
                }
            }
            catch (Exception ex)
            {
                return $"Error retrieving M script: {ex.Message}";
            }
        }


    }
}
