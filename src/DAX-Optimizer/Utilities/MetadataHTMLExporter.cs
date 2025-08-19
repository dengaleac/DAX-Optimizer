using DAX_Optimizer.AI;
using DAX_Optimizer.Core;
using OpenAI.Chat;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Documents;
using MAT = Microsoft.AnalysisServices.Tabular;


namespace DAX_Optimizer.Utilities
{
    public class ExpressionMeasure
    {
        public string Name { get; set; }
        public string Expression { get; set; } 
        public string Explanation { get; set; } = string.Empty;
    }



    public class ExpressionTable
    {
        public string Name { get; set; }        
        public List<ExpressionMeasure> Measures { get; set; } = new List<ExpressionMeasure>();
    }

    public class ExpressionModel
    {
        public string ModelName { get; set; }
        public List<ExpressionTable> Tables { get; set; } = new List<ExpressionTable>();        
    }


    public class MetadataHTMLExporter
    {      

        public MetadataHTMLExporter()
        {

        }


        public void ExportToHtml(MAT.Model _model, ExpressionModel _expressionModel, string filePath)
        {   
            var html = GenerateHtmlDocument(_model,_expressionModel);
            File.WriteAllText(filePath, html, Encoding.UTF8);
        }

        

        private string GenerateHtmlDocument(MAT.Model _model, ExpressionModel _expressionModel)
        {
            var sb = new StringBuilder();

            // Group items by type
            //var tables = metadataItems.Where(x => x.Type == MetadataItemType.Table).ToList();
            //var columns = metadataItems.Where(x => x.Type == MetadataItemType.Column).ToList();
            //var measures = metadataItems.Where(x => x.Type == MetadataItemType.Measure).ToList();
            //var calculatedColumns = metadataItems.Where(x => x.Type == MetadataItemType.CalculatedColumn).ToList();

            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang=\"en\">");
            sb.AppendLine("<head>");
            sb.AppendLine("    <meta charset=\"UTF-8\">");
            sb.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
            sb.AppendLine("    <title>Metadata Documentation</title>");
            sb.AppendLine("    <style>");
            sb.AppendLine(GetCssStyles());
            sb.AppendLine("    </style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");

            sb.AppendLine("    <div class=\"container\">");
            sb.AppendLine("        <h1>📋 Metadata Documentation</h1>");
            sb.AppendLine($"        <p class=\"timestamp\">Generated on: {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>");

            // Generate Tables section
            if (_model.Tables.Any())
            {
                sb.AppendLine("        <section class=\"section\">");
                sb.AppendLine("            <h2>📊 Tables</h2>");

                foreach (MAT.Table table in _model.Tables)
                {
                    sb.AppendLine("            <div class=\"item-card\">");
                    sb.AppendLine($"                <h3>{EscapeHtml(table.Name)}</h3>");
                    sb.AppendLine("                <div class=\"properties\">");                    
                    //sb.AppendLine($"                    <div class=\"property\"><strong>Type:</strong> {table.}</div>");
                    //if (!string.IsNullOrEmpty(table.Description))
                    //    sb.AppendLine($"                    <div class=\"property\"><strong>DEscript:</strong> {EscapeHtml(table.DataType)}</div>");
                    if (!string.IsNullOrEmpty(table.Description))
                        sb.AppendLine($"                    <div class=\"property\"><strong>Description:</strong> {EscapeHtml(table.Description)}</div>");
                    if (!string.IsNullOrEmpty(TOM_Utilities.GetTableMScript(table)))
                    {
                        sb.AppendLine("                    <div class=\"property\">");
                        sb.AppendLine("                        <strong>Expression:</strong>");
                        sb.AppendLine($"                        <pre class=\"expression\">{EscapeHtml(TOM_Utilities.GetTableMScript(table))}</pre>");
                        sb.AppendLine("                    </div>");
                    }
                    sb.AppendLine("                </div>");

                    

                    // Show related columns and measures
                    
                    if (table.Columns.Any() || table.Measures.Any())
                    {
                        sb.AppendLine("                <div class=\"related-items\">");
                        sb.AppendLine("                    <h4>Related Items:</h4>");
                        sb.AppendLine("                    <ul>");

                        sb.AppendLine("        <section class=\"section\">");
                        sb.AppendLine("            <h2>📗 Columns</h2>");
                        GenerateColumnTable(sb, table.Columns);
                        sb.AppendLine("        </section>");

                        // Generate Measures section
                        if (table.Measures.Any())
                        {
                            sb.AppendLine("        <section class=\"section\">");
                            sb.AppendLine("            <h2>📏 Measures</h2>");
                            GenerateMeasureTable(sb, table.Name, table.Measures,_expressionModel);
                            sb.AppendLine("        </section>");
                        }

                        sb.AppendLine("                    </ul>");
                        sb.AppendLine("                </div>");
                    }
                    sb.AppendLine("            </div>");


                }
                sb.AppendLine("        </section>");


            }

            sb.AppendLine("    </div>");
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            return sb.ToString();
        }


        private void GenerateColumnTable(StringBuilder sb, MAT.ColumnCollection columns)
        {
            //columns[0].Name
            //columns[0].DataType
            //columns[0].Description
            //    columns[0].SummarizeBy
            //    columns[0].SortByColumn
            //    columns[0].IsHidden
            //    columns[0].FormatString
            //    columns[0].Type
            //    if(columns[0].Type == MAT.ColumnType.Calculated)
            //{
            //    ((CalculatedColumn)columns[0]).Expression
            //}

            sb.AppendLine("            <div class=\"table-container\">");
            sb.AppendLine("                <table>");
            sb.AppendLine("                    <thead>");
            sb.AppendLine("                        <tr>");
            sb.AppendLine("                            <th>Name</th>");
            sb.AppendLine("                            <th>Data Type</th>");
            sb.AppendLine("                            <th>Description</th>");
            sb.AppendLine("                            <th>Format String</th>");
            //sb.AppendLine("                            <th>Sort by</th>");
            sb.AppendLine("                            <th>IsHidden</th>");
            sb.AppendLine("                            <th>Expression</th>");
            sb.AppendLine("                        </tr>");
            sb.AppendLine("                    </thead>");
            sb.AppendLine("                    <tbody>");

            foreach (var column in columns.OrderBy(x => x.Name))
            {
                sb.AppendLine("                        <tr>");
                sb.AppendLine($"                            <td><strong>{column.Name}</strong></td>");
                sb.AppendLine($"                            <td>{EscapeHtml(column.DataType.ToString())}</td>");
                sb.AppendLine($"                            <td>{EscapeHtml(column.Description ?? "")}</td>");
                sb.AppendLine($"                            <td>{EscapeHtml(column.FormatString ?? "")}</td>");
                //sb.AppendLine($"                            <td>{EscapeHtml(column.SortByColumn??.Name ?? "")}</td>");
                sb.AppendLine($"                            <td>{EscapeHtml(column.IsHidden.ToString() ?? "")}</td>");
                sb.AppendLine($"                            <td>");
                if(column.Type == MAT.ColumnType.Calculated)
                {
                    var calcol = (MAT.CalculatedColumn)column;
                    if (!string.IsNullOrEmpty(calcol.Expression))
                    {
                        sb.AppendLine($"                                <pre class=\"expression-small\">{EscapeHtml(calcol.Expression)}</pre>");
                    }
                }              
                sb.AppendLine($"                            </td>");
                sb.AppendLine("                        </tr>");
            }

            sb.AppendLine("                    </tbody>");
            sb.AppendLine("                </table>");
            sb.AppendLine("            </div>");
        }


        private void GenerateMeasureTable(StringBuilder sb, string tableName, MAT.MeasureCollection measures, ExpressionModel _expressionModel)
        {
            
            sb.AppendLine("            <div class=\"table-container\">");
            sb.AppendLine("                <table>");
            sb.AppendLine("                    <thead>");
            sb.AppendLine("                        <tr>");
            sb.AppendLine("                            <th>Name</th>");
            sb.AppendLine("                            <th>Data Type</th>");
            sb.AppendLine("                            <th>Description</th>");
            sb.AppendLine("                            <th>Format String</th>");            
            sb.AppendLine("                            <th>IsHidden</th>");
            sb.AppendLine("                            <th>Expression</th>");            
            sb.AppendLine("                        </tr>");
            sb.AppendLine("                    </thead>");
            sb.AppendLine("                    <tbody>");

            foreach (var measure in measures.OrderBy(x => x.Name))
            {
                sb.AppendLine("                        <tr>");
                sb.AppendLine($"                            <td><strong>{measure.Name}</strong></td>");
                sb.AppendLine($"                            <td>{EscapeHtml(measure.DataType.ToString())}</td>");
                sb.AppendLine($"                            <td>{EscapeHtml(measure.Description ?? "")}</td>");
                sb.AppendLine($"                            <td>{EscapeHtml(measure.FormatString?? "")}</td>");
                sb.AppendLine($"                            <td>{EscapeHtml(measure.IsHidden.ToString() ?? "")}</td>");                
                sb.AppendLine($"                            <td>");
                
                sb.AppendLine($"                                <pre class=\"expression-small\">{EscapeHtml(measure.Expression)}</pre>");
                
                if (_expressionModel != null && _expressionModel.Tables.Any(t => t.Name == tableName))
                {
                    var exprTable = _expressionModel.Tables.FirstOrDefault(t => t.Name == tableName);
                    if (exprTable != null)
                    {
                        var exprMeasure = exprTable.Measures.FirstOrDefault(m => m.Name == measure.Name);
                        if (exprMeasure != null && !string.IsNullOrEmpty(exprMeasure.Explanation))
                        {
                            sb.AppendLine($"                                <pre class=\"expression-small\">{EscapeHtml(exprMeasure.Explanation)}</pre>");
                        }
                    }
                }

                sb.AppendLine($"                            </td>");
                sb.AppendLine("                        </tr>");
            }

            sb.AppendLine("                    </tbody>");
            sb.AppendLine("                </table>");
            sb.AppendLine("            </div>");
        }


        private string GetCssStyles()
        {
            return @"
        body {
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            line-height: 1.6;
            margin: 0;
            padding: 0;
            background-color: #f5f5f5;
            color: #333;
        }
        
        .container {
            max-width: 1200px;
            margin: 0 auto;
            padding: 20px;
            background-color: white;
            box-shadow: 0 0 10px rgba(0,0,0,0.1);
        }
        
        h1 {
            color: #2c3e50;
            border-bottom: 3px solid #3498db;
            padding-bottom: 10px;
            margin-bottom: 30px;
        }
        
        h2 {
            color: #34495e;
            margin-top: 40px;
            margin-bottom: 20px;
            padding: 10px;
            background-color: #ecf0f1;
            border-left: 4px solid #3498db;
        }
        
        h3 {
            color: #2c3e50;
            margin-bottom: 15px;
        }
        
        h4 {
            color: #7f8c8d;
            margin-bottom: 10px;
        }
        
        .timestamp {
            color: #7f8c8d;
            font-style: italic;
            margin-bottom: 30px;
        }
        
        .section {
            margin-bottom: 40px;
        }
        
        .item-card {
            background-color: #fafafa;
            border: 1px solid #ddd;
            border-radius: 8px;
            padding: 20px;
            margin-bottom: 20px;
            box-shadow: 0 2px 4px rgba(0,0,0,0.1);
        }
        
        .properties {
            margin-top: 15px;
        }
        
        .property {
            margin-bottom: 10px;
            padding: 8px;
            background-color: white;
            border-radius: 4px;
            border-left: 3px solid #3498db;
        }
        
        .related-items {
            margin-top: 20px;
            padding: 15px;
            background-color: #e8f4fd;
            border-radius: 6px;
            border: 1px solid #bee5eb;
        }
        
        .related-items ul {
            margin: 10px 0;
            padding-left: 20px;
        }
        
        .related-items li {
            margin-bottom: 5px;
        }
        
        .table-container {
            overflow-x: auto;
            margin-top: 20px;
        }
        
        table {
            width: 100%;
            border-collapse: collapse;
            background-color: white;
            box-shadow: 0 2px 4px rgba(0,0,0,0.1);
        }
        
        th {
            background-color: #3498db;
            color: white;
            padding: 12px;
            text-align: left;
            font-weight: bold;
        }
        
        td {
            padding: 12px;
            border-bottom: 1px solid #ddd;
            vertical-align: top;
        }
        
        tr:nth-child(even) {
            background-color: #f9f9f9;
        }
        
        tr:hover {
            background-color: #e3f2fd;
        }
        
        .expression {
            background-color: #2c3e50;
            color: #ecf0f1;
            padding: 15px;
            border-radius: 6px;
            font-family: 'Courier New', monospace;
            font-size: 14px;
            line-height: 1.4;
            margin: 10px 0;
            overflow-x: auto;
            white-space: pre-wrap;
            word-wrap: break-word;
        }
        
        .expression-small {
            background-color: #2c3e50;
            color: #ecf0f1;
            padding: 8px;
            border-radius: 4px;
            font-family: 'Courier New', monospace;
            font-size: 12px;
            line-height: 1.3;
            margin: 5px 0;
            overflow-x: auto;
            white-space: pre-wrap;
            word-wrap: break-word;
            max-width: 300px;
        }
        
        @media (max-width: 768px) {
            .container {
                padding: 10px;
            }
            
            table {
                font-size: 14px;
            }
            
            th, td {
                padding: 8px;
            }
        }";
        }

        private string EscapeHtml(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            return text.Replace("&", "&amp;")
                      .Replace("<", "&lt;")
                      .Replace(">", "&gt;")
                      .Replace("\"", "&quot;")
                      .Replace("'", "&#39;");
        }
    }

}
